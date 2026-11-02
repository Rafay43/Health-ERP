# Stabilization Batch 5 — Inpatient bed/admission/transfer integrity

Completed 12 October 2026. Backend build: **PASS**, zero warnings/errors. Backend tests: **223 passed, 0 failed, 0 skipped**. Angular build: **PASS**. Angular tests: **51 passed**, one unchanged scaffold failure. Migration required: **NO**. Pending EF model changes: **NO**. No manual/browser testing was performed by Codex. Work stops at Batch 5; no Phase 7 work was started.

## 1. Original bed/admission architecture

Application handlers use IApplicationDbContext directly; there is no separate inpatient repository. Admit checked patient existence, absence of an active admission, then loaded a bed, called Occupy, added Admission and saved once. Transfer loaded Admission with its patient/bed, checked destination, released source, occupied destination, updated Admission and saved once. Discharge loaded Admission/bed, discharged the admission, released the bed and saved once.

Emergency's TransferToInpatient handler was an additional bed-acquisition writer: it occupied a bed, created Admission, changed triage disposition and saved once, without the ordinary admission handler's active-patient check. All four paths needed the same integrity boundary. Bed creation has its own existing name check and unique location index; no update/delete/maintenance endpoint was found.

## 2. Confirmed race-condition root cause

Authoritative reads happened before EF's implicit SaveChanges transaction, without concurrency tokens or explicit protection. Two admissions could both read Available and create two active Admission rows for one BedId. Two transfers could both read an available destination, release both source beds, and set both admissions to that destination. The bed's final Occupied flag could conceal duplicate active owners. Same-admission stale reads could also overwrite the current BedId or leave a separately acquired destination occupied.

Before product changes, two disposable PostgreSQL reproductions demonstrated these exact old algorithms: two admissions on one bed, and two transfers on one destination with both sources released. These intentionally invalid test records exist only in the fixture's disposable database. The old algorithm is retained solely in reproduction tests.

Individual SaveChanges calls already made each request's writes atomic, but did not protect the earlier decision reads. `Bed.Occupy()` also rejected only Occupied, permitting Maintenance despite its handler comment claiming Available-only validation.

## 3. Bed model discovered

Bed has typed GUID ID, WardName, RoomNumber, BedNumber, BedType, decimal DailyRate, persisted BedStatus and audit fields. It does not store PatientId, AdmissionId, active/inactive flags or reservations. The grid derives current occupant from active Admission rows. The location tuple has an existing unique index; Status is indexed. DailyRate is numeric(18,2).

## 4. Admission model discovered

Admission has typed GUID ID, required PatientId/BedId foreign keys, doctor name, complaint, UTC admission date, optional discharge date/summary, decimal daily-rate snapshot/room charge, IsDischarged and audit fields. BedId is mandatory, including during an active admission. Discharged records retain historical BedId. Patient and bed deletes are restricted by their FKs.

There is no admission-status enum or dedicated transfer-history table. Transfer changes BedId/rate snapshot and appends a dated reason to ChiefComplaint. These existing accounting/history semantics were preserved. No new ownership column or concurrency token was added.

## 5. Actual bed-status semantics

Only Available=1, Occupied=2, Maintenance=3 exist. “Cleaning” appears in DTO/frontend comments but is not an actual enum status; no such state was invented. Available is assignable only if no active admission already owns the bed. Occupied and Maintenance are rejected. Domain Occupy now requires Available explicitly. There is no separate inactive/out-of-service/reservation workflow to implement.

Release remains Available. MarkMaintenance exists as a domain method but has no current API handler; no maintenance-management redesign was included.

## 6. Active-admission semantics

Active means `!IsDischarged`. The existing ordinary admission handler expressly intended one active admission per patient. That rule now applies safely across ordinary and Emergency admission. Admission cannot temporarily have no bed. A discharged admission cannot transfer or discharge again; repeat discharge is a controlled conflict rather than a second release.

## 7. Integrity invariants enforced

All protected bed-acquisition writers check authoritative status and active-owner absence inside the transaction. Admission must have no existing active inpatient admission for the patient. Transfer/discharge require an active admission whose source bed is Occupied with no other active owner. An inconsistent source is rejected rather than releasing another patient's bed or inventing a repair.

Admission creates its owner and occupies its bed together. Transfer releases source, acquires destination and moves Admission together. Discharge marks inactive and releases its current bed together. Known rejection/rollback preserves the previous rows, and tracked failed changes are cleared. Historical corruption/direct SQL is not repaired; see section 36.

## 8. Chosen concurrency strategy

One explicit PostgreSQL **Serializable transaction** begins before any authoritative read for admit, transfer, discharge and Emergency admission. All validations and mutations occur in that transaction, with one SaveChanges and commit. There is no automatic retry of a hospital action.

The strategy protects both existing-row conflicts and absence predicates: no current owner of a destination bed, and no current active admission for a patient. Competing transactions reading the same original state cannot both commit incompatible transitions. Losing serialization/deadlock/concurrency failures become safe HTTP 409 responses.

## 9. Why this strategy fits this architecture

The inpatient transition spans Admission plus one or two beds, and sometimes triage disposition. Patient admission exclusivity also depends on absence of another row, rather than a scalar counter. Serializable covers that topology without introducing a patient/bed ownership lock protocol, xmin migration, advisory locks, or a conditional-update accounting subsystem.

Billing's valid concurrent additive payments should both accumulate; inpatient actions competing from one original ownership state should conflict. This choice follows those different semantics, rather than copying an earlier batch mechanically. Explicit row locking would need a consistent shared admission/patient/bed ordering across every acquisition path and additional handling of phantom admission rows. Current schema/indexes support Serializable without migration.

## 10. Lock/transaction ordering

There are no application `SELECT FOR UPDATE` locks or separately ordered multirow lock acquisitions. Each operation begins Serializable before its reads, validates authoritative entities/ownership, mutates, saves once and commits. Ordinary admit reads patient, patient-active predicate, destination and destination ownership. Transfer reads Admission, source and destination, verifies source/destination ownership and patient, then changes all three. Discharge reads Admission, source ownership and patient, then changes both. Emergency reads disposition and patient, performs the same acquisition checks, and changes triage in the same unit.

PostgreSQL takes normal predicate/read/write locks for this design. No EF-generated physical write ordering is assumed to eliminate deadlocks. Cross-transfers into occupied beds reject both before mutation. A test confirms that outcome. Remaining physical deadlocks safely abort an entire transaction as described next.

## 11. Deadlock handling

The service traverses provider exception chains, including InvalidOperationException wrappers around DbUpdateException. PostgreSQL SQLSTATE 40001/40P01 and EF concurrency exceptions map to a safe inpatient conflict, after rollback and tracker clearing. The API exposes no SQL, lock details, token values or stack trace. There is no blind retry.

Actual required races run against PostgreSQL. Separate triggers raising 40001/40P01 verify provider-wrapper error translation; those taxonomy tests do not claim to manufacture a genuine deadlock. No shared Pharmacy/Billing exception logic was changed.

## 12. Admission atomicity

Available bed acquisition and Admission insertion share one transaction/save. Patient existence, active admission absence, destination existence/status and active ownership are checked before mutation. Failed insert or bed update leaves no Admission and the bed unchanged, proven with fresh reads. Emergency acquisition also commits its triage disposition in the same transaction; forced triage UPDATE failure rolls back the admission and bed too.

## 13. Transfer atomicity

Source release, destination acquisition, BedId/rate/history update commit together. Rejected destinations leave the original source/admission unchanged. Failure of required Admission or Bed writes rolls back every row. No half-transfer or orphaned acquired destination survives a losing transaction.

## 14. Discharge atomicity

Admission.Discharge and release of the authoritative current bed commit together. Existing discharge date/summary and minimum-one-day room-charge calculation remain. Required write failures preserve the active admission and occupied bed. A reused-context regression verifies discharge uses the current bed assigned by another context.

Cached admission/bed entities are replaced with database snapshots, including EF relationship tracking state. A test initially exposed stale navigation restoration when only scalars were copied; replacing the tracked entry corrected it. A second discharge is rejected before bed release, including after that bed has been assigned to another patient.

## 15. Same-bed admission protection

Two independent authenticated requests read the original available bed and reach the write boundary together. Exactly one returns **201**, one **409**. Fresh PostgreSQL reads show one active owner and Occupied status; the losing patient's attempted admission did not persist.

## 16. Same-destination transfer protection

Two active admissions on different source beds compete for one available destination. Exactly one returns **200**, one **409**. Winner's source is Available and its Admission points to Occupied destination. Loser's original source remains Occupied and its Admission remains there. Tests inspect all rows after contention.

## 17. Admit-versus-transfer protection

Ordinary admission and existing-admission transfer use the same Serializable acquisition checks. Exactly one can acquire a shared destination; the losing new admission creates no record, or losing transfer preserves its source and Admission. Emergency versus normal admission on a shared bed is tested too.

## 18. Same-admission double-transfer protection

Two transfers reading the same original admission cannot both commit different destination acquisitions. Deterministically synchronized PostgreSQL HTTP tests require one **200** and one **409**, one occupied destination, the other Available, and source Available with the admission pointing only to the winner.

This is protection of conflicting original database snapshots. The existing API has no expected-source/revision or idempotency field. A request whose first authoritative read occurs after an earlier transfer committed is a subsequent transition from the new state; it can legitimately succeed. No new stale-client intent/version protocol was invented. Manual simultaneous fetches encourage overlap; the automated barrier provides deterministic same-original-state contention.

## 19. Transfer-versus-discharge protection

When both read the same active original admission, one commits and one conflicts. Transfer winner leaves active Admission on Occupied destination and Available source. Discharge winner leaves discharged Admission, Available source and unacquired destination Available. Fresh assertions rule out orphan occupancy or an active admission pointing to an available bed.

A discharge that first reads after a completed transfer may legitimately discharge the admission from its new bed; the current bed is always loaded server-side. Such sequential operations remain coherent.

## 20. Double-discharge protection

Concurrent original-state discharges produce one **200** and one **409** and one persisted inactive transition. Repeat discharge is also rejected sequentially. The former bed remains Available unless legitimately reassigned; in that case the new owner's Occupied bed is preserved. No other patient's bed is released.

## 21. Same-patient active-admission protection

Applicable: this rule already existed. Serializable protects the active-admission absence predicate when one patient targets two different beds. Tests require one **201**, one **409**, exactly one active admission and an unchanged Available losing bed. Emergency versus ordinary admission of the same patient to different beds is also tested, closing the prior alternate-entry bypass.

## 22. Error/status-code behavior

| Outcome | HTTP |
|---|---:|
| Ordinary admission created | 201 |
| Transfer/discharge completed | 200 with AdmissionDto |
| Emergency admission completed | 200 with admissionId |
| Invalid domain input, e.g. missing doctor | 400 |
| Missing patient, bed, Admission or triage record | 404 |
| Nonavailable bed, active owner/patient, resolved admission, same bed, inconsistent ownership | 409 |
| Serialization/deadlock/EF concurrency failure | 409 |
| Unexpected storage failure | 500 with safe refresh guidance |
| No authentication / insufficient endpoint role | 401 / 403 |

HTTP 500 says “Unable to confirm” and asks to refresh beds/admissions. A lost commit acknowledgment can accompany a completed atomic operation; an error response alone does not prove the old state persisted. Validation/business rejection and confirmed rollback do preserve it. No automatic retry was introduced.

## 23. Angular behavior changes

Only existing inpatient transition paths and Emergency bed acquisition were adjusted. Admit/transfer/discharge now guard duplicate pending calls; their templates already disabled submit buttons. Success reloads backend beds/admissions. Failure shows the safe message, reloads both, and never fabricates occupancy or automatically retries. Admission reload replaces selected transfer/discharge snapshots.

Emergency transfer similarly guards pending calls and displays failures while reloading queue and beds. Its other intake/bay/discharge UI logic is untouched. No role predicates, routing, templates or layout were redesigned.

The bed grid now queries beds, active owners and patient details in **one SQL statement**. The former two queries could straddle a commit and mix old status with new ownership. A real PostgreSQL test commits admission after the grid reader opens: that response remains a coherent pre-commit view, and the next response shows Occupied and its owner together. Duplicate active owners encountered by the grid produce a controlled reconciliation conflict rather than inventing an occupant.

## 24. Authorization behavior

Production policies are unchanged:

| Endpoint | Policy | Existing backend roles |
|---|---|---|
| Admit, transfer, Emergency-to-Inpatient | CanManageAdmissions | Admin, Nurse, Receptionist |
| Inpatient discharge | CanEditPatients | Admin, Doctor, Nurse, Receptionist |
| Beds/admissions reads | CanViewPatients | Admin, Doctor, Nurse, Receptionist |
| Bed creation | CanManageStaff | Admin |

Patient cannot mutate staff inpatient data. Tests cover authorized roles, Doctor's existing admit/transfer rejection, Patient rejection, unauthenticated requests, differing discharge policy and Admin-only bed creation. The test host uses the existing JWT extension with a test session validator; the unchanged security suites independently test actual revocation.

Existing UI mismatches remain: Nurse can see Add Bed but backend requires Admin; Doctor can see Admit/Transfer but backend denies those; Receptionist is backend-authorized but excluded by those inpatient UI predicates; Nurse/Receptionist can discharge server-side while that UI predicate shows only Admin/Doctor. These decisions were documented, not changed.

## 25. Files changed in this batch

Only Batch 5 changes are listed; prior workspace work was preserved.

| File | Purpose |
|---|---|
| [IInpatientTransitionService.cs](<F:/Health ERP/src/Core/HealthERP.Application/Common/Interfaces/IInpatientTransitionService.cs>) | Protected operation contract |
| [InpatientConflictException.cs](<F:/Health ERP/src/Core/HealthERP.Application/Inpatient/InpatientConflictException.cs>) | Safe conflict |
| [InpatientStorageException.cs](<F:/Health ERP/src/Core/HealthERP.Application/Inpatient/InpatientStorageException.cs>) | Safe storage classification |
| [AdmitPatientCommand.cs](<F:/Health ERP/src/Core/HealthERP.Application/Inpatient/Commands/AdmitPatientCommand.cs>) | Delegate to transaction service |
| [TransferBedCommand.cs](<F:/Health ERP/src/Core/HealthERP.Application/Inpatient/Commands/TransferBedCommand.cs>) | Delegate to transaction service |
| [DischargePatientCommand.cs](<F:/Health ERP/src/Core/HealthERP.Application/Inpatient/Commands/DischargePatientCommand.cs>) | Delegate to transaction service |
| [TransferToInpatientCommand.cs](<F:/Health ERP/src/Core/HealthERP.Application/Triage/Commands/TransferToInpatientCommand.cs>) | Protect alternate bed-acquisition writer |
| [GetBedGridQuery.cs](<F:/Health ERP/src/Core/HealthERP.Application/Inpatient/Queries/GetBedGridQuery.cs>) | Coherent grid snapshot |
| [Bed.cs](<F:/Health ERP/src/Core/HealthERP.Domain/Inpatient/Bed.cs>) | Available-only occupancy |
| [InpatientTransitionService.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/Services/InpatientTransitionService.cs>) | Serializable operations and cleanup |
| [DependencyInjection.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/DependencyInjection.cs>) | Scoped service registration |
| [GlobalExceptionHandlingMiddleware.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Middleware/GlobalExceptionHandlingMiddleware.cs>) | Inpatient-specific safe responses |
| [InpatientEndpoints.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/EndPoints/InpatientEndpoints.cs>) | Cancellation propagation and response metadata |
| [EmergencyEndpoints.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/EndPoints/EmergencyEndpoints.cs>) | Same for inpatient acquisition only |
| [inpatient-list.ts](<F:/Health ERP/health-erp-client/src/app/features/inpatient/inpatient-list.ts>) | Busy guards and authoritative reloads |
| [emergency-list.ts](<F:/Health ERP/health-erp-client/src/app/features/emergency/emergency-list.ts>) | Inpatient transfer guard/error reload |
| [InpatientOriginalRaceTests.cs](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/InpatientOriginalRaceTests.cs>) | Two old-race reproductions |
| [InpatientTransitionTests.cs](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/InpatientTransitionTests.cs>) | 59 PostgreSQL cases |
| [BedAvailabilityTests.cs](<F:/Health ERP/tests/HealthERP.Domain.UnitTests/Inpatient/BedAvailabilityTests.cs>) | Maintenance rejection |
| [inpatient-integrity.spec.ts](<F:/Health ERP/health-erp-client/src/app/features/inpatient/inpatient-integrity.spec.ts>) | Ten UI cases |
| [emergency-inpatient-transfer.spec.ts](<F:/Health ERP/health-erp-client/src/app/features/emergency/emergency-inpatient-transfer.spec.ts>) | Two UI cases |
| [INPATIENT_TESTS.md](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/INPATIENT_TESTS.md>) | Safe test setup and methodology |
| [This report](<F:/Health ERP/STABILIZATION_BATCH_5_REPORT.md>) | Findings/results/manual verification |

No inpatient mappings, migration, model snapshot, Admission accounting rules, production authorization policies, or completed Batch 1–4 logic were changed. Shared DI/middleware additions are limited to the new inpatient service and response types.

## 26. Tests added/changed

Added **62 backend cases**: 59 PostgreSQL transition/read/authorization/model cases, two original-race reproductions and one domain test. Coverage includes normal/occupied/Maintenance admissions; existing active patient and inconsistent available flag; normal/rejected transfers; normal/repeated discharge and reassigned-bed protection; missing entities; invalid doctor; nine races; cross-transfer rejection; failure of either required write for all three transitions; provider error translation; post-save cancellation; Emergency disposition rollback; stale EF relationships; coherent bed grid; role boundaries and model changes.

Added **12 Angular cases**: three operations' pending duplicate protection, authoritative success reload, conflict refresh without retry; uncertain failure; Emergency pending/conflict and success reload. Existing tests were not deleted or weakened.

## 27. PostgreSQL concurrency-test methodology

Reused the guarded Batch 3 fixture/nonparallel collection unchanged, including its existing environment variable. A separate PostgreSQL 17 cluster in a temporary directory listened only on loopback port **53666**. The fixture created random disposable databases and dropped only its own generated databases. It never used the ERP database, production seeds/migrations or the running development API.

Before implementation, original-flow tests synchronized two independent DbContexts after reads and before saves, proving duplicate owners. Protected races use actual authenticated HTTP endpoints/Mediatr/scoped DbContexts with a test-only SaveChanges barrier after both read/mutate their original snapshots. PostgreSQL performs real Serializable conflict detection; assertions require one success/409 and inspect fresh rows. No EF InMemory concurrency claim or production test hook.

Failure triggers are restricted to generated test IDs and removed in finally. Cancellation occurs after writes but before commit. Bed-grid concurrent-read injection uses a separate writer after the SQL reader opens. Temporary cluster shutdown and empty disposable database confirmation occur after validation. Reproducible setup is in INPATIENT_TESTS.md.

## 28. Backend build result

**PASS**, zero warnings/errors:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch5-20261012"
```

Final rebuild with `--no-restore` also passed after the grid snapshot change. The isolated output folder avoids touching DLLs held by a running development API. NuGet restore used approved network access. An initial reproduction-test tuple declaration was corrected before the pre-fix tests ran successfully.

## 29. Backend test result

**223 passed, 0 failed, 0 skipped.**

| Project | Passed |
|---|---:|
| Domain unit | 41 |
| Application unit | 39 |
| API functional | 30 |
| API integration | 113 |

The first full run exposed the stale-context relationship issue; it was fixed and the complete suite rerun. The final run includes the coherent-grid test. All existing notification/SignalR, account/JWT/session, Pharmacy PostgreSQL, Billing PostgreSQL and Patient Portal ownership/contract tests passed.

```powershell
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = 'Host=127.0.0.1;Port=53666;Database=postgres;Username=health_erp_tests'
dotnet test HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch5-20261012" --no-build --no-restore --logger "trx;LogFilePrefix=Batch5Complete"
```

Final TRX artifacts are `Batch5Complete_net10.0_20261012*.trx` in the four test projects' TestResults folders. Port 53666 belongs to the temporary cluster stopped after validation; create a separate cluster using the documented setup before rerunning.

## 30. Angular build result

**PASS:** `npm.cmd run build` from `F:\Health ERP\health-erp-client`. The existing SweetAlert2 CommonJS optimization warning remains. No build error.

## 31. Angular test result

`npm.cmd test -- --watch=false`: **51 passed, 1 failed, 52 total**, across eight files. All 12 new UI cases passed, along with prior billing/pharmacy/notification/auth tests. The unchanged scaffold failure is `src/app/app.spec.ts:22`, expecting an absent scaffold `<h1>` greeting. It was not modified.

## 32. Migration required

**NO.** Existing rows, FKs, bed identity, active flags and BedId/PatientId indexes support Serializable validation and atomic transitions. No ownership column, concurrency token, partial unique index, CHECK constraint or other schema modification is required for these protected workflows. No migration was created.

Partial unique active BedId/PatientId indexes could provide future defense against arbitrary bypass writers/direct SQL, but are not necessary for this transaction-level fix. They would need a separate approved migration and historical-data compatibility review; no production-data audit or constraint rollout was attempted here.

## 33. Pending EF model changes

**NO.** `Database.HasPendingModelChanges()` passed as false in the new PostgreSQL inpatient test and existing Pharmacy/Billing tests. Domain guard/service/query changes do not alter mapped properties. Model snapshot unchanged in this batch.

## 34. Performance implications

Transactions cover only short database reads and one save/commit; no external calls occur inside. Serializable predicate tracking and contention can abort competing operations with 409. Existing patient/bed indexes support ownership checks; PostgreSQL may use broader predicate locks on small tables or particular plans and reject otherwise independent transactions conservatively. No automatic retry hides those decisions.

The bed grid now uses one SQL statement instead of two and does not load every active admission separately before filtering wards. No table-wide application mutex, distributed transaction or new index was added. EF stale-entry replacement is limited to the target entities, rather than clearing unrelated tracked state on successful operations.

## 35. Exact manual verification steps

Restart the development API with this code when ready. Use **Admin** for all steps so the existing UI/backend mismatches do not interfere. Use two development test patients with no active inpatient admissions. The existing HTTP API launch profile is `http://localhost:5268`; the frontend's `/api` proxy supports the browser helper below. No manual/browser testing is claimed in this report.

Exact current routes/payloads:

| Action | Method/route | Payload |
|---|---|---|
| Create bed | POST `/api/v1/inpatient/beds` | `{"wardName":"Batch5-Test","roomNumber":"1","bedNumber":"A","bedType":"General","dailyRate":100}` |
| Admit | POST `/api/v1/inpatient/admissions` | `{"patientId":"PATIENT_GUID","bedId":"BED_GUID","admittingDoctorName":"Test Doctor","chiefComplaint":"Batch 5 manual test"}` |
| Transfer | POST `/api/v1/inpatient/admissions/ADMISSION_GUID/transfer` | `{"newBedId":"DESTINATION_GUID","transferReason":"Batch 5 manual test"}` |
| Discharge | POST `/api/v1/inpatient/admissions/ADMISSION_GUID/discharge` | `{"dischargeSummary":"Batch 5 test complete"}` |
| Inspect beds | GET `/api/v1/inpatient/beds?wardName=Batch5-Test` | None |
| Inspect all/history admissions | GET `/api/v1/inpatient/admissions?activeOnly=false` | None; optional patientId filter |
| Inspect active admissions | GET `/api/v1/inpatient/admissions?activeOnly=true` | None; optional patientId filter |

Emergency acquisition remains POST `/api/v1/emergency/triage/TRIAGE_GUID/transfer-inpatient`, payload `{"bedId":"BED_GUID","admittingDoctorName":"Test Doctor","admissionNotes":"Batch 5 manual test"}`, returning 200 with admissionId. The 11 required checks below focus on the primary inpatient endpoints.

Paste this helper once into the frontend browser console while signed in. It creates a unique test ward, retains IDs of admissions it created, prints no token, and does not retry mutations. Replace the two patient GUIDs:

```javascript
var batch5 = (() => {
  const ward = 'Batch5-' + crypto.randomUUID().slice(0, 8);
  const beds = new Set();
  const createdAdmissions = new Set();
  async function api(path, method = 'GET', payload) {
    const token = localStorage.getItem('health_erp_token');
    if (!token) throw new Error('Sign in as Admin first.');
    const response = await fetch('/api/v1/inpatient' + path, {
      method, headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
      body: payload === undefined ? undefined : JSON.stringify(payload)
    });
    const text = await response.text();
    return { status: response.status, body: text ? JSON.parse(text) : null };
  }
  async function bed(label) {
    const result = await api('/beds', 'POST', {
      wardName: ward, roomNumber: '1', bedNumber: label + '-' + crypto.randomUUID().slice(0, 8),
      bedType: 'General', dailyRate: 100
    });
    if (result.status !== 201) throw new Error(JSON.stringify(result));
    beds.add(result.body.id); return result.body.id;
  }
  async function admit(patientId, bedId) {
    const result = await api('/admissions', 'POST', {
      patientId, bedId, admittingDoctorName: 'Test Doctor', chiefComplaint: 'Batch 5 manual test'
    });
    if (result.status === 201) createdAdmissions.add(result.body.id);
    return result;
  }
  async function seed(patientId, bedId) {
    const result = await admit(patientId, bedId);
    if (result.status !== 201) throw new Error(JSON.stringify(result));
    return result.body.id;
  }
  function transfer(id, destination) {
    return api('/admissions/' + encodeURIComponent(id) + '/transfer', 'POST', {
      newBedId: destination, transferReason: 'Batch 5 manual test'
    });
  }
  function discharge(id) {
    return api('/admissions/' + encodeURIComponent(id) + '/discharge', 'POST', {
      dischargeSummary: 'Batch 5 test complete'
    });
  }
  async function state() {
    const results = await Promise.all([
      api('/beds?wardName=' + encodeURIComponent(ward)), api('/admissions?activeOnly=false')
    ]);
    if (results.some(r => r.status !== 200)) throw new Error(JSON.stringify(results));
    return { ward, beds: results[0].body.filter(b => beds.has(b.id)),
      admissions: results[1].body.filter(a => beds.has(a.bedId) || createdAdmissions.has(a.id)) };
  }
  async function cleanup() {
    const result = await api('/admissions?activeOnly=true');
    if (result.status !== 200) throw new Error(JSON.stringify(result));
    const outcomes = [];
    for (const admission of result.body.filter(a => createdAdmissions.has(a.id))) {
      const discharged = await discharge(admission.id);
      outcomes.push({ id: admission.id, ...discharged });
      if (discharged.status !== 200) throw new Error('Inspect current state before further cleanup: ' + JSON.stringify(discharged));
    }
    return outcomes;
  }
  return { ward, bed, admit, seed, transfer, discharge, state, cleanup };
})();
var batch5PatientA = 'REPLACE_TEST_PATIENT_A_GUID';
var batch5PatientB = 'REPLACE_TEST_PATIENT_B_GUID';
```

Run each statement individually so its response is visible. Use `await batch5.state()` after all requests for each test finish. Inspect Bed.status/currentAdmissionId and Admission.bedId/isDischarged/dischargeDateUtc. Occupied beds must have exactly one matching active admission; Available beds must have none. After inspection, reload the Angular Inpatient grid and admission list and compare those same records. There should be no optimistic local acquisition or success notification for a rejected operation.

**Manual Test 1 — Normal admission (201).**

```javascript
var b5A = await batch5.bed('T1-A');
var b5AdmissionA = await batch5.seed(batch5PatientA, b5A);
await batch5.state();
```

Expect A Occupied, one active Admission for Patient A on A, no discharge date. Angular shows that bed and patient. Keep this admission for tests 2–5.

**Manual Test 2 — Occupied bed (409).**

```javascript
await batch5.admit(batch5PatientB, b5A);
await batch5.state();
```

Expect conflict, no admission for Patient B on A, original admission/bed unchanged. Angular should show a meaningful rejection and refresh. Do not discharge the winner yet.

**Manual Test 3 — Normal transfer (200).**

```javascript
var b5B = await batch5.bed('T3-B');
await batch5.transfer(b5AdmissionA, b5B);
await batch5.state();
```

Expect A Available, B Occupied, the same active Admission now on B. Angular grid/list reflect source release and destination ownership.

**Manual Test 4 — Transfer to occupied destination (409).**

```javascript
var b5C = await batch5.bed('T4-C');
var b5AdmissionB = await batch5.seed(batch5PatientB, b5C);
await batch5.transfer(b5AdmissionA, b5C);
await batch5.state();
```

Expect Patient A still on occupied B; Patient B still on occupied C; no transfer/bed mutation. Angular conflict must refresh those states without success.

**Manual Test 5 — Normal discharge (200).**

```javascript
await batch5.discharge(b5AdmissionA);
await batch5.state();
await batch5.cleanup(); // After inspection, discharge the separately created Patient B test admission.
```

Expect Patient A's Admission discharged with date/summary, historical BedId B retained, B Available. Angular active-only list removes it; unfiltered history retains it. Cleanup frees Patient B for the next tests.

**Manual Test 6 — Same-bed concurrent admission (201 + 409, either order).**

```javascript
var b5X6 = await batch5.bed('T6-X');
await Promise.all([batch5.admit(batch5PatientA, b5X6), batch5.admit(batch5PatientB, b5X6)]);
await batch5.state();
await batch5.cleanup(); // Only after inspecting the winner and absence of a losing admission.
```

Expect X Occupied with one active admission, losing patient without an accidental admission to X. Angular shows only the winner. Cleanup discharges that created winner.

**Manual Test 7 — Same-destination transfers (200 + 409, either order).**

```javascript
var b5A7 = await batch5.bed('T7-A'), b5B7 = await batch5.bed('T7-B'), b5X7 = await batch5.bed('T7-X');
var b5AA7 = await batch5.seed(batch5PatientA, b5A7), b5AB7 = await batch5.seed(batch5PatientB, b5B7);
await Promise.all([batch5.transfer(b5AA7, b5X7), batch5.transfer(b5AB7, b5X7)]);
await batch5.state();
await batch5.cleanup();
```

Expect winner's source Available and admission on Occupied X. Loser stays active on its Occupied original source. Angular must show both correctly. Cleanup discharges both created admissions from their current beds.

**Manual Test 8 — Admit versus transfer (200 + 409 or 409 + 201).**

```javascript
var b5A8 = await batch5.bed('T8-A'), b5X8 = await batch5.bed('T8-X');
var b5AA8 = await batch5.seed(batch5PatientA, b5A8);
await Promise.all([batch5.transfer(b5AA8, b5X8), batch5.admit(batch5PatientB, b5X8)]);
await batch5.state();
await batch5.cleanup();
```

Expect one active owner on X. A losing transfer preserves A; a losing admission creates no row. Angular shows only the acquired destination owner and correct original source. Cleanup releases created active admissions.

**Manual Test 9 — Same-admission double transfer (one same-original-state 200 and one 409).**

```javascript
var b5A9 = await batch5.bed('T9-A'), b5B9 = await batch5.bed('T9-B'), b5C9 = await batch5.bed('T9-C');
var b5AA9 = await batch5.seed(batch5PatientA, b5A9);
await Promise.all([batch5.transfer(b5AA9, b5B9), batch5.transfer(b5AA9, b5C9)]);
await batch5.state();
await batch5.cleanup();
```

Expect one active current destination, source Available, and unowned destination Available. Angular must not show both destinations occupied. If the browser requests serialize before their first reads, both 200 responses can represent successive moves from different states; final ownership must still be coherent. The synchronized automated test proves exactly one transition from the same original snapshot. No expected-source/revision field exists in this API.

**Manual Test 10 — Transfer versus discharge (same-snapshot 200 + 409; coherent final state).**

```javascript
var b5A10 = await batch5.bed('T10-A'), b5B10 = await batch5.bed('T10-B');
var b5AA10 = await batch5.seed(batch5PatientA, b5A10);
await Promise.all([batch5.transfer(b5AA10, b5B10), batch5.discharge(b5AA10)]);
await batch5.state();
await batch5.cleanup();
```

Transfer winner: active admission on Occupied B, A Available. Discharge winner: inactive admission, A/B Available. Sequential transfer followed by a discharge reading the new state can also both succeed, ending discharged with both beds Available. No orphaned Occupied bed is allowed. Angular grid/list reflect that final state. Cleanup acts only if the created admission remains active.

**Manual Test 11 — Double discharge (200 + 409, either order).**

```javascript
var b5A11 = await batch5.bed('T11-A');
var b5AA11 = await batch5.seed(batch5PatientA, b5A11);
await Promise.all([batch5.discharge(b5AA11), batch5.discharge(b5AA11)]);
await batch5.state();
await batch5.cleanup();
```

Expect one discharged Admission with date/summary, A Available, no unrelated bed mutation. Angular active list removes it, history retains it. Repeat discharge cannot release a later occupant.

Cleanup deliberately uses only existing discharge endpoints for IDs created by the helper. No bed/admission DELETE route exists; created beds and historical test admissions remain in their uniquely named test ward. Do not invent deletion endpoints or run raw destructive cleanup. If an operation's outcome is uncertain, inspect state before any deliberate retry or cleanup. Do not run subsequent cases until both test patients have no active admission.

## 36. Limitations intentionally left for later

Serializable protects current workflows and their original-state conflicts, not arbitrary SQL or future writers bypassing the service. Existing duplicate ownership/inconsistent historical data is not repaired; acquisition/source checks reject relevant inconsistencies and the grid reports duplicate owners for reconciliation. No ERP data was audited or altered by automated tests.

There is no new request idempotency/expected-revision protocol. Sequential actions reading successive states remain permitted; browser arrival timing alone does not establish overlapping database snapshots. A commit/HTTP acknowledgment can be lost, so refresh and reconcile before retry. The bed-grid response is one coherent SQL snapshot, but separate bed/admission UI requests can observe different times while other actions are still running; inspect final state after those actions settle.

Existing transfer rate/room-charge accounting and appended complaint history were preserved, including existing field-length/storage limitations. Emergency inpatient acquisition is protected; broader Emergency bay/discharge concurrency is outside this batch. Bed-name creation rules, inactive/reservation/cleaning features, legacy-data constraints and role-policy/UI mismatches were not redesigned.

No Pharmacy, Billing, notification/SignalR, JWT/session, DOB, patient pagination, dashboard, activation, BillingStaff landing, reporting/insurance placeholder, unrelated authorization, UI redesign, broad refactor, unrelated technical-debt or Phase 7 work was included. Completed Batch 1–4 tests and implementations were preserved. Stop here.

## Explicit TRUE/FALSE confirmations

TRUE integrity statements concern the protected workflows from a valid starting state. As with Batch 4, an error response after an unknown commit is different from a confirmed rejected/rolled-back operation.

| Statement | TRUE/FALSE | Evidence/qualification |
|---|---|---|
| One bed cannot have two active admissions through protected workflows | TRUE | Serializable shared acquisition; all entry paths covered |
| Concurrent same-bed admissions cannot both succeed | TRUE | PostgreSQL 201/409 and one active owner |
| Concurrent transfers to one destination cannot both succeed | TRUE | PostgreSQL 200/409; losing source preserved |
| Admission creation and bed acquisition are atomic | TRUE | One transaction/save/commit, both write-failure tests |
| Transfer source release, destination acquisition and Admission update are atomic | TRUE | Three-state rollback and race assertions |
| Discharge and bed release are atomic | TRUE | Two-state rollback/cancellation assertions |
| Failed admission leaves bed unchanged | FALSE as an unconditional transport claim | TRUE for rejection/confirmed rollback; lost acknowledgment may accompany a complete atomic admission |
| Failed transfer leaves source/destination/admission unchanged | FALSE as an unconditional transport claim | TRUE for rejection/confirmed rollback; same unknown-commit limit |
| Failed discharge leaves admission/bed consistent | TRUE | Either pre-operation or atomically completed state; no half-discharge |
| Same admission cannot successfully double-transfer from the same original state | TRUE | Same-snapshot PostgreSQL test; subsequent-state transitions remain legitimate |
| Transfer/discharge contention cannot leave contradictory state | TRUE | Fresh coherent-outcome assertions and stale-context regression |
| Double discharge cannot corrupt bed state | TRUE | Race, repeat and later-occupant tests |
| Backend is authoritative for bed availability | TRUE | Transaction-protected reads; frontend filtering is UX only |
| Patient cannot perform staff inpatient mutations | TRUE | Actual HTTP 403 and unchanged state |
| Existing inpatient authorization was not broadened | TRUE | Policy declarations/role predicates unchanged |
| Batch 1 notification security remains intact | TRUE | Preserved suites passed |
| Batch 2 JWT/session security remains intact | TRUE | Preserved suites passed |
| Batch 3 Pharmacy integrity remains intact | TRUE | All existing PostgreSQL cases passed |
| Batch 4 Billing integrity remains intact | TRUE | All existing PostgreSQL cases passed |
| Patient Portal ownership remains intact | TRUE | Preserved ownership/contracts passed |
| No unrelated authorization mismatch was fixed | TRUE | Mismatches documented only |
| No Phase 7 work was started | TRUE | Batch 5 scope only |
| Unrelated audit findings were not fixed | TRUE | Only directly related bed/admission integrity paths changed |
