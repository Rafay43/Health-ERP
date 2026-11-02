# Doctor Workspace Phase 1 — Verified Identity ↔ Staff Association

Completed 2026-10-13. Phase 1 adds the association model and server-side resolver only. The migration was generated and tested in disposable PostgreSQL data; it was not applied to the ERP database. Existing uncommitted work from earlier tasks was preserved.

## 1. Summary

StaffMembers now has optional Guid? UserId and explicit StaffKind. PostgreSQL enforces a real Identity FK and unique non-null association, including soft-deleted Staff. ICurrentStaffService resolves the authenticated account's Staff or Doctor context using live database reads. There are no caller-selected identity parameters, JWT additions, frontend changes, schedule endpoints, or linking endpoints.

## 2. Root problem fixed

Previously Identity accounts and Staff records were independent, so an authenticated account could not be mapped authoritatively to scheduling's StaffId. Specialty, department, name and email were not verified identity or Doctor classification. This prerequisite introduces an explicit durable account association and independent Doctor designation without guessing from those values. Existing accounts/Staff remain unlinked until future controlled provisioning establishes an association.

## 3. Domain changes

The existing HealthERP.Domain.StaffMembers.StaffMembers entity gains private-set UserId and StaffKind properties. No Domain reference to ApplicationUser or Identity libraries was introduced.

- LinkUserId rejects Guid.Empty and refuses a different account when already linked.
- Repeating the same link is idempotent.
- UnlinkUserId explicitly clears the association and is idempotent.
- AssignStaffKind rejects undefined enum values and permits deliberate classification changes.
- Existing UpdateProfile, Activate and Deactivate behavior remains; profile/classification changes do not alter UserId.
- Creation does not link or classify Staff based on profile information.

Staff has no existing Domain email-update method, and none was added. Integration tests change stored account/Staff email and names through test setup and confirm resolution remains anchored to UserId.

## 4. StaffKind design

StaffKind is an integer-backed enum consistent with existing enum persistence:

| Value | Meaning |
|---|---|
| 0 — Unspecified | Default for new/existing unreviewed Staff |
| 1 — Doctor | Explicit Doctor classification |

No Nurse/Receptionist or speculative values were added. Specialty, license, department, names, emails and Identity roles do not automatically change StaffKind. Invalid values introduced outside Domain behavior are also rejected by the resolver.

## 5. EF relationship/constraints

StaffConfiguration defines:

- staff.staff.UserId: nullable PostgreSQL uuid.
- staff.staff.StaffKind: integer, non-null, default 0.
- IX_staff_UserId: unique with filter `"UserId" IS NOT NULL`.
- FK_staff_users_UserId: UserId → identity.users.Id, ON DELETE RESTRICT.
- A navigation-free one-to-one relationship with Staff as the dependent.

The index filter deliberately does not mention IsDeleted or IsActive. A deleted/inactive linked row continues reserving the account until explicitly unlinked. Staff's single scalar FK provides at most one account per row; the unique index provides at most one Staff row per account. Multiple null associations are allowed. No ApplicationUser navigation or new project dependency was necessary.

## 6. Migration details

Generated one migration: **20261013104925_AddVerifiedStaffIdentityAssociation**.

Manual review confirmed Up contains exactly:

1. Add StaffKind integer NOT NULL DEFAULT 0 to staff.staff.
2. Add nullable UserId uuid to staff.staff.
3. Create unique non-null UserId index.
4. Create the Identity FK with Restrict deletion.

There is no backfill, data matching, Doctor inference, role change or unrelated table change. Down removes only this FK/index/two columns. The generated designer and snapshot represent the current model; earlier existing snapshot changes were preserved.

The generated Down/Up operations were exercised inside a rolled-back transaction against pre-existing rows in the disposable test database. Legacy Staff retained profile data, became unlinked/Unspecified on upgrade, and existing Identity role memberships remained unchanged. Operational migration application is deferred; normal Development startup already runs migrations through the unchanged initializer.

## 7. CurrentStaffService design

Application interface:

```csharp
Task<CurrentStaffContext> GetCurrentStaffAsync(CancellationToken cancellationToken = default);
Task<CurrentStaffContext> GetCurrentDoctorAsync(CancellationToken cancellationToken = default);
```

The minimal sealed immutable record contains StaffId (the existing strongly typed Domain ID), UserId, and StaffKind. It exposes neither the tracked entity nor unrelated account/profile information.

Infrastructure resolution:

1. Require an authenticated HttpContext principal and a nonempty valid ICurrentUserService.UserId.
2. Require the principal's NameIdentifier to agree with that identity.
3. Read the Identity account live and reject missing, inactive or currently locked accounts.
4. Read Staff by UserId using IgnoreQueryFilters and AsNoTracking, taking at most two rows.
5. Require exactly one row. Multiple rows log an integrity failure and are denied, never reduced to the first result.
6. Explicitly reject inactive/deleted Staff and invalid classifications.
7. Return only the authoritative immutable context.

There is no email/name/first-Staff fallback and no result cache. Reused-scope tests confirm subsequent role removal and Staff deactivation are observed. Missing/ambiguous/ineligible association throws StaffAccessDeniedException. Middleware maps that exception to a generic 403 ProblemDetails; malformed/anonymous identity uses the existing 401 mapping. Storage failures are not converted into a successful or guessed association.

## 8. CurrentDoctor resolution rules

Current Doctor requires all valid current Staff/account conditions plus BOTH:

- Current persisted Identity membership in the Doctor role.
- Linked StaffKind.Doctor.

The role lookup joins Identity user_roles and roles in the database. A principal merely claiming Doctor does not override current persisted membership. Admin-only fails; Admin+Doctor succeeds only for its own valid Doctor Staff. Unspecified Staff fails even when the account has Doctor role.

## 9. Authentication guarantees relied upon

Existing AccountJwtAuthentication validates issuer, audience, signature and lifetime. AccountJwtBearerEvents and JwtSessionValidator validate account availability/activity, lockout and the security-stamp-derived session_version. These guarantees belong to authentication middleware and were not duplicated or modified.

CurrentStaffService independently checks an authenticated principal, consistent parsed identity, live account activity/lockout, Staff association/status and current Doctor membership/classification. It does not decode/validate JWT signatures or reimplement session_version checking. Future endpoints using this service must require normal authentication. Unit/integration setup principals are test authentication identities; unchanged account/session functional tests also passed in the complete suite.

No StaffId, StaffKind or DoctorId was added to JWT. JwtTokenGenerator and authentication/session code were unchanged.

## 10. Development data decision

The initializer was not changed. Tests explicitly create isolated accounts/roles/Staff, then associate/classify through Domain behavior. This exercises the prerequisite without taking over existing Staff or changing demo account behavior. Existing demo Doctor account remains unlinked unless separately provisioned later. No records were matched by email, name, department, specialty or license.

## 11. Tests added

**36 focused tests: 9 Domain and 27 real PostgreSQL integration cases.**

Domain cases cover null/Unspecified creation; valid and empty account IDs; overwrite rejection; identical-link idempotency; profile/classification preservation; explicit idempotent unlink and deliberate relink; explicit Doctor/Unspecified classification; invalid enum rejection.

PostgreSQL cases cover:

- Valid authoritative Staff/Doctor resolution.
- Missing/inactive/deleted Staff denial.
- Doctor role plus Doctor kind; Doctor role plus Unspecified denial.
- Nurse/Admin-only accounts with Doctor Staff denial, even with test principals claiming Doctor.
- Admin+Doctor resolving only its own Staff.
- Inactive/locked/missing Identity account denial.
- Anonymous, malformed and empty authenticated IDs.
- No email/name/first-Staff fallback.
- Account/Staff name/email changes preserving ID resolution.
- Invalid persisted classification denial.
- Live role removal and Staff deactivation in a reused scope.
- FK rejection of nonexistent account; duplicate account association rejection.
- Different accounts linking different Staff; multiple unlinked Staff.
- Soft-deleted Staff retaining uniqueness and linked-account hard-delete restriction.
- Concurrent independent SaveChanges attempts: exactly one same-account link succeeds.
- Defensive ambiguity including hidden deleted Staff.
- Generated migration preserving old rows as unlinked/Unspecified.
- HasPendingModelChanges == false.

The existing PharmacyPostgresFixture was reused unchanged. It requires an explicit separate loopback maintenance connection, creates a random health_erp_pharmacy_test_<guid> database, and drops only that database. No EF InMemory or mocked FK/concurrency behavior was used.

The concurrent association test coordinates two independent DbContexts before their inserts; there is no production anchor lock for this setup to deadlock against. PostgreSQL uniqueness arbitrates the writes. The ambiguity test temporarily drops the association index only inside a rolled-back transaction in the guarded test database, inserts a conflicting row and proves both resolvers deny. Rollback restores the index and removes the fault-injection row. Migration Down/Up testing likewise rolls back all changes.

## 12. Test/build results

Backend solution build: **passed, 0 warnings, 0 errors**.

Focused tests: **36 passed, 0 failed**.

Complete backend suite: **295 passed, 0 failed, 0 skipped**:

| Suite | Passed |
|---|---:|
| Domain | 50 |
| Application | 39 |
| API functional | 30 |
| API integration | 176 |

Commands used isolated temporary build artifacts, preserving the running development API's output:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-DoctorPhase1-20261013" --no-restore
# Set only to your separately created disposable loopback test cluster.
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = 'Host=127.0.0.1;Port=<test-port>;Database=postgres;Username=health_erp_tests'
dotnet test HealthERP.slnx --no-build --artifacts-path "$env:TEMP\HealthERP-DoctorPhase1-20261013" --logger 'trx;LogFileName=doctor-phase1-full.trx' --logger 'console;verbosity=quiet'
```

Full TRX results are in each existing test project's TestResults/doctor-phase1-full.trx. Focused results use doctor-phase1-focused.trx. The separate PostgreSQL 17 cluster used port 57319. Its maintenance query confirmed zero remaining generated test databases, and the owned cluster was stopped afterwards. No ERP database was used or migrated.

There were no backend test failures to hide or alter. Angular was not changed or rebuilt in this backend-only phase. Its previously documented unrelated scaffold failure was not modified. EF tooling still reports the existing Patient required-navigation/query-filter warnings; those unrelated model relationships were preserved.

## 13. Exact files changed

Existing files updated:

- F:\Health ERP\src\Core\HealthERP.Domain\StaffMembers\Staff.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\Persistence\Configurations\StaffConfiguration.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\DependencyInjection.cs
- F:\Health ERP\src\Presentation\HealthERP.Api\Middleware\GlobalExceptionHandlingMiddleware.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\Persistence\Migrations\ApplicationDbContextModelSnapshot.cs

New files:

- F:\Health ERP\src\Core\HealthERP.Domain\StaffMembers\StaffKind.cs
- F:\Health ERP\src\Core\HealthERP.Application\Common\Interfaces\ICurrentStaffService.cs
- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\Models\CurrentStaffContext.cs
- F:\Health ERP\src\Core\HealthERP.Application\StaffMembers\StaffAccessDeniedException.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\Services\CurrentStaffService.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\Persistence\Migrations\20261013104925_AddVerifiedStaffIdentityAssociation.cs
- F:\Health ERP\src\Infrastructure\HealthERP.Infrastructure\Persistence\Migrations\20261013104925_AddVerifiedStaffIdentityAssociation.Designer.cs
- F:\Health ERP\tests\HealthERP.Domain.UnitTests\StaffMembers\StaffAssociationTests.cs
- F:\Health ERP\tests\HealthERP.Api.IntegrationTests\CurrentStaffTests.cs
- F:\Health ERP\DOCTOR_WORKSPACE_PHASE_1_REPORT.md

Generated test outputs also remain under existing TestResults directories. Existing Staff creation handler/lookup, Patient changes and earlier migrations seen in git status were already present; they were not changed for Phase 1.

## 14. Migration/model consistency result

The real PostgreSQL integration model check passes. `dotnet ef migrations has-pending-model-changes` also completed successfully with **No changes have been made to the model since the last migration**. The EF command used isolated artifacts and an intentionally nonconnectable design-only connection, with no database update command.

**Migration created: yes. HasPendingModelChanges: false. Migration applied to ERP database: no.**

## 15. Known limitations

This is a prerequisite, not operational provisioning. A foreign key proves the account exists; the future controlled workflow must verify that it belongs to the intended staff member and explicitly classify Doctor eligibility. There is no browser path to self-link or change classification.

Domain overwrite guards protect deliberate entity operations, and the unique index protects duplicate UserId associations. A later Admin write workflow must still protect concurrent updates to the same Staff row, re-check current association, audit changes, and revoke affected sessions; no such workflow or new Staff concurrency column was added here.

Resolver reads are live, with no cross-request cache, but do not lock Staff for a future clinical operation. Later ownership-dependent writes must revalidate within their own protected operation. Existing broad appointment/Patient APIs remain as previously audited because their behavior is outside this phase.

The migration must be applied before using the new resolver against an operational database. Existing Staff remain unavailable to Doctor resolution until explicitly associated/classified. Generic current Staff may resolve an active Unspecified Staff; current Doctor may not.

## 16. Explicitly deferred work

- Controlled Admin invitation/provisioning/reconciliation and association writes.
- Administrative session revocation on link/unlink/reassignment.
- Doctor Workspace UI, routing and login redirect changes.
- Doctor personal schedule endpoints and appointment scoping changes.
- Patient clinical-access ownership changes.
- Prescription/lab author changes and unrelated role/policy refactoring.
- Additional StaffKind values and demo initialization changes.

No public link/unlink endpoint, self-linking operation, schedule endpoint, frontend change or JWT extension was added.

## 17. TRUE/FALSE checklist

| Statement | Answer |
|---|---|
| Staff now has an optional verified Identity association. | TRUE — explicit model/FK; human provisioning is deferred |
| Domain references ApplicationUser. | FALSE |
| Existing Staff were automatically linked. | FALSE |
| Existing Staff were automatically classified as Doctor. | FALSE |
| UserId has database uniqueness protection. | TRUE |
| UserId has a database FK to Identity users. | TRUE |
| Soft-deleted Staff still reserve their association. | TRUE |
| Hard deleting a linked Identity account is restricted. | TRUE |
| Current Staff is resolved from authenticated UserId. | TRUE |
| Resolver accepts browser StaffId. | FALSE |
| Email/name is used as authorization fallback. | FALSE |
| Current Doctor requires Identity Doctor role. | TRUE |
| Current Doctor requires StaffKind.Doctor. | TRUE |
| Inactive/deleted Staff are rejected. | TRUE |
| StaffId was added to JWT. | FALSE |
| A public link/unlink endpoint was added. | FALSE |
| Doctor Workspace UI was implemented. | FALSE |
| Doctor schedule endpoint was implemented. | FALSE |
| Migration was created. | TRUE |
| HasPendingModelChanges is false. | TRUE |
| Backend build passes. | TRUE |
| Focused tests pass. | TRUE |
