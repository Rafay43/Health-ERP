# PostgreSQL billing payment tests

`BillingPaymentTests` reuses the existing `PharmacyPostgresFixture` and its nonparallel collection. Batch 3's fixture and tests are unchanged. The shared environment variable remains `HEALTHERP_PHARMACY_TEST_CONNECTION`.

Use a separate disposable PostgreSQL cluster. The fixture requires an explicit loopback connection to the `postgres` maintenance database, creates a random `health_erp_pharmacy_test_<GUID>` database, runs `EnsureCreated` there, and drops only that generated database. It never uses the ERP connection, migrations, or seed data. Missing configuration fails explicitly.

PowerShell example with PostgreSQL 17 installed:

```powershell
$billingPgBin = 'C:\Program Files\PostgreSQL\17\bin'
$billingTestRoot = Join-Path $env:TEMP ('HealthERP-Billing-Pg-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $billingTestRoot | Out-Null
& "$billingPgBin\initdb.exe" -D "$billingTestRoot\data" -U health_erp_tests -A trust --encoding=UTF8 --locale=C
if ($LASTEXITCODE -ne 0) { throw 'Test cluster initialization failed' }
$billingListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$billingListener.Start()
$billingTestPort = $billingListener.LocalEndpoint.Port
$billingListener.Stop()
& "$billingPgBin\pg_ctl.exe" -D "$billingTestRoot\data" -l "$billingTestRoot\server.log" -o "-h 127.0.0.1 -p $billingTestPort" -w start
if ($LASTEXITCODE -ne 0) { throw 'Test cluster start failed' }
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = "Host=127.0.0.1;Port=$billingTestPort;Database=postgres;Username=health_erp_tests"
try {
    dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch4"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    dotnet test HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch4" --no-build --no-restore
} finally {
    & "$billingPgBin\pg_ctl.exe" -D "$billingTestRoot\data" -m fast -w stop
    Remove-Item Env:HEALTHERP_PHARMACY_TEST_CONNECTION
}
```

Trust authentication is confined to this temporary loopback cluster. Keep the ERP PostgreSQL service separate; preserve temporary logs when investigating a failure. The isolated artifacts folder avoids DLL locks from a running development API.

The three payment races use independent authenticated HTTP requests and scoped DbContexts against real PostgreSQL. An independent connection first holds the invoice row. Tests observe `pg_stat_activity` until both request transactions are blocked, release the row, and assert actual HTTP responses plus financial state in a fresh DbContext. This detects the old implementation too: its unlocked reads could finish and its stale UPDATE statements would wait at the same gate, then lose an update or overpay after release. There is no EF InMemory concurrency claim or test-only synchronization in production code.

An additional reproduction test runs the former handler algorithm exclusively against its own disposable invoice: both reads finish before either save, both payment records commit, and invoice PaidAmount retains only 30 or 40 while payment history totals 70. This verifies the original root cause; the production service uses the protected path.

Database triggers restricted to individual test invoices force Payment INSERT and Invoice UPDATE failures. They verify rollback, unchanged persisted history/amount/status, empty EF tracking, and safe API errors. PostgreSQL SQLSTATE 40001 and 40P01 triggers test error translation through Npgsql's execution-strategy wrappers; they do not claim to create genuine deadlocks. Cancellation after both writes but before commit is also tested. Trigger functions are removed in `finally`, and the fixture drops the disposable database.
