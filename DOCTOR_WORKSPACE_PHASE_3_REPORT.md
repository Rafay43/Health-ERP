# Doctor Workspace Phase 3 — Secure Patient Access and Read-only Snapshot

Implementation and verification: October 22, 2026.

## 1. Summary

Added a dedicated read-only Patient Snapshot query and endpoint for verified Doctors. Access is established in PostgreSQL before loading Patient identity or clinical data. The response uses explicit projections with bounded histories, nullable missing observations and no unverified author fields.

Reused the existing verified Identity/Staff association, StaffKind.Doctor, GetCurrentDoctorAsync(), session validation, MediatR registration, appointment ownership, EF context and Phase 2 TimeProvider. The personal schedule endpoint remains intact. No Angular, shared Patient API, clinical write or schema changes were made.

Inspection was limited to the relevant Patient/EHR DTOs and query, Appointment/status/schedule, CurrentStaffService, Patient authorization policies, facility calendar/clock, clinical entities/configurations and their indexes/soft-delete semantics.

## 2. Endpoint

```http
GET /api/v1/doctor/patients/{patientId}/snapshot
Authorization: Bearer <current session token>
```

PatientId identifies the requested resource. It is the only field in GetDoctorPatientSnapshotQuery. The endpoint accepts no DoctorId, StaffId or UserId for authorization. Extra ownership query-string values are ignored by normal binding and cannot change ownership; an HTTP test attempts this override and is denied. Malformed Guid binding returns 400. A valid but unavailable PatientId returns the safe 404 described below.

## 3. V1 Doctor–Patient access rule

At least one persisted appointment must satisfy all of:

```text
StaffId == current verified Doctor StaffId
PatientId == requested PatientId
IsDeleted == false
Status IN (Scheduled, Confirmed, Completed)
```

Past, current and future appointments qualify equally; there is no date window or dependency on today's schedule. Cancelled and NoShow appointments do not grant access. If a separate qualifying appointment exists, a cancelled/no-show record does not negate that qualifying relationship.

Name, email, specialty, department, legacy clinical author strings, MRN, browser ownership IDs and hypothetical JWT Staff claims confer no access. This deliberately limited V1 rule is not a richer care-team, cross-cover or encounter authorization model.

## 4. Authorization sequence

1. Existing JWT signature/lifetime/issuer/audience and live session-version validation.
2. Endpoint requires an authenticated Doctor role, with no Admin-only bypass.
3. Handler independently calls ICurrentStaffService.GetCurrentDoctorAsync(). The unchanged resolver verifies the live account, explicit association, active/non-deleted Staff, live Doctor membership and StaffKind.Doctor.
4. SQL EXISTS verifies the qualifying appointment relationship using the server-resolved typed StaffId and requested PatientId.
5. Only after success, project the non-deleted Patient's identity. A missing/deleted Patient stops processing here.
6. Load the small Patient-scoped clinical projections.

Admin+Doctor still needs its own qualifying Doctor relationship. Invalid Staff associations fail with 403; absent/invalid/revoked or inactive/locked sessions fail through existing authentication behavior. Cancellation propagates from HTTP through MediatR to the EF queries.

No new authorization service was necessary for this single read use case: the narrow relationship predicate is explicit in the dedicated handler, immediately after current-Doctor resolution. It has no caller-selected Doctor identity and is tested through the real endpoint and SQL. No generic authorization framework or duplicated prerequisite resolver was introduced.

## 5. Information-disclosure behavior

Nonexistent Patient, existing Patient without a qualifying relationship, Doctor B-only Patient, and soft-deleted Patient all receive:

```text
HTTP 404
title: Resource Not Found
detail: Patient snapshot was not found.
```

The handler uses the same generic KeyNotFoundException with the existing ProblemDetails middleware. No reason, Patient existence, appointment or ownership information is returned. ProblemDetails.instance contains the request path supplied by the caller, as elsewhere in the API; it does not reveal database existence. Tests compare status/title/detail for nonexistent and unauthorized existing Patients. This does not claim timing indistinguishability.

## 6. Snapshot contract

```text
DoctorPatientSnapshotDto
  Patient
    PatientId, MedicalRecordNumber, DisplayName, DateOfBirth, Gender?
  LatestVitals?
    BloodPressure?, HeartRateBpm?, TemperatureCelsius?, WeightKg?, RecordedAtUtc
  RecentNotes[]
    NoteId, RecordedAtUtc, ChiefComplaint, Diagnosis, Notes
  RecentPrescriptions[]
    PrescriptionId, PrescribedDateUtc, MedicationName, Dosage, Frequency,
    DurationDays, Refills, Instructions?, Status, DispensedDateUtc?
  RecentLabOrders[]
    LabOrderId, OrderedDateUtc, TestName, TestCode, Priority, Status, Result?
      Result: CompletedDateUtc?, Value?, ReferenceRange?, Unit?, Remarks?, IsAbnormal
  CurrentAdmission?
    AdmissionId, AdmissionDateUtc, ChiefComplaint
```

Normal API camelCase JSON applies. DTOs contain no entities or open-ended Details object. Nullable fields remain nullable and collections are returned as arrays, including when empty.

## 7. Clinical data included

Patient identity is read from persisted Patient fields. Vitals come from the most recent single ClinicalNote with at least one numeric observation or a nonblank BP, ordered by NoteDateUtc descending then NoteId descending. All fields and the recorded timestamp come from that same note; partial observations are not merged with older notes. This query is separate from the latest-five notes so older recorded vitals remain available even when more than five newer notes contain none.

Recent notes contain persisted complaint, diagnosis and note text. Recent prescriptions report the real Pending/Dispensed/Cancelled status and prescription details; the section is deliberately not called Active Medications. Prescription duration and dispensing status are not proof of current medication use.

Recent lab orders are ordered by OrderedDateUtc and show the real Ordered/InProgress/Completed/Cancelled status. Result is null unless the persisted status is Completed. Completed records project actual result values, reference range, unit, remarks, completion time and stored abnormal flag without generating interpretations. The abnormal flag is persisted data, not a new medical interpretation.

CurrentAdmission is the latest persisted admission with IsDischarged=false, DischargeDateUtc=null and AdmissionDateUtc at or before the injected clock's current UTC instant. No discharged, future-dated or contradictory discharge-date record is substituted as current. No history fallback is used. The DTO omits rates/charges and unverified admitting Doctor text. This reflects recorded open admission state, not an independent physical-location verification.

## 8. Clinical data deliberately omitted

No contact email/phone, Patient.UserId, portal-link state, Identity or Staff/account information, billing/invoices/payments/room charges, complete timeline, full EHR, bed directory, discharge history, catalog entities or clinical author/audit identities are returned. Allergies, blood group, problems and chronic-condition lists are absent because no authoritative structured workflow/model was found. Free text is not parsed into invented structured facts.

## 9. Missing-data behavior

No recorded vitals means LatestVitals=null. Blank BP alone does not count as an observation. Partial observations preserve nulls; numeric zero is a persisted value and remains zero. There are no normal/default vitals or fallback clinical summaries.

No history yields empty arrays. No qualifying open admission yields CurrentAdmission=null. Pending/cancelled lab orders do not receive fabricated completed results. Stored nullable values remain null. The endpoint cannot determine whether a historical writer incorrectly persisted frontend defaults; it reports actual recorded values without supplying any new defaults.

## 10. Author-attribution handling

ClinicalNote.DoctorName, Prescription.DoctorName, LabOrder.OrderingDoctorName, LabOrder.PerformedBy and Admission.AdmittingDoctorName are omitted from projections and the response. Browser-supplied legacy display text is not treated as verified attribution. CreatedBy/ModifiedBy are also not promoted into clinical authorship. No author field is supplied and no attribution write workflow was changed.

Persisted note/diagnosis/instructions/remarks remain ordinary clinical text; the endpoint neither generates author statements nor parses or rewrites free text that a user previously recorded.

## 11. Query/data-minimization design

All reads use AsNoTracking and explicit Select projections, with no Include graph. The authorization EXISTS runs before any Patient identity or clinical query. The Patient query respects the global soft-delete filter and explicitly requires !IsDeleted; Appointment authorization respects its global filter and explicitly requires !IsDeleted.

ClinicalNote, Prescription, LabOrder and Admission currently have no IsDeleted property or soft-delete query filter. The snapshot preserves those existing persistence semantics and does not invent a deleted-record flag. A model test records this fact. Any future clinical soft-delete model/filter will require corresponding verification.

Each subsequent query filters PatientId in SQL. No unbounded history is downloaded or trimmed in memory. SQL capture verifies query order, EXISTS ownership/status/deletion predicates, parameter identities, PatientId scoping, ordering/limits and omission of private/author columns. Failed authorization produces no Patient or clinical reads. The response exposes no author, contact, linkage or financial fields even when real linked Patient and invoice records exist in the database.

The handler performs seven small reads after Doctor resolution: relationship, Patient identity, latest vitals, recent notes, recent prescriptions, recent labs and current admission. Queries are sequential on the existing scoped EF context.

## 12. Bounded-history limits

| Section | Limit | SQL order |
|---|---:|---|
| LatestVitals | 1 qualifying note | NoteDateUtc DESC, Id DESC |
| RecentNotes | 5 | NoteDateUtc DESC, Id DESC |
| RecentPrescriptions | 5 | PrescribedDateUtc DESC, Id DESC |
| RecentLabOrders | 5 | OrderedDateUtc DESC, Id DESC |
| CurrentAdmission | 1 qualifying open record | AdmissionDateUtc DESC, Id DESC |

Ordering and Take/FirstOrDefault are applied in PostgreSQL. IDs provide deterministic ties. PostgreSQL UUID ordering is used; tests compare its lexical ordering for tied timestamps. These are recent records, not exhaustive histories; lab recency means order time, not completion time.

## 13. Index/performance review

The existing appointment schema has StaffId and PatientId foreign-key indexes. Clinical notes, prescriptions, lab orders and admissions all have PatientId indexes, including EF convention-generated FK indexes confirmed in the model snapshot. Clinical notes additionally have a NoteDateUtc index; other status/priority/IsDischarged indexes remain unchanged.

These indexes reasonably support V1's selective per-Doctor/per-Patient EXISTS and per-Patient recent reads. There is no production execution-plan/volume evidence requiring a new composite index in this phase. No migration or index change was added. Large per-Patient histories should be measured with EXPLAIN ANALYZE before considering PatientId/date/Id composite indexes; high appointment volumes may warrant a measured StaffId/PatientId partial composite index. This implementation does not claim benchmarked production-volume performance.

## 14. Tests added

Added DoctorPatientSnapshotTests.cs with 53 focused cases using the existing guarded real PostgreSQL fixture. HTTP hosts use actual Doctor endpoints, MediatR, CurrentStaffService, JwtSessionValidator, Identity EF stores and JWT authentication. Ownership and persistence are not mocked.

Coverage:

- All three qualifying statuses across past/current/future dates (nine cases); Cancelled/NoShow denial; soft-deleted appointment denial; A/B independent access and attempted ownership overrides.
- Unrelated/nonexistent safe responses; legacy matching author text does not authorize; unlinked/Unspecified/inactive/deleted Staff; inactive/locked account; anonymous/Admin/Nurse/Patient denial; stale Doctor claim; revoked session; own-only Admin+Doctor.
- Real identity; deleted Patient unavailable; null/empty missing data; partial latest vitals from outside the recent-five notes; all four individual vital types including persisted numeric zeros.
- Five-item newest-first limits and tied ordering for all three histories; real prescription and lab status semantics; completed results only; no historical/future/inconsistent admission substitution; real open admission.
- Unrelated Patient history exclusion; exact property sets for every nested DTO; no untrusted author/contact/billing/linkage fields even with populated records.
- SQL predicates include server-resolved Doctor StaffId, requested PatientId, !IsDeleted and allowed statuses; SQL order proves authorization precedes clinical reads; all histories use SQL ordering/limits and minimal projections.
- Malformed PatientId binding; no pending model changes; existing absence of clinical soft-delete flags.

There is no soft-deleted-note test because this domain has no note soft-delete state; no unrelated schema behavior was invented to simulate one.

## 15. Test/build results

Verification uses isolated artifacts under `%TEMP%/HealthERP-DoctorSnapshot-20261022`, avoiding DLL locks on a running ERP API.

- Backend build: PASS, 0 warnings, 0 errors.
- Focused Phase 3 tests: PASS, 53 passed, 0 failed/skipped.
- Full backend suite: PASS, 421/421, 0 failed/skipped: Domain 50, Application 39, Functional 30, Integration 302 (including all 53 new cases).
- Direct Database.HasPendingModelChanges assertion: PASS (false).
- EF CLI model check: PASS, no changes since the last migration. The six existing required-Patient/global-query-filter warnings remain; no new model warning was introduced.
- Scoped diff/whitespace checks: PASS.
- Angular was untouched; Angular checks were not run.

Commands:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-DoctorSnapshot-20261022" --no-restore
dotnet test tests/HealthERP.Api.IntegrationTests/HealthERP.Api.IntegrationTests.csproj --no-build --artifacts-path "$env:TEMP\HealthERP-DoctorSnapshot-20261022" --filter FullyQualifiedName~DoctorPatientSnapshotTests --logger 'trx;LogFileName=doctor-snapshot-focused.trx'
dotnet test HealthERP.slnx --no-build --artifacts-path "$env:TEMP\HealthERP-DoctorSnapshot-20261022" --logger 'trx;LogFileName=doctor-snapshot-full.trx'
dotnet ef migrations has-pending-model-changes --project src/Infrastructure/HealthERP.Infrastructure --startup-project src/Presentation/HealthERP.Api --no-build
```

Test commands set HEALTHERP_PHARMACY_TEST_CONNECTION to this task's isolated loopback PostgreSQL cluster's postgres maintenance database. The existing fixture creates/drops only random test databases. EF CLI sets ArtifactsPath/UseArtifactsOutput and a design-only port-1 connection so it cannot connect to the ERP database. No dependency versions were changed.

After verification, zero generated test databases remained and the temporary cluster was stopped successfully. The normal ERP database and running application processes were not modified.

## 16. Exact files changed

Paths relative to `F:/Health ERP`:

Added:

1. `src/Core/HealthERP.Application/DoctorWorkspace/Models/DoctorPatientSnapshotDto.cs`
2. `src/Core/HealthERP.Application/DoctorWorkspace/Queries/GetDoctorPatientSnapshotQuery.cs`
3. `tests/HealthERP.Api.IntegrationTests/DoctorPatientSnapshotTests.cs`
4. `DOCTOR_WORKSPACE_PHASE_3_REPORT.md`

Updated:

5. `src/Presentation/HealthERP.Api/EndPoints/DoctorEndpoints.cs` — append the snapshot endpoint; retain the Phase 2 schedule route and behavior.

Generated test evidence is under the existing test projects' TestResults/doctor-snapshot-full.trx and the integration project's TestResults/doctor-snapshot-focused.trx. These are generated outputs, not additional source changes. All prior local changes were preserved.

## 17. Migration/model status

No entity, EF configuration, snapshot, migration or index changes. No backfill, seeding or normal ERP database writes. No new migration was required; the direct EF model assertion is false for pending changes.

## 18. Known limitations

The existing shared Patient/EHR and other ERP APIs retain their previous authorization semantics. This phase secures the dedicated snapshot; it does not solve system-wide Patient ownership or prevent use of older broad endpoints.

V1 intentionally allows read access through any qualifying past or future appointment, without an expiry window, encounter, care team or cross-cover policy. Once authorized, the snapshot is a bounded view of that Patient's records, including records from other clinicians; it is not restricted by unverified author strings.

Recorded clinical data may contain legacy inaccuracies, defaults or user-entered text; this read path does not validate or repair past clinical writes. Recent Prescriptions is not proof of active medication use. CurrentAdmission means the persisted open state; stale open flags or inconsistent legacy duplicate open records require separate data-integrity handling. If multiple records satisfy the open-state predicate, the most recent deterministic record is returned.

These remain sequential live reads without a new serialized transaction spanning authentication, relationship and clinical queries. Concurrent changes may occur after the authorization check; no stronger snapshot or immediate-revocation atomicity guarantee was introduced. This task adds no access-window framework or optimistic/locking model changes.

Timestamps remain recorded UTC instants. There is no date filter in V1 relationship authorization, so facility-local day conversion is intentionally unnecessary here; Phase 2 facility timezone behavior remains unchanged. TimeProvider is reused only to exclude future admissions from the current admission section.

Verification is through real PostgreSQL HTTP tests, not deployed browser/manual testing or production-scale benchmarks.

## 19. Explicitly deferred work

Doctor Workspace Angular UI/routes/navigation; clinical note, prescription and lab write attribution/security; Patient editing; appointment completion/cancellation/rescheduling; admission/transfer/discharge actions; structured allergies/problem lists; care-team/cross-cover/encounter authorization; configurable historical access windows; globally tightening existing broad Patient APIs; exhaustive EHR exports and production-volume index tuning.

## 20. TRUE/FALSE checklist

Access claims below refer to this dedicated Doctor Snapshot endpoint.

| Statement | Result |
|---|---|
| Dedicated Doctor Patient Snapshot endpoint exists. | TRUE |
| Browser supplies Doctor/Staff identity for authorization. | FALSE |
| Current Doctor is resolved server-side. | TRUE |
| A qualifying Doctor–Patient appointment is required. | TRUE |
| Scheduled appointment grants V1 access. | TRUE |
| Confirmed appointment grants V1 access. | TRUE |
| Completed appointment grants V1 access. | TRUE |
| Cancelled appointment grants access. | FALSE |
| NoShow appointment grants access. | FALSE |
| Doctor A can access Doctor B-only Patient. | FALSE |
| Admin-only can access Doctor Snapshot. | FALSE |
| Admin+Doctor is limited to its own relationships. | TRUE |
| Nonexistent and unauthorized Patient are outwardly distinguishable. | FALSE — same status/title/detail |
| Full EHR is returned. | FALSE |
| Billing data is returned. | FALSE |
| Identity UserId is returned. | FALSE |
| Patient Portal linkage is returned. | FALSE |
| Fake allergies/problem list were added. | FALSE |
| Missing vitals receive normal/default values. | FALSE |
| Browser-supplied DoctorName is presented as verified attribution. | FALSE |
| Histories are bounded. | TRUE |
| Clinical write actions were added. | FALSE |
| Existing broad Patient APIs were globally redesigned. | FALSE |
| New migration was created. | FALSE |
| HasPendingModelChanges is false. | TRUE |
| Backend build passes. | TRUE |
| Focused tests pass. | TRUE — 53/53 |
