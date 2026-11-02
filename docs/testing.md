# Testing

Run commands from the repository root. Tests create scenario records with synthetic identities; never supply a real ERP database.

## Unit and functional suites


```powershell
dotnet restore HealthERP.slnx
dotnet build HealthERP.slnx --configuration Release --no-restore
dotnet test tests/HealthERP.Domain.UnitTests --configuration Release --no-build
dotnet test tests/HealthERP.Application.UnitTests --configuration Release --no-build
dotnet test tests/HealthERP.Api.FunctionalTests --configuration Release --no-build
```

Functional tests exercise HTTP authentication/session/ownership behavior with isolated test hosts. Some suites use mocks or EF InMemory; those providers are not evidence of PostgreSQL concurrency behavior.

## Real PostgreSQL integration tests

The shared fixture retains the historical environment name HEALTHERP_PHARMACY_TEST_CONNECTION across pharmacy, billing, admissions, activation and doctor/staff tests. It requires a loopback host and maintenance database postgres, plus permission to create/drop test databases. Missing/unsafe configuration fails explicitly rather than silently skipping.

The fixture creates a uniquely named health_erp_pharmacy_test database, initializes the current EF model with EnsureCreated, and drops that generated database afterward. It does not load application configuration, run production migrations, or seed the normal ERP database.

For PostgreSQL 17 on Windows, create a disposable cluster on an unused loopback port. Trust authentication below is for that isolated local cluster only:

```powershell
$pgBin = 'C:\Program Files\PostgreSQL\17\bin'
$pgRoot = Join-Path $env:TEMP ('HealthERP-Test-' + [guid]::NewGuid().ToString('N'))
$pgPort = 55483 # Choose an unused port.
New-Item -ItemType Directory -Path $pgRoot | Out-Null
& "$pgBin/initdb.exe" -D "$pgRoot/data" -U health_erp_tests -A trust --encoding=UTF8 --locale=C
if ($LASTEXITCODE -ne 0) { throw 'Test cluster initialization failed.' }
try {
    & "$pgBin/pg_ctl.exe" -D "$pgRoot/data" -l "$pgRoot/server.log" -o "-h 127.0.0.1 -p $pgPort" -w start
    if ($LASTEXITCODE -ne 0) { throw 'Test cluster startup failed.' }
    $env:HEALTHERP_PHARMACY_TEST_CONNECTION = "Host=127.0.0.1;Port=$pgPort;Database=postgres;Username=health_erp_tests"
    dotnet test HealthERP.slnx --configuration Release --no-build
} finally {
    & "$pgBin/pg_ctl.exe" -D "$pgRoot/data" -m fast -w stop
    Remove-Item Env:HEALTHERP_PHARMACY_TEST_CONNECTION -ErrorAction SilentlyContinue
}
```

Use an appropriate temporary path if the system drive lacks space. If a running API locks DLLs, use the same --artifacts-path directory for restore, build and test. Stop only the cluster created for the test run; do not adapt generated-database cleanup to operational databases.

### Engineering scenarios covered

- Dispensing couples prescription state and stock; races and forced write failures check conflict handling/rollback.
- Payments lock invoice state; concurrent/overpayment cases verify persisted balances, payment rows and atomic rollback.
- Admissions/transfers/discharges and emergency handoff exercise bed/patient contention and losing-operation preservation.
- Activation exercises cooldown, attempt limits, expiry, supersession, atomic account linking and competing verification.
- Doctor/staff tests exercise identity associations, authorization, schedule boundaries and clinical authorship.
- Test-only interceptors/triggers coordinate contention and failures; no test synchronization is added to production code. Serialization/deadlock taxonomy checks should not be presented as proof of every possible real deadlock scenario.

## Angular and CI

```powershell
cd health-erp-client
npm.cmd ci
npm.cmd run build
npm.cmd test -- --watch=false
```

Angular tests use Vitest/jsdom, isolated files and disabled test code splitting for predictable mocks. Notification/billing and asynchronous Doctor Workspace assertions remain enabled. A component test is not a browser-level end-to-end test.

The [CI workflow](../.github/workflows/ci.yml) restores/builds/tests .NET with an ephemeral PostgreSQL service and installs/builds/tests Angular. It uses disposable test credentials, no production secrets, and no deployment steps. Test counts describe executed cases, not coverage percentages; report the actual result of your checkout.
