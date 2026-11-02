# Doctor Workspace prerequisite — Admin-controlled Staff account linking

Completed 2026-10-14. The existing Phase 1 association model/resolver were retained. This feature adds controlled provisioning of existing records, not Doctor Workspace or scheduling functionality. No operational database was modified by verification.

## 1. Summary

Admin can search existing accounts, deliberately link an active Staff record and select Unspecified/Doctor classification, or explicitly unlink. Backend policy and service checks enforce Admin access. Association changes and Identity security-stamp rotation share one PostgreSQL transaction. Exact repeated links are no-ops; different associations cannot be silently overwritten. Staff details now show safe account identity and classification, with confirmation/pending/error handling.

## 2. Backend flow

StaffEndpoints → MediatR queries/commands → IStaffAccountService → Infrastructure StaffAccountService → existing ApplicationDbContext / Identity UserManager.

| Endpoint | Behavior |
|---|---|
| GET /api/v1/staff/account-lookup | Admin account search/paging |
| POST /api/v1/staff/{id}/account-link | Admin explicit association/classification; returns committed safe account summary |
| DELETE /api/v1/staff/{id}/account-link | Admin explicit unlink/reset; returns 204 |
| GET /api/v1/staff | Existing Admin directory, now enriched with StaffKind and linked account summary |

All new endpoints require existing CanManageStaff, which requires Admin. The service also checks an authenticated Admin principal and a consistent nonempty current account ID. Provisioning accepts target IDs because Admin explicitly chooses existing records; these inputs are never used by CurrentStaffService to resolve the caller's Doctor identity.

## 3. Account lookup design

The bounded lookup searches account name/email, defaults to 20 rows, allows page sizes 1–50, page numbers 1–10000, and search up to 100 characters. Results are ordered by last name, first name and ID. A pageSize+1 read determines HasMore without returning an unlimited table.

Projection fields are UserId, first/last name, email, current roles, IsActive and IsLinkedToStaff. IsActive reflects account activity and current lockout eligibility without exposing lockout details. IsLinkedToStaff includes soft-deleted owners, whose association remains reserved. Inactive/already-linked candidates are displayed as unavailable in the dialog.

No password hash, security/concurrency stamp, token, username, lockout dates or other unnecessary Identity fields appear in this DTO. The lookup is administrative discovery only; current Staff/Doctor authorization remains a live UserId-based resolver operation.

## 4. Link validation rules

Before authoritative reads, begin a ReadCommitted transaction. Under the row locks, validate:

- Nonempty Staff/account IDs and an explicitly supplied defined classification.
- Staff exists, is not deleted, and is active.
- Target Identity account exists, is active, and is not currently locked out.
- Doctor role consistency when classification is Doctor.
- Target account is not associated with any other Staff, including deleted rows.
- Target Staff is not associated with a different account.

Domain LinkUserId and AssignStaffKind perform the actual mutation. A different existing association returns safe 409 and requires explicit unlink first. Same account/classification returns 200 with the current summary and does not rotate the stamp or change audit timestamps. Same account with a different supported classification is an explicit classification update and revokes its existing sessions.

Requests use the existing numeric enum convention: 0 = Unspecified, 1 = Doctor. Omitting StaffKind is rejected, rather than implicitly treating omission as an explicit classification. Invalid GUID JSON/empty IDs/undefined kind are 400; missing/deleted records are 404; eligibility/ownership conflicts are safe 409.

## 5. Doctor role consistency

StaffKind.Doctor requires UserManager.IsInRoleAsync(account, Doctor) to succeed against current persisted Identity membership. Admin status, specialty, license, department, name and email are not substitutes. Selecting Doctor does not assign any role. The Admin must manage roles separately before linking if appropriate.

Unspecified is permitted for active eligible accounts but is clearly labeled as no verified clinical role. It does not satisfy GetCurrentDoctorAsync. Existing CurrentStaffService still requires the live Doctor role, explicit Doctor kind, and active/non-deleted linked Staff.

## 6. Unlink behavior

DELETE explicitly calls Domain UnlinkUserId and AssignStaffKind(Unspecified). It keeps the Staff and Identity records, their roles and account activity state. If an association existed, its account's security stamp is rotated. An already unlinked Unspecified record is a no-op 204 with no revocation. An unlinked record with a retained classification is reset explicitly.

Inactive Staff/accounts may be unlinked for cleanup; eligibility checks that prohibit new links do not prevent removing an association. Deleted Staff are rejected by this feature's record lookup, and are not resurrected or silently released. Deleted associations remain reserved until a separate controlled reconciliation handles those records.

Reassignment is two deliberate Admin operations: unlink, then explicitly link the new account. No implicit replacement or account deletion exists.

## 7. Concurrency strategy

Use ReadCommitted with a consistent order:

**Staff row FOR UPDATE → affected Identity user row FOR UPDATE → fresh validation → Domain changes → persistence → commit.**

Same Staff/different accounts serialize at the Staff row; the contender sees the winner's committed association and cannot overwrite it. Same account/different Staff serialize at the account row; the contender sees the newly committed owner and is rejected. The existing unique UserId index and FK remain final database protections.

Unlink uses the same order, locking its current associated account after Staff. Verify-vs-reassignment is not a new workflow; unlink-vs-link tests prove one valid serial order: unlink first permits the explicit new link, or the new link first conflicts with the old owner and unlink leaves the Staff unlinked.

Fresh locked Staff/account reads detach earlier tracked instances to avoid cached ownership/stamp values. No broad concurrency framework, new xmin column, Serializable isolation or automatic retry was introduced. EF concurrency failures and PostgreSQL 23505, 23503, 40001 and 40P01 become safe 409 responses. Other storage failures become a generic 500 instructing refresh. Rollback clears tracked state.

## 8. Session invalidation strategy

Call existing UserManager.UpdateSecurityStampAsync for every actual link, unlink or classification change affecting an account. No new revocation mechanism or JWT claims were created.

The Identity EF store and Staff service share the scoped ApplicationDbContext. UserManager may save both pending Staff changes and the stamp internally, but this occurs inside the explicit outer transaction. Neither becomes committed/visible until the service commits. IdentityResult failures and database failures roll back the entire operation. Identity concurrency failure maps to 409; other Identity stamp-update failure maps to a safe 500.

Actual HTTP tests use the existing AccountJwtAuthentication, JwtSessionValidator and JwtTokenGenerator with real persisted users. Old tokens become 401 after link/unlink. Fresh tokens resolve the linked Staff/Doctor. After unlink, a freshly issued valid token authenticates but Doctor resolution fails. Forced failed link/unlink leaves the previous stamp and session valid.

If Admin associates its own account, that successful response can complete, but its old token is rejected on the next request; signing in again is required. This is tested. A lost response/commit acknowledgement remains uncertain; refresh or sign in before deciding whether to repeat. No automatic mutation retry exists.

## 9. Audit behavior

Existing AuditableEntitySaveChangesInterceptor retains Staff.CreatedBy/CreatedAtUtc and records ModifiedBy/ModifiedAtUtc from the acting Admin. Tests verify the Admin GUID and unchanged original creator. Audit modification and stamp/association share the transaction, including rollback on failure.

No separate enterprise audit/event system was added. Existing fields record the latest modifier; they are not a complete immutable association-history ledger. Exact idempotent requests do not create fictitious modifications.

## 10. Admin frontend changes

The existing Staff detail modal shows Linked/Not Linked, safe account name/email and StaffKind. Admin can open Link Account for active unlinked Staff, or confirm Unlink Account for linked Staff.

The small linking dialog provides explicit account search, paged results, active/already-linked flags, explicit selection, and classification options for only Unspecified/Doctor. Doctor is disabled for non-Doctor-role accounts. Unspecified is labeled as no verified clinical role. No demo account or name/email-based selection is hardcoded.

Loading, empty, lookup failure, role incompatibility and mutation error states are visible. Shared account pending guards cover confirmation and request completion, duplicate submissions, competing account actions, target switching, Escape/backdrop/cancel while a mutation is pending. Stale search subscriptions are cancelled. Controls do not optimistically claim a successful association; they update after the committed API response. Errors read ProblemDetails.detail and refresh Staff state without retrying the mutation.

Existing SweetAlert confirmation/toast and Staff modal styling were reused. Routes, Doctor navigation and the rest of Staff CRUD were preserved.

## 11. Authorization enforcement

Backend CanManageStaff independently protects lookup, link and unlink. StaffAccountService also requires a valid authenticated Admin principal. Doctor-only, Nurse, Receptionist, Patient, Pharmacist and anonymous requests are tested and denied. Frontend visibility/guards are UX only.

CurrentStaffService and its no-argument current-user resolution remain unchanged. Browser-provided provisioning IDs do not become current Doctor authorization claims or parameters. JWT generation and existing account/session validation were not modified.

## 12. Tests added

**40 backend PostgreSQL/HTTP cases** in StaffAccountLinkTests. Coverage includes all requested scenarios plus:

- Exact-link idempotency and explicit classification change.
- Explicit classification presence and invalid JSON/enum inputs.
- Inactive/locked account rejection.
- Soft-deleted ownership reservation.
- Inactive Staff/account cleanup unlink.
- Safe lookup fields, paging limits, roles and availability.
- Two independent HTTP races for each ownership contention pattern.
- Unlink-versus-new-link serial outcomes.
- Admin self-association session revocation.
- Staff-write and Identity-stamp-write failure rollback.
- Failed unlink preserving association/classification/session.
- SQLSTATE conflict mapping without raw database message exposure.
- Existing model consistency.

Tests reuse the unchanged guarded PharmacyPostgresFixture, creating/dropping only randomly named databases in a separate disposable PostgreSQL 17 cluster. Independent HTTP scopes use real Npgsql, Identity UserManager/store, JWT session validator and audit interceptor. Test-only resolver/session endpoints exist solely inside the integration harness; no production Doctor endpoint was added.

Race tests hold an external Staff/account row lock and observe both independent requests blocked in PostgreSQL before releasing it. They do not use a SaveChanges barrier that would deadlock behind the production locks. Failure triggers operate only in the disposable database and are removed in finally blocks.

**12 Angular cases** cover non-Admin controls, lookup states, inactive/already-linked selection, role compatibility, pending/cross-action guards, committed-state updates, confirmation cancellation, safe conflict details with no retry, unlink retention/reset, unlink failure, stale search cancellation and lookup failure.

## 13. Test/build results

Backend build: **passed, 0 warnings, 0 errors**.

Initial focused run: 36/36 passed. Four final cases were added and all **40 new cases passed in the complete suite**. Complete backend suite: **335 passed, 0 failed, 0 skipped**:

| Suite | Passed |
|---|---:|
| Domain | 50 |
| Application | 39 |
| API functional | 30 |
| API integration | 216 |

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-StaffLink-20261014" --no-restore
# Use only a separate disposable loopback cluster, never the ERP database.
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = 'Host=127.0.0.1;Port=<test-port>;Database=postgres;Username=health_erp_tests'
dotnet test HealthERP.slnx --no-build --artifacts-path "$env:TEMP\HealthERP-StaffLink-20261014" --logger 'trx;LogFileName=staff-link-full.trx' --logger 'console;verbosity=quiet'
```

Full TRX results are in each test project's TestResults/staff-link-full.trx. The owned cluster used available port 63607 after Windows denied binding the initially chosen port. No existing service was stopped. The generated test database list was empty after tests, and the owned cluster was shut down.

Angular production build: **passed**, with the existing SweetAlert2 CommonJS optimization warning.

```powershell
# From F:\Health ERP\health-erp-client
npm.cmd run build
npm.cmd test -- --watch=false
```

Complete Angular suite: **71 passed, 1 failed, 72 total**. All 12 new Staff account tests passed. The unchanged pre-existing failure is App > should render title at src/app/app.spec.ts:22: the scaffold expects an h1 that the current application does not render. It was not repaired or hidden.

No manual browser operation against operational Staff/account data was claimed. For manual verification: sign in as Admin, open /staff, view an active unlinked Staff profile, search/select an existing Doctor-role account, choose Doctor and confirm Link. Confirm linked details, sign in again as the affected account, then verify resolver behavior through later supported consumers. Confirm Unlink resets the profile to Unspecified and invalidates the affected account session. Direct non-Admin calls to all three new endpoints must be denied. Use disposable accounts and deliberate selections; no automatic matching occurs.

## 14. Exact files changed

New files:

- F:\Health ERP\src\Core\HealthERP.Application\Common\Interfaces\IStaffAccountService.cs
- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\Models\StaffAccountDtos.cs
- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\StaffAccountExceptions.cs
- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\Commands\ManageStaffAccount\StaffAccountCommands.cs
- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\Queries\LookupStaffAccounts\LookupStaffAccountsQuery.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\Services\StaffAccountService.cs
- F:\Health ERP\tests\HealthERP.Api.IntegrationTests\StaffAccountLinkTests.cs
- F:\Health ERP\health-erp-client\src\app\features\staff\staff-account-link.spec.ts
- F:\Health ERP\DOCTOR_WORKSPACE_ADMIN_LINKING_REPORT.md

Updated files:

- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\Models\StaffDto.cs
- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\Queries\GetStaff\GetStaffQuery.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\DependencyInjection.cs
- F:\Health ERP\src\Presentation\HealthERP.Api\EndPoints\StaffEndpoints.cs
- F:\Health ERP\src\Presentation\HealthERP.Api\Middleware\GlobalExceptionHandlingMiddleware.cs
- F:\Health ERP\health-erp-client\src\app\features\staff\data-access\staff.model.ts
- F:\Health ERP\health-erp-client\src\app\features\staff\data-access\staff.service.ts
- F:\Health ERP\health-erp-client\src\app\features\staff\staff-list.ts
- F:\Health ERP\health-erp-client\src\app\features\staff\staff-list.html

Generated test/build output is additional verification output, not product source. Earlier worktree changes to Domain Staff, StaffConfiguration, migrations/snapshot and other modules were preserved and not modified by this feature.

## 15. Migration/model status

**No new migration created. No database model/configuration changes.**

The real PostgreSQL ModelRequiresNoNewMigration check passes HasPendingModelChanges() == false. The EF CLI has-pending-model-changes command also reports no changes since the last migration. It uses isolated artifacts and a nonconnectable design-only connection, not an operational database update. Existing unrelated Patient query-filter warnings remain in EF tooling output.

## 16. Known limitations

- Admin must verify that the chosen account belongs to the intended person; this feature records an explicit association, not external identity proof.
- Existing user validation can prevent a stamp update for malformed legacy account data; the operation fails safely and rolls back.
- A lost response after commit requires refresh/sign-in to establish final state. There is no automatic retry or new idempotency-key framework.
- The existing audit fields store the latest modifier, not a complete immutable association event history.
- Deleted Staff are outside this directory/provisioning flow and are rejected; their reserved associations require separately controlled reconciliation.
- Roles may change later; the unchanged current Doctor resolver checks the live Doctor role and classification and fails closed. Linking does not replace role administration.
- The full Angular suite remains red solely because of the documented existing scaffold test.

## 17. Explicitly deferred work

Doctor Workspace UI/redirects/schedules, appointment scoping, Patient ownership, prescribing/lab attribution, invitations/email onboarding, account/password creation, automatic role changes, JWT Staff identity claims, automatic matching, broad Staff CRUD and unrelated authorization/UI changes.

## 18. TRUE/FALSE checklist

| Statement | Answer |
|---|---|
| Admin can explicitly link Staff to an existing Identity account. | TRUE |
| Doctor can self-link. | FALSE — Doctor-only cannot; an authorized Admin may manage its own association |
| Email/name matching is used. | FALSE — search is discovery, not association |
| Linking automatically assigns Identity roles. | FALSE |
| StaffKind.Doctor requires Doctor Identity role. | TRUE |
| Duplicate account associations are prevented. | TRUE |
| Existing association is silently overwritten. | FALSE |
| Concurrent linking can create duplicate ownership. | FALSE |
| Unlink deletes the Identity account. | FALSE |
| Unlink deletes the Staff record. | FALSE |
| Unlink clears the association. | TRUE |
| Association changes invalidate affected sessions. | TRUE — exact no-op links/unlinks do not rotate stamps |
| Backend independently enforces Admin authorization. | TRUE |
| Sensitive Identity fields are exposed by lookup. | FALSE |
| StaffId was added to JWT. | FALSE |
| Doctor Workspace was implemented. | FALSE |
| Doctor schedule endpoint was implemented. | FALSE |
| A new migration was created. | FALSE |
| HasPendingModelChanges is false. | TRUE |
| Backend build passes. | TRUE |
| Focused backend tests pass. | TRUE |
| Angular build passes. | TRUE |
