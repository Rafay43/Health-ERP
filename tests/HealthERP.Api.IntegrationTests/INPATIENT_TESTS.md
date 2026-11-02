# PostgreSQL inpatient integrity tests

Batch 5 reuses the existing `PharmacyPostgresFixture` and nonparallel collection without changing Batch 3 or Batch 4 infrastructure/tests. Set `HEALTHERP_PHARMACY_TEST_CONNECTION` explicitly to a separate loopback PostgreSQL cluster's `postgres` maintenance database. The fixture creates a random `health_erp_pharmacy_test_<GUID>` database, uses EnsureCreated there, and drops only that generated database. It never uses the ERP connection, production migrations, or normal development data.

Use the separate temporary-cluster setup in [BILLING_TESTS.md](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/BILLING_TESTS.md>), substituting `HealthERP-Inpatient-Pg-` for the temporary folder prefix and `HealthERP-Batch5` for the artifacts directory. That setup selects a free loopback port and stops its own cluster in finally. Missing test connection configuration fails explicitly; these tests are not silently skipped.

Validation from the repository root, after creating that isolated cluster:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch5"
dotnet test HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch5" --no-build --no-restore
```

The focused original-race reproductions can be run using `--filter 'FullyQualifiedName~InpatientOriginalRaceTests'`. They preserve the pre-Batch-5 handler algorithms exclusively on generated disposable records. Before product changes were applied, both reproductions passed by demonstrating duplicate active admissions on one bed/destination. These intentionally inconsistent records never reach the ERP database.

The nine protected concurrency cases send independent authenticated HTTP requests through actual inpatient/Emergency endpoints, Mediatr, scoped DbContexts, exception middleware and PostgreSQL. A test-only SaveChanges interceptor waits until both independent Serializable transactions have completed their authoritative reads and reached the write boundary. Both are then released. Tests require exactly one success and one HTTP 409, and verify fresh persisted admission/bed states, source/destination ownership, losing-operation preservation, and one active admission per patient. No EF InMemory concurrency claim or timing-dependent sleep is used.

Covered contention: same-bed admissions; same-destination transfers; admission versus transfer; same-admission double transfer; transfer versus discharge; double discharge; same-patient admissions to different beds; Emergency versus normal admission on one bed; Emergency versus normal admission of the same patient to different beds. Cross-transfers into already occupied beds separately verify both controlled rejections without mutation.

PostgreSQL triggers restricted to generated test IDs force Admission INSERT, Admission UPDATE, Bed UPDATE and Emergency triage UPDATE failures. Fresh reads verify rollback of every required state and no later leakage from EF tracking. SQLSTATE 40001 and 40P01 triggers verify safe exception translation through provider wrappers; they are error-taxonomy tests rather than artificially claiming genuine deadlocks. Cancellation is forced after SaveChanges and before commit for all three primary transitions.

A stale-context regression verifies discharge follows the bed assigned by another context, rather than an earlier cached navigation. A bed-grid regression commits a new admission immediately after the SQL reader opens: the returned grid must remain a coherent pre-commit snapshot; the next grid request must show the occupied bed and its owner together.

Authorization tests preserve actual differing policies: admit/transfer use CanManageAdmissions (Admin, Nurse, Receptionist), discharge uses CanEditPatients (Admin, Doctor, Nurse, Receptionist), reads use CanViewPatients, and bed creation remains Admin only. The test host uses the real JWT authentication extension with a test session validator; existing session-security suites separately cover account validation and revocation.

Trigger cleanup runs in finally; generated databases are dropped by the shared fixture. Stop only the temporary cluster after testing and preserve its logs if investigating a failure. Do not adapt these destructive reproductions to the ERP database.
