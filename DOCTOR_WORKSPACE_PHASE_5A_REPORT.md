# Doctor Workspace Phase 5A — Secure Clinical Note and Vitals Recording

Implemented and verified October 22, 2026.

## 1. Summary

Added the first dedicated Doctor Workspace write action: Add Clinical Note with optional vitals. The server resolves the verified Doctor, re-authorizes the Patient, validates meaningful clinical content, assigns a server timestamp and saves a durable Staff author FK. The workspace then re-fetches the secure Patient Snapshot.

Reused existing JWT/session validation, CurrentStaffService, Doctor Staff classification, appointment ownership, EF persistence, audit interceptor, TimeProvider, CQRS/MediatR, FluentValidation and the Doctor Workspace/drawer. No prescription, lab, appointment, Patient-edit or admission write actions were added.

Inspection covered the relevant ClinicalNote entity/configuration/legacy create handler, EHR and snapshot queries, Staff resolver/model, appointment status/access predicate, audit and validation/middleware conventions, timestamp/concurrency behavior, Doctor Workspace and legacy encounter form. ClinicalNote had no update handler, row-version/concurrency token or clinical soft-delete flag. This remains an append operation.

## 2. Endpoint/request contract

```http
POST /api/v1/doctor/patients/{patientId}/clinical-notes
Authorization: Bearer <current session token>
Content-Type: application/json
```

The body uses the existing model's flat field names, all optional:

```json
{
  "chiefComplaint": null,
  "diagnosis": null,
  "notes": "Recorded clinical content",
  "bloodPressure": null,
  "heartRateBpm": null,
  "temperatureCelsius": null,
  "weightKg": null
}
```

Request fields are restricted with JsonUnmappedMemberHandling.Disallow. DoctorName, AuthorName, DoctorId, StaffId, UserId, RecordedByStaffId, CreatedBy, NoteDateUtc, body PatientId and all other undeclared properties are rejected with 400 rather than silently trusted. The PatientId route identifies the resource; it is not an author selector.

201 returns only `{ noteId, recordedAtUtc }`, with Location pointing to the secure Patient Snapshot. No full entity, Patient/EHR, account/association identity or security fields are returned. Malformed binding receives a generic 400 even when development-style ThrowOnBadRequest is enabled; a safe BadHttpRequestException mapping was added to existing middleware without exposing exception text.

## 3. Doctor–Patient authorization

Every POST independently performs:

1. Existing JWT signature/lifetime/session-version validation and Doctor endpoint authorization, with no Admin-only bypass.
2. GetCurrentDoctorAsync(): live account, verified explicit Staff association, active/non-deleted Staff, live Doctor role and StaffKind.Doctor.
3. PostgreSQL EXISTS for the current Doctor StaffId, requested PatientId, non-deleted appointment and Scheduled/Confirmed/Completed status.
4. Verify that Patient exists and is not soft deleted.
5. Validate clinical input, create the attributed note and SaveChangesAsync.

The small internal DoctorPatientAccess helper now shares the exact V1 predicate between snapshot and write handlers. It accepts only an already server-resolved CurrentStaffContext, not a caller-selected Doctor ID. Past/current/future qualifying relationships retain Phase 3 semantics. Cancelled/NoShow alone do not authorize.

Having loaded a Snapshot earlier does not grant future write permission. A test loads the Snapshot, cancels the appointment in a separate context, then verifies the POST is denied without inserting a note. Admin+Doctor is limited to its own relationships.

Nonexistent, unrelated, Doctor B-only, soft-deleted Patient and deleted-only appointment cases return the same generic 404 detail: "Patient was not found." No ownership explanation or Patient existence is exposed.

## 4. Authoritative Staff attribution design

ClinicalNote now has nullable strongly typed RecordedByStaffId and optional RecordedByStaff navigation. The FK points to staff.staff.Id, using Restrict deletion. Legacy null authors are valid.

The new RecordByDoctor factory requires nonempty Patient/Staff IDs, a server Staff display name, a UTC instant and valid meaningful content. The handler supplies StaffId exclusively from GetCurrentDoctorAsync(). The browser has no input for it. No name/email/JWT StaffId matching or inferred attribution exists.

DoctorName is retained for old screens as a server-generated display snapshot on the new path. It is bounded to the legacy 200-character column when a Staff full name is longer. It is not the clinical identity and is never the secure Snapshot's author fallback. The FK preserves durable identity independently of account audit fields and display text.

## 5. Domain/model changes

- Nullable StaffId? RecordedByStaffId and optional RecordedByStaff navigation on ClinicalNote.
- Navigation/FK mapping, nullable UUID conversion, FK index and Restrict delete.
- Separate RecordByDoctor factory with meaningful-content and shared recording-rule checks.
- Existing Create factory retains its legacy DoctorName/required-Diagnosis behavior. Its constructor uses the supplied server UTC instant only for the new factory; legacy creation still uses UTC now.
- New note-only/vitals-only rows store absent complaint/diagnosis/note text as empty strings, preserving the existing non-null string schema without inventing a diagnosis. Optional vitals remain null.

No existing note or DoctorName was rewritten. No new clinical soft-delete, update, row-version, appointment-state or encounter model was added.

## 6. Migration status

Generated migration:

`20261022111010_AddClinicalNoteRecordedByStaff`

Its generated SQL contains only:

```sql
ALTER TABLE patients.clinical_notes ADD "RecordedByStaffId" uuid;
CREATE INDEX "IX_clinical_notes_RecordedByStaffId"
  ON patients.clinical_notes ("RecordedByStaffId");
ALTER TABLE patients.clinical_notes
  ADD CONSTRAINT "FK_clinical_notes_staff_RecordedByStaffId"
  FOREIGN KEY ("RecordedByStaffId") REFERENCES staff.staff ("Id") ON DELETE RESTRICT;
```

EF additionally writes its normal migration-history entry. No data backfill, DoctorName matching or unrelated schema operations exist. Designer and model snapshot were generated by EF; the snapshot was not manually edited.

Verified generated Down/Up operations against pre-existing legacy notes in the isolated PostgreSQL fixture, with transactional rollback restoring the test schema. Existing notes retain null author and unchanged legacy DoctorName. Tests verify nullable FK metadata, missing-Staff rejection, deletion restriction and no pending model changes.

**Not applied to the normal Development ERP database during this task.** No API application instance was started or restarted. The existing Development Program/initializer runs MigrateAsync() and SeedAsync() on startup; that behavior was not changed. A future restart of the updated API in Development will apply pending migrations through that existing workflow. Other environments require their normal migration deployment procedure before using the updated model.

Reviewable SQL is saved at `F:/Health ERP/artifacts/doctor-note/obj/clinical-note-attribution.sql` (generated test/build artifact).

## 7. Timestamp behavior

Recorded time comes from injected TimeProvider.GetUtcNow().UtcDateTime and is persisted in the existing NoteDateUtc UTC/timestamptz field. No browser timestamp is accepted. The domain factory rejects non-UTC instants.

CreatedAtUtc/CreatedBy continue through the unchanged AuditableEntitySaveChangesInterceptor. Its clock behavior remains unchanged. CreatedBy is the authenticated Identity account ID; RecordedByStaffId is the clinical Staff author. Tests verify these separately using a fixed recording clock and real authentication/audit services.

## 8. Validation rules

No existing model had numeric vital entry ranges, so this phase adds explicit domain entry bounds shared by the new server validator/factory, mirrored in the focused Angular form:

| Field | Rule |
|---|---|
| Entire record | At least one nonblank complaint/diagnosis/note/BP or provided numeric vital |
| Chief complaint | At most 500 trimmed characters |
| Diagnosis | At most 300 trimmed characters; optional on the new path |
| Notes | At most 2000 trimmed characters |
| Blood pressure | Two/three-digit systolic/diastolic; systolic 40–300, diastolic 20–200, systolic higher; outer whitespace trimmed |
| Heart rate | Integer 1–300 bpm |
| Temperature | 25–45 Celsius, at most two decimal places |
| Weight | Greater than 0 and no more than 500 kg, at most two decimal places |

These are application entry bounds, not normal-range claims, diagnostic interpretation or clinical advice. BP cannot contain arbitrary free text. Numeric zero is a provided value and is rejected where invalid; it is never changed to null. Whitespace-only strings do not satisfy meaningful content. Blank BP becomes null when another value makes the record meaningful.

FluentValidation validates the input after Doctor/Patient authorization, preserving the requested access-first sequence; its rules delegate to the same domain recording rules used as a factory safeguard. Normal JSON shape/type parsing still occurs before the handler. All-blank input returns 400 without writing. Note-only, complaint-only, diagnosis-only, vitals-only and mixed input are supported.

## 9. Snapshot author behavior

The secure SnapshotClinicalNoteDto adds nullable RecordedByDisplayName. It is projected through RecordedByStaff, respecting the normal Staff soft-delete filter. No StaffId or account ID is sent to the UI.

An attributed note shows "Recorded by <returned Staff name>". Legacy null attribution or an unavailable/deleted Staff relationship shows "Author unavailable". There is no DoctorName fallback. Inactive historical Staff still retain their real attribution; a current writer must separately satisfy active Doctor resolution.

Tests prove that changing the linked Staff name changes verified display through the FK, while legacy author text remains unverified. Soft-deleting that Staff makes author display null for another authorized Doctor viewing the Patient.

## 10. Angular form UX

The Snapshot drawer now offers Add Clinical Note, implemented as a focused nested Reactive Forms component. It does not reuse the legacy encounter form. All seven fields start empty: strings empty, numeric vitals null. No editable author/Staff/User/timestamp input exists.

Fields are clearly optional, units/ranges/format hints are shown, and BP/numeric feedback appears for invalid touched values. Form submission requires meaningful, valid content. Buttons are Cancel and Save Clinical Note. While pending, the form fieldset and submit/cancel controls are disabled, date controls are disabled, the drawer cannot be dismissed or closed with Escape, and parent navigation methods reject Patient/date switching.

The snapshot itself remains read-only; the dedicated new form is the sole clinical write surface. Native labels, focus outlines, status/error roles and responsive field grids use existing project conventions. No medication/lab/appointment/admission/Patient-edit action was added.

## 11. Success/refresh flow

The POST binds an immutable PatientId and sends only the seven clinical fields. It does not submit attribution or recorded time.

After a successful persisted response, the form resets and closes, a success status message appears and the parent re-fetches the current Patient through the dedicated secure Snapshot endpoint. Old snapshot data is cleared during refresh. The newly persisted note, authoritative author and latest vitals are displayed only from that response.

No local note object or fake vitals are inserted, and the page is not reloaded. If refresh fails after the successful POST, the success status remains distinct from the retryable snapshot-read error; the form does not automatically submit again.

## 12. Error/race behavior

400 gives safe entry-limit/range/format guidance plus local field feedback. 401 uses existing auth/session handling. 403 gives a safe access message. 404 says "Patient is not available." 409 gives a safe conflict message. Unexpected/network/500 failures preserve input and explain that the save could not be confirmed, asking the user to refresh state before retrying. No raw server/database detail is displayed.

Failure leaves the form and clinical values intact while the current workspace/session remains available, and re-enables retry/cancel. Duplicate submit is blocked synchronously while saving. Existing Patient/date request cancellation remains intact.

During the POST, the parent rejects Patient/date/close actions and the drawer's dismissal/escape/close icon are disabled. After success, a subsequent Patient selection cancels the earlier Patient's snapshot refresh; tests verify late Patient A refresh cannot overwrite Patient B.

This is one append and one SaveChangesAsync using existing EF transaction behavior. No explicit transaction, serializable isolation, cross-request cache or heavy lock was added. Cancellation propagates to server resolution, queries, validation and save; Angular unsubscribes on destruction.

## 13. Legacy encounter form status

The existing broad Patient clinical-note endpoint, handler, DTO and legacy Angular encounter form remain unchanged. The old form's hardcoded Doctor/vital defaults were not copied or imported into Doctor Workspace and were not cleaned up under this task.

Legacy broad APIs retain their previous authorization/attribution behavior. This task secures the dedicated Doctor write path and truthful Snapshot author display; it does not claim system-wide clinical write security is solved.

## 14. Privacy/data minimization

The new request is a clinical-only DTO with strict unmapped-field rejection. The response contains only NoteId and recorded timestamp. Durable Staff attribution remains server-side; the secure Snapshot exposes only a nullable linked Staff display name.

No Identity UserId, Patient.UserId, Staff association ID, security stamp, JWT/session data, password information, billing data or portal-link state is returned. CreatedBy remains internal audit data. Text is displayed with Angular interpolation, not HTML injection. There is no new private-notification/SignalR clinical-content broadcast.

## 15. Backend tests

Added 54 focused real PostgreSQL HTTP cases in a partial extension of DoctorPatientSnapshotTests, reusing its fixture, actual JWT/Identity/current-Doctor services and scoped audit interceptor. Coverage includes qualifying statuses and historical/future access; Cancelled/NoShow/deleted-only denial; A/B and Admin+Doctor isolation; missing Staff/kind/live-role/inactive/deleted checks; account authentication; generic Patient 404 cases; nine forbidden author/timestamp/resource fields; fifteen invalid-content scenarios; single meaningful field modes; re-authorization after a previously opened Snapshot; durable Staff FK, separate CreatedBy, fixed server time, minimal response, refreshed note/vitals/author; legacy/null and renamed/deleted authors; FK rules; generated migration legacy behavior.

The test host now enables ThrowOnBadRequest to exercise the safe strict-binding error path. The prior exact Snapshot property-set test was updated only for the intentional RecordedByDisplayName contract addition. Existing Snapshot authorization, privacy, histories and SQL-scoping tests still pass.

## 16. Angular tests

Added 30 focused clinical-note tests, alongside the 67 prior workspace/navigation tests. Coverage includes the sole new action; exact empty fields/no unsafe defaults; no editable identity/timestamp input; meaningful note/complaint/diagnosis/vitals-only cases; malformed BP and numeric range/precision rejection; exact clinical-only POST payload and selected PatientId; pending duplicate/context guards; close/reset/server-only refresh after success; retained content on 400/401/403/404/409/500; safe 404 text; recorded author only from refreshed Snapshot; stale refresh cancellation; cancellation without writing.

The prior note fixture was updated for the intentionally added nullable author field. The unrelated scaffold title test was not modified. A first run exposed a test-only DebugNode selector traversing comment nodes; it was corrected to standard By.directive selection. No production workaround was used.

## 17. Build/test results

Verified October 22, 2026:

- Backend build: PASS, 0 warnings/errors.
- Focused backend note tests: PASS, 54/54.
- Full backend suite: PASS, 475/475, no failures/skips (Domain 50, Application 39, Functional 30, Integration 356).
- HasPendingModelChanges(): false in tests and EF CLI.
- Generated attribution SQL: inspected, limited to nullable column/index/Restrict FK plus migration history.
- Angular production build: PASS; only the pre-existing SweetAlert2 CommonJS warning remains.
- Focused Doctor Workspace tests: PASS, 97/97, including all 30 new form tests.
- Full Angular suite: 168 passed, 1 failed (169 total). The sole failure remains the unrelated pre-existing app.spec.ts:22 scaffold h1/title expectation.
- Scoped diff/whitespace checks: PASS.

Backend build artifacts and the isolated PostgreSQL cluster use `F:/Health ERP/artifacts/doctor-note` because the first C:-temporary build encountered insufficient disk space. Only this task's failed C: build directory was removed after checking its exact path; no unrelated files were deleted. A successful isolated F: build replaced it. No dependency versions were changed.

Representative commands:

```powershell
dotnet build HealthERP.slnx --artifacts-path 'F:\Health ERP\artifacts\doctor-note' --no-restore
dotnet test tests/HealthERP.Api.IntegrationTests/HealthERP.Api.IntegrationTests.csproj --no-build --artifacts-path 'F:\Health ERP\artifacts\doctor-note' --filter FullyQualifiedName~RecordNote_ --logger 'trx;LogFileName=doctor-note-focused.trx'
dotnet test HealthERP.slnx --no-build --artifacts-path 'F:\Health ERP\artifacts\doctor-note' --logger 'trx;LogFileName=doctor-note-full.trx'
npm.cmd run build
npm.cmd test -- --watch=false --include='src/app/features/doctor-workspace/*.spec.ts'
npm.cmd test -- --watch=false
```

Angular commands run in health-erp-client. Backend tests use HEALTHERP_PHARMACY_TEST_CONNECTION pointing only at the isolated loopback cluster's postgres maintenance DB; the existing fixture creates/drops guarded random test databases. EF generation/model/script commands use ArtifactsPath/UseArtifactsOutput and an unreachable design-only port-1 connection, not the ERP connection. TRX results are under the existing test projects' TestResults/doctor-note-full.trx and the integration project's doctor-note-focused.trx.

After verification, zero generated test databases remained and the isolated PostgreSQL cluster was stopped successfully. The normal ERP database and running application processes were not modified.

## 18. Exact files changed

Paths relative to `F:/Health ERP`; prior local changes were preserved.

Added:

1. `src/Core/HealthERP.Domain/Patients/ClinicalNoteRecordingRules.cs`
2. `src/Core/HealthERP.Application/DoctorWorkspace/DoctorPatientAccess.cs`
3. `src/Core/HealthERP.Application/DoctorWorkspace/Commands/RecordDoctorClinicalNote.cs`
4. `src/Infrastructure/HealthERP.Infrastructure/Persistence/Migrations/20261022111010_AddClinicalNoteRecordedByStaff.cs`
5. `src/Infrastructure/HealthERP.Infrastructure/Persistence/Migrations/20261022111010_AddClinicalNoteRecordedByStaff.Designer.cs`
6. `tests/HealthERP.Api.IntegrationTests/DoctorClinicalNoteTests.cs`
7. `health-erp-client/src/app/features/doctor-workspace/clinical-note-form.ts`
8. `health-erp-client/src/app/features/doctor-workspace/clinical-note-form.html`
9. `health-erp-client/src/app/features/doctor-workspace/clinical-note-form.spec.ts`
10. `DOCTOR_WORKSPACE_PHASE_5A_REPORT.md`

Updated:

11. `src/Core/HealthERP.Domain/Patients/ClinicalNote.cs`
12. `src/Infrastructure/HealthERP.Infrastructure/Persistence/Configurations/ClinicalNoteConfiguration.cs`
13. `src/Core/HealthERP.Application/DoctorWorkspace/Queries/GetDoctorPatientSnapshotQuery.cs`
14. `src/Core/HealthERP.Application/DoctorWorkspace/Models/DoctorPatientSnapshotDto.cs`
15. `src/Presentation/HealthERP.Api/EndPoints/DoctorEndpoints.cs`
16. `src/Presentation/HealthERP.Api/Middleware/GlobalExceptionHandlingMiddleware.cs`
17. `src/Infrastructure/HealthERP.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs` — EF generated.
18. `tests/HealthERP.Api.IntegrationTests/DoctorPatientSnapshotTests.cs` — shared host/partial declaration and intentional Snapshot contract test update.
19. `health-erp-client/src/app/features/doctor-workspace/data-access/doctor-workspace.model.ts`
20. `health-erp-client/src/app/features/doctor-workspace/data-access/doctor-workspace.service.ts`
21. `health-erp-client/src/app/features/doctor-workspace/doctor-workspace.ts`
22. `health-erp-client/src/app/features/doctor-workspace/doctor-workspace.html`
23. `health-erp-client/src/app/features/doctor-workspace/doctor-workspace.spec.ts` — nullable verified-author fixture.

No legacy encounter, auth foundation, role/link management, dependency/configuration budget, billing, pharmacy or lab source files were changed by this phase. Generated SQL, build, cache, PG cluster and test-result outputs are artifacts, not additional application source changes.

## 19. Known limitations

- Existing broad Patient/EHR clinical writes and unsafe legacy defaults remain outside this secure dedicated path.
- V1 appointment-based access still includes any-date Scheduled/Confirmed/Completed relationships. There is no encounter/care-team/window model.
- Authorization and save use ordinary sequential live reads and EF save behavior. There is no new lock guaranteeing that an appointment, role or Staff association cannot change immediately after its authorization read.
- This is append/create, without idempotency keys. UI prevents duplicate pending submission, but a lost response after a committed insert can leave save outcome uncertain; blindly retrying may append a duplicate. No automatic POST retry occurs.
- Navigating away or closing the application can abort the browser request after the server has committed; cancellation is not a transaction-outcome guarantee. The server request remains bound to its immutable PatientId.
- Recorded author display follows the linked Staff's current name, not a separately versioned historical legal identity. Deleted Staff displays unavailable; the durable FK remains stored. Legacy names are never inferred into it.
- Entry bounds are explicit V1 domain rules and may require future product/clinical-policy review; the UI does not supply medical interpretation.
- Normal ERP migration application and a live manual/browser walkthrough were not performed in this task. Real PostgreSQL HTTP tests and rendered Angular tests provide verification. User must use the updated schema before exercising the new path; Development startup already applies pending migrations.

## 20. Explicitly deferred work

Prescriptions, lab ordering/results, appointment completion/cancellation/rescheduling, Patient editing, admission/transfer/discharge; globally fixing legacy clinical author trust/defaults; structured problems/allergies; care-team/cross-cover/encounter authorization; note editing/signing/amendments; versioned author identity; idempotency protocol; live clinical notifications; unrelated scaffold-test cleanup.

## 21. TRUE/FALSE checklist

| Statement | Result |
|---|---|
| Dedicated Doctor clinical-note write endpoint exists. | TRUE |
| Browser supplies DoctorId/StaffId/UserId for attribution. | FALSE |
| Current Doctor is resolved server-side. | TRUE |
| Patient access is re-authorized on every write. | TRUE |
| Cancelled-only appointment grants write access. | FALSE |
| NoShow-only appointment grants write access. | FALSE |
| Durable Staff attribution is persisted. | TRUE |
| Attribution is based on DoctorName string matching. | FALSE |
| Legacy rows require fake backfill. | FALSE |
| Timestamp is browser controlled. | FALSE |
| Empty clinical note can be created. | FALSE |
| Vitals-only note can be created. | TRUE |
| Note-only note can be created. | TRUE |
| Vitals have validation. | TRUE |
| Normal/default vitals are prefilled. | FALSE |
| Snapshot refreshes from server after success. | TRUE |
| Verified Snapshot author comes from authoritative Staff relationship. | TRUE |
| Legacy DoctorName is treated as verified author. | FALSE |
| Prescribe Medication was added. | FALSE |
| Lab Order was added. | FALSE |
| Appointment completion was added. | FALSE |
| Billing data is exposed. | FALSE |
| Identity UserId is exposed. | FALSE |
| Migration was required. | TRUE |
| Migration was applied to normal Development ERP DB. | FALSE |
| HasPendingModelChanges is false. | TRUE |
| Backend build passes. | TRUE |
| Focused backend tests pass. | TRUE — 54/54 |
| Angular production build passes. | TRUE |
| Focused Angular tests pass. | TRUE — 30 new; 97 total Workspace |
