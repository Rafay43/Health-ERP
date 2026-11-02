# PostgreSQL pharmacy tests

These tests use real PostgreSQL transactions and independent HTTP requests/DbContexts. They do not use EF InMemory for concurrency. The repository previously had no database integration fixture; this fixture requires explicit configuration instead of falling back to the ERP database.

Set `HEALTHERP_PHARMACY_TEST_CONNECTION` to a **separate local test PostgreSQL cluster** maintenance connection (`Database=postgres`). The fixture rejects non-loopback hosts and non-maintenance database names. It creates an empty `health_erp_pharmacy_test_<random GUID>` database, initializes the current EF model with EnsureCreated, and drops only that generated database after the tests. It never runs production migrations or seeds. No pharmacy model/schema changes are part of this batch.

For Windows with PostgreSQL 17 installed, create a fresh cluster in a temporary folder and use a free loopback port. For example, in PowerShell (choose an unused port):

```powershell
$pharmacyPgBin = 'C:\Program Files\PostgreSQL\17\bin'
$pharmacyTestRoot = Join-Path $env:TEMP ('HealthERP-Pharmacy-Pg-' + [guid]::NewGuid().ToString('N'))
$pharmacyTestPort = 55483
New-Item -ItemType Directory -Path $pharmacyTestRoot | Out-Null
& "$pharmacyPgBin\initdb.exe" -D "$pharmacyTestRoot\data" -U health_erp_tests -A trust --encoding=UTF8 --locale=C
& "$pharmacyPgBin\pg_ctl.exe" -D "$pharmacyTestRoot\data" -l "$pharmacyTestRoot\server.log" -o "-h 127.0.0.1 -p $pharmacyTestPort" -w start
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = "Host=127.0.0.1;Port=$pharmacyTestPort;Database=postgres;Username=health_erp_tests"
try {
    dotnet test HealthERP.slnx
} finally {
    & "$pharmacyPgBin\pg_ctl.exe" -D "$pharmacyTestRoot\data" -m fast -w stop
    Remove-Item Env:HEALTHERP_PHARMACY_TEST_CONNECTION
}
```

Trust authentication is confined to the short-lived loopback test cluster. Keep the existing ERP service/database separate. Preserve the test logs if diagnosing failures. Missing configuration causes an explicit test failure; database tests are not silently skipped.

If a running development API locks build DLLs, use the SDK's isolated output option for build/test:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch3-20261012"
dotnet test HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch3-20261012"
```

The two concurrency tests pause both requests in a test-only SaveChanges interceptor until both have read/mutated their initial snapshot. They then release both writes together. PostgreSQL's serializable transaction aborts the losing transaction; the production middleware returns 409. The test verifies database stock and both prescription states afterward. A database trigger in the disposable test database forces a required prescription write to fail and verifies full rollback and cleared EF tracking state.
