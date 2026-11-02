# Doctor Workspace Phase 2 — Secure Personal Schedule Backend

Implementation and verification: October 22, 2026.

## 1. Summary

Added a dedicated, read-only Doctor schedule API using the existing MediatR architecture, verified Staff association, live current-Doctor resolver and JWT session validation. Ownership is resolved exclusively on the server. The existing shared appointment query, booking/status handlers, completion billing, association management and Angular application were not changed by this phase.

Before editing, inspected the Appointment entity/status/DTO/query, endpoint, EF configuration and migration indexes, Patient and Staff relationships, booking and completion handlers, browser timestamp serialization, CurrentStaffService, authorization registration and real PostgreSQL integration fixture. Appointment timestamps are PostgreSQL `timestamp with time zone`; booking clients serialize UTC instants. No existing facility timezone or injectable clock abstraction was found.

## 2. Endpoint

```http
GET /api/v1/doctor/schedule
GET /api/v1/doctor/schedule?date=2026-10-22
Authorization: Bearer <current session token>
```

The sole business request parameter is optional `date` (`DateOnly`). Neither the endpoint binding nor `GetDoctorScheduleQuery` accepts StaffId, DoctorId or UserId. Extra ownership query-string parameters are ignored by ASP.NET binding and cannot select a different owner; a real HTTP test verifies this explicitly. No request body or caller-selected timezone is used.

Responses: 200 schedule; 400 invalid/unrepresentable calendar date; 401 unauthenticated, inactive/locked account or revoked session; 403 unauthorized role or ineligible verified Staff association. Cancellation flows from the HTTP request through MediatR and EF reads.

## 3. Authorization flow

Existing JWT signature, issuer, audience, lifetime and session-version validation runs first. Endpoint authorization explicitly requires an authenticated principal with the Doctor role, using an endpoint policy without an Admin bypass. The handler independently invokes `ICurrentStaffService.GetCurrentDoctorAsync()`.

Admin-only is denied. Admin+Doctor receives only its own eligible verified Doctor schedule. A stale Doctor claim cannot substitute for live Doctor membership: this is tested with a valid session whose live account has only Nurse membership.

## 4. Current Doctor resolution flow

The unchanged resolver validates the authenticated Identity ID, live account activity/lockout, exactly one explicit Staff.UserId association including hidden/deleted records, active/non-deleted Staff, defined StaffKind, live Doctor Identity membership and StaffKind.Doctor. Failure closes access before the appointment query runs.

The handler uses the returned typed StaffId directly in its appointment predicate. It separately projects display name, specialty and department from that Staff row, respecting soft deletion and activity. No email/name matching, role assignment, association inference, JWT Staff claims or caller-supplied ownership IDs were added. Identity UserId is absent from the response.

## 5. Date/timezone semantics

Added the explicit configuration:

```json
"Facility": { "TimeZoneId": "Asia/Karachi" }
```

Production uses this configured facility timezone, independently of browser timezone and server-machine local timezone. Deployments can override it through normal configuration, for example `Facility__TimeZoneId`. Missing or invalid configuration fails at startup; there is no implicit server-local or UTC fallback. The checked-in facility choice is Asia/Karachi and should be configured to the actual facility if deployed elsewhere.

Added the small `FacilityTimeZone` calendar helper and registered `TimeProvider.System`. The handler captures one `clock.GetUtcNow()` instant after resolving the Doctor. Omitted date is that instant's calendar date in the facility timezone. An explicit date uses the same authoritative timezone.

Local midnight and the following local midnight are converted independently into UTC. The query uses `[utcStart, utcEnd)`, based on appointment start time. For October 22, 2026 in Asia/Karachi this is:

```text
2026-10-21T19:00:00Z <= StartDateTimeUtc < 2026-10-22T19:00:00Z
```

The response includes the selected Date and configured TimeZoneId; appointment timestamps remain UTC. DST tests demonstrate independently converted 23-hour and 25-hour days in America/New_York. Rare zones/dates with ambiguous or nonexistent local midnight receive an explicit 400 instead of a guessed offset. DateOnly.MaxValue receives 400 because its next day cannot be represented.

## 6. Database query/scoping

The read uses AsNoTracking and projects only required columns, with this SQL-translated predicate before materialization:

```text
StaffId == currentDoctor.StaffId
AND IsDeleted == false
AND StartDateTimeUtc >= utcStart
AND StartDateTimeUtc < utcEnd
```

Appointments are ordered by StartDateTimeUtc ascending, then AppointmentId ascending. PostgreSQL applies the ownership/date/soft-delete filters and ordering. SQL interception tests verify ownership and both date comparisons are present and unnecessary Patient Email and appointment Notes are absent from the schedule SQL.

A left join to the normally filtered Patients query retrieves minimal identity. A soft-deleted Patient does not silently remove its real appointment: PatientId stays the persisted foreign key, while display name/MRN become null. No deleted Patient identity is exposed and no placeholder name is fabricated. This case is covered by a PostgreSQL HTTP test.

## 7. Response contract

```text
DoctorScheduleDto
  Date
  TimeZoneId
  Doctor
    DisplayName
    Specialty
    Department
  Summary
    Total
    Scheduled
    Confirmed
    Completed
    Cancelled
    NoShow
  NextScheduledConsultation: appointment item or null
  Appointments: appointment items

Appointment item
  AppointmentId
  PatientId
  PatientMedicalRecordNumber: nullable
  PatientDisplayName: nullable
  StartDateTimeUtc
  EndDateTimeUtc
  Status: persisted enum name
  Reason
```

Standard API camelCase JSON serialization applies. No entities are serialized. Doctor display information comes from Staff, rather than the Identity account's potentially different display name. Appointment notes are deliberately omitted from this schedule surface.

## 8. Summary count definitions

Total is the number of returned appointments. Each other count is the number of those same items with the corresponding real AppointmentStatus: Scheduled=1, Confirmed=2, Completed=3, Cancelled=4 or NoShow=5. Counts are calculated from the single materialized scoped list, so they agree with the response list. No Waiting, CheckedIn or InProgress status was introduced.

An empty day returns all six counts as zero, an empty Appointments array and null NextScheduledConsultation. No fallback counts or historical replacement exists.

## 9. Next Scheduled Consultation definition

The first item in the deterministic chronological list whose start is at or after the captured current UTC instant and whose status is Scheduled or Confirmed. Completed, Cancelled and NoShow never qualify. An active appointment whose start is already in the past also does not qualify, even if its end is in the future.

This definition applies to the selected date. A future selected date can have a next scheduled consultation; a past selected date has none. No qualifying item means null, without consulting another day or another Doctor. No queue, waiting or check-in meaning is implied.

## 10. Patient data minimization

Patient projection contains only persisted PatientId, MRN and display name needed to identify the consultation. The appointment's persisted reason is included. Date of birth, gender, contact details, Patient.UserId, clinical notes, prescriptions, labs, admissions, invoices and portal information are absent. Tests assert exact top-level, Doctor and appointment JSON property sets.

## 11. Query/index review

The existing schema has B-tree indexes `IX_appointments_StaffId` and `IX_appointments_PatientId`, plus the Appointment primary key. The StaffId index reasonably supports V1 equality scoping to one Doctor; the database then applies the selected-day range and sorts the matching rows. There is no existing composite StaffId/start-time index.

No production size or execution-plan evidence justifies schema churn in this phase. No index/configuration/model changes were made. For a Doctor with a large historical appointment volume, measure the real query with EXPLAIN ANALYZE before considering a partial composite index on `(StaffId, StartDateTimeUtc, Id) WHERE IsDeleted = false`. Current V1 does not claim optimal performance for an unbounded historical volume.

## 12. Tests added

New `DoctorScheduleTests.cs`: 33 focused test cases, using the existing Pharmacy PostgreSQL collection and guarded random test database fixture. HTTP tests use the actual Doctor endpoint, MediatR handler, CurrentStaffService, JwtSessionValidator, Identity EF stores and existing JWT authentication. There is no mocked ownership resolver or in-memory persistence.

Coverage includes:

- Doctor A/B independent isolation; attempted ownership query-string override; query contract accepts only Date; captured SQL scoping/projection.
- Unlinked Doctor; Unspecified Staff; inactive/deleted Staff; inactive/locked Identity account; anonymous/Admin/Nurse/Patient denial; stale Doctor claim with no live Doctor role; own-only Admin+Doctor.
- Empty dates with real zeros and no historical fallback; all five actual status counts; total; stable chronological/tied ordering.
- Scheduled and Confirmed at the current instant qualify; past active items do not; each terminal status is excluded; no future eligible item means null.
- Previous/next day exclusion; inclusive local midnight/exclusive next midnight; appointment soft deletion; explicit previous/future selected dates; omitted date on a different UTC calendar date through a fixed TimeProvider.
- Minimal response properties, real MRN/display name, no sensitive extras; soft-deleted Patient identity remains unavailable without losing the appointment.
- Invalid dates return 400; Asia/Karachi UTC window; 23/25-hour DST conversion; HasPendingModelChanges is false.

The first test run identified a missing UserManager registration in the new test harness. The harness was corrected to use real Identity EF stores; production prerequisite code was unchanged. Final focused tests all pass.

## 13. Test/build results

Verified October 22, 2026 with isolated build artifacts under `%TEMP%/HealthERP-DoctorSchedule-20261022`:

- Backend build: PASS, 0 warnings, 0 errors.
- Focused Doctor schedule tests: PASS, 33/33, 0 failed/skipped.
- Full backend suite: PASS, 368/368, 0 failed/skipped: Domain 50, Application 39, Functional 30, Integration 249 (including the 33 new cases).
- EF CLI `migrations has-pending-model-changes`: PASS, no changes since the last migration. The pre-existing six EF required-Patient/global-filter warnings remain.
- `Database.HasPendingModelChanges()` assertion: PASS.
- Scoped `git diff --check`: PASS.
- Angular files were not changed and Angular checks were not rerun for this backend-only phase.

Commands:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-DoctorSchedule-20261022" --no-restore
dotnet test tests/HealthERP.Api.IntegrationTests/HealthERP.Api.IntegrationTests.csproj --no-build --artifacts-path "$env:TEMP\HealthERP-DoctorSchedule-20261022" --filter FullyQualifiedName~DoctorScheduleTests --logger 'trx;LogFileName=doctor-schedule-focused.trx'
dotnet test HealthERP.slnx --no-build --artifacts-path "$env:TEMP\HealthERP-DoctorSchedule-20261022" --logger 'trx;LogFileName=doctor-schedule-full.trx'
```

Test commands set `HEALTHERP_PHARMACY_TEST_CONNECTION` to the isolated loopback cluster's postgres maintenance database. The fixture creates and drops only randomly named test databases. EF CLI used `ArtifactsPath`/`UseArtifactsOutput=true` and a design-only connection on port 1, avoiding the ERP database. Initial sandboxed restore/network and PostgreSQL process restrictions were resolved using approved elevated commands; no dependency versions were changed.

After verification, the isolated cluster contained zero generated test databases and was stopped successfully. The normal ERP database and running application processes were not modified.

## 14. Exact files changed

All paths below are relative to `F:/Health ERP`; pre-existing worktree changes were preserved.

Added:

1. `src/Core/HealthERP.Application/Common/Configurations/FacilityTimeZone.cs`
2. `src/Core/HealthERP.Application/DoctorWorkspace/Models/DoctorScheduleDto.cs`
3. `src/Core/HealthERP.Application/DoctorWorkspace/Queries/GetDoctorScheduleQuery.cs`
4. `src/Presentation/HealthERP.Api/EndPoints/DoctorEndpoints.cs`
5. `tests/HealthERP.Api.IntegrationTests/DoctorScheduleTests.cs`
6. `DOCTOR_WORKSPACE_PHASE_2_REPORT.md`

Updated:

7. `src/Infrastructure/HealthERP.Infrastructure/DependencyInjection.cs` — facility calendar and TimeProvider registrations only.
8. `src/Presentation/HealthERP.Api/Program.cs` — map the new Doctor endpoints.
9. `src/Presentation/HealthERP.Api/appsettings.json` — explicit Facility.TimeZoneId setting.

Generated TRX evidence is under each existing test project's `TestResults/doctor-schedule-full.trx`; focused evidence is `tests/HealthERP.Api.IntegrationTests/TestResults/doctor-schedule-focused.trx`. These are test outputs, not additional application source changes.

## 15. Migration/model status

No new migration, entity change, EF configuration change, snapshot update, backfill or application-database migration was introduced by Phase 2. Both direct model assertion and EF CLI report no pending model changes. The existing Phase 1 verified Staff association is reused.

## 16. Known limitations

- The unchanged shared ERP appointment and Patient APIs retain their previous access semantics. This endpoint establishes a secure personal schedule; it does not close broader access paths elsewhere in the application.
- No clinical Patient-access relationship is established by receiving an appointment item. Minimal PatientId/MRN/name are not authorization to open EHR or perform clinical actions.
- The selected-day list is not paginated. Existing StaffId indexing is adequate for V1 scoping but should be measured as historical volume grows.
- Missing/deleted Patient identity is represented by null name/MRN, preserving appointment integrity without fabricated or deleted identity data.
- Facility timezone is one authoritative deployment setting, not a per-facility multi-tenant directory. Midnight DST ambiguity/gaps are explicitly unsupported (400); ordinary DST transitions are converted correctly.
- Existing resolver/reads remain normal live reads, not a new serialized transaction spanning all account, Staff and appointment reads. No stronger cross-request/concurrent-change guarantee was introduced.
- No browser/manual deployed-environment testing was performed; this is an API implementation verified through real PostgreSQL HTTP tests.

## 17. Explicitly deferred work

Doctor Workspace frontend, routes/login redirects/navigation; Patient Snapshot authorization; full EHR access; clinical notes, prescriptions, lab orders/results and admissions; completing/cancelling/rescheduling appointments; check-in/waiting states; shared appointment access redesign; existing completion billing changes; dashboard fabricated-data cleanup; production-volume index optimization.

## 18. TRUE/FALSE checklist

Schedule access statements below refer to the new dedicated endpoint. Existing shared APIs are explicitly outside this change.

| Statement | Result |
|---|---|
| Dedicated Doctor schedule endpoint exists. | TRUE |
| Existing general appointment query semantics were changed. | FALSE |
| Browser supplies StaffId/DoctorId for ownership. | FALSE |
| Current Doctor is resolved server-side. | TRUE |
| Doctor A can see Doctor B appointments. | FALSE |
| Doctor role without verified Staff association is allowed. | FALSE |
| Admin-only can access a Doctor personal schedule. | FALSE |
| Admin+Doctor is limited to its own verified schedule. | TRUE |
| Date filtering happens in the database. | TRUE |
| Facility timezone behavior is explicit. | TRUE |
| Appointment list is chronologically ordered. | TRUE |
| Counts come only from real scoped appointments. | TRUE |
| Legitimate zero counts remain zero. | TRUE |
| Waiting/CheckedIn/InProgress status was invented. | FALSE |
| Next Scheduled Consultation uses only eligible future appointments. | TRUE — includes start equal to now |
| Historical fallback appointment is used when none exists. | FALSE |
| Full Patient EHR is exposed. | FALSE |
| Billing data is exposed. | FALSE |
| Clinical write actions were added. | FALSE |
| Fake/default dashboard data was reused. | FALSE |
| New migration was created. | FALSE |
| HasPendingModelChanges is false. | TRUE |
| Backend build passes. | TRUE |
| Focused tests pass. | TRUE — 33/33 |
