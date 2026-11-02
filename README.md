# Health ERP

**A full-stack hospital management application connecting clinical workflows, patient self-service, and role-based staff workspaces.**

Built with **ASP.NET Core 10, PostgreSQL, and Angular 22**, Health ERP explores how clinical and administrative workflows fit together in one application. Its most developed engineering areas include verified staff identity, scoped doctor access, patient account activation, and transaction handling for competing writes.

This is an actively developed portfolio application, not a certified clinical system. Shared workflows have broader permissions than the dedicated Doctor Workspace, and the general dashboard contains demonstration fallbacks. See the [publication review](docs/PUBLICATION_REVIEW.md) for limitations and issues to resolve before public hosting.

[Features](#main-features) · [Architecture](#architecture) · [Local setup](#local-setup) · [Tests and CI](#tests-and-ci) · [Screenshots](#screenshots)

## Main features

| Area | Implemented functionality |
| --- | --- |
| Patient management | Registration, server-generated MRNs, demographics, updates, search/list views, and patient health records. |
| Patient Portal | Account-linked profile, dashboard, appointments, health records, prescriptions, completed laboratory results, and billing views. |
| Patient activation | MRN/date-of-birth verification, an emailed code, and atomic account-to-patient linking. |
| Appointments | Booking with overlap checks, status updates, notifications, and overdue scheduled-appointment processing. Completion can create a consultation invoice. |
| Doctor Workspace | Personal daily schedule, appointment-linked patient snapshots, and clinical note/vitals recording with server-resolved authorship. |
| EHR | Clinical notes, persisted vital signs, and timelines combining appointments, prescriptions, laboratory orders, and invoices. |
| Prescriptions and pharmacy | Catalog/free-text prescriptions, drug stock/expiry views, dispensing, and transactional stock updates. |
| Laboratory | Test catalog, orders, priorities/statuses, and results with units, reference ranges, remarks, and abnormal flags. |
| Admissions and emergency care | Ward/bed views, admission, transfer, discharge, triage, bay assignment, and emergency-to-inpatient handoff. |
| Billing | Invoices, line items, payments, balances, consultation charges, and admission charge snapshots. |
| Administration | Staff management, explicit staff/account linking, account roles/status, executive views, and audit queries. |
| Notifications | Recipient-owned notifications and SignalR updates, including appointment-change notifications. |

Prescriptions represent stored orders and dispensing status, not proof of current medication use. Insurance claims, payment gateway integration, electronic prescribing networks, and FHIR/PACS integrations are not implemented.

## Tech stack

| Layer | Technologies verified in the repository |
| --- | --- |
| Backend | .NET 10, ASP.NET Core Minimal APIs, Identity, JWT bearer authentication, SignalR |
| Application | MediatR 14, FluentValidation 12, command/query handlers and pipeline validation |
| Persistence | EF Core 10, Npgsql/PostgreSQL, migrations, entity configurations, audit interceptor |
| Frontend | Angular 22 standalone components, TypeScript 6, RxJS 7, signals, reactive forms |
| UI | PrimeNG 22, PrimeIcons, Tailwind CSS 4, SweetAlert2 |
| Backend tests | xUnit, Shouldly, Moq, NetArchTest, in-memory/mocked persistence in some suites, real PostgreSQL integration tests |
| Frontend tests | Angular unit-test builder, Vitest, jsdom |

## Authentication and authorization

ASP.NET Core Identity manages accounts, password hashing, roles, lockout, and password-reset tokens. The API validates JWT signature, issuer, audience, and lifetime, then checks the live account and a security-stamp-derived session version. Role/status changes and staff/account association changes invalidate existing sessions through security-stamp updates.

Public registration assigns **Patient** server-side; it does not grant staff roles supplied by a caller. Angular route guards control navigation, while API authorization remains authoritative.

### Roles and policies

The principal roles are **Admin, Doctor, Nurse, Pharmacist, LabTechnician, Receptionist, BillingStaff, and Patient**.

| Backend policy | Allowed roles |
| --- | --- |
| `CanViewPatients`, `CanEditPatients` | Admin, Doctor, Nurse, Receptionist |
| `CanPrescribeMedication`, `CanOrderLabTest` | Admin, Doctor |
| `CanDispenseMedication` | Admin, Pharmacist |
| `CanEnterLabResult` | Admin, LabTechnician |
| `CanManageAdmissions` | Admin, Nurse, Receptionist |
| `CanManageBilling` | Admin, BillingStaff |
| `CanManageStaff`, `CanViewExecutiveReports` | Admin |

This describes named policies, not uniform patient-level authorization across every workflow. For example, inpatient discharge uses `CanEditPatients`. Shared patient APIs use broader role-based policies than the dedicated Doctor Workspace.

**Doctor identity and patient access.** Doctor endpoints require the Doctor role and an active, explicitly linked Staff record of kind Doctor. Identity comes from the authenticated account and database, not caller-provided staff IDs or name/email matching. Admin alone does not grant access to a doctor's personal workspace.

A doctor may open a snapshot or record a note when the current staff member has a non-deleted Scheduled, Confirmed, or Completed appointment with that patient. Cancelled/NoShow appointments alone do not qualify. Missing and unrelated patients receive the same safe not-found response. Notes store `RecordedByStaffId`; legacy notes are not assigned inferred authors.

**Patient ownership.** Portal queries resolve the linked patient from the signed-in account rather than accepting an arbitrary patient ID as ownership proof.

### Patient account activation

1. A signed-in Patient account submits an existing patient's MRN and date of birth.
2. The service emails the address already recorded by the hospital, not an address supplied in the request.
3. A cryptographically generated six-digit code is hashed, expires after ten minutes, and has a sixty-second resend cooldown and five verification attempts.
4. Verification locks the account, patient, and challenge records, then consumes the challenge and links the account atomically. Newer challenges supersede older ones.
5. Email delivery occurs after the transaction completes. Development supports delivery simulation without logging the code; production activation fails closed when delivery is unconfigured.

Patients without a recorded hospital email need staff assistance. The code hash currently uses a static salt; stronger protection against offline guessing is future hardening.

## Architecture

Health ERP is a **layered modular monolith**: one API and PostgreSQL database, organized by clinical and administrative feature areas. MediatR provides command/query separation, without separate read/write databases or event sourcing.

```text
Angular components / data-access services
                  |
      ASP.NET Core Minimal API endpoints
                  |
 Application commands / queries / validation
                  |
       Domain entities and rules
                  |
 Infrastructure: EF Core / Identity / services
                  |
              PostgreSQL
```

The diagram shows request flow. Project references keep Domain independent of Infrastructure; Infrastructure implements application interfaces. Handlers use `IApplicationDbContext` and DbSets directly, without a repository abstraction. Entities use strongly typed identifiers, factories, and domain events. Audit interception captures actor/timestamps; it is not an immutable, comprehensive audit ledger.

### Important folders

```text
HealthERP.slnx
src/
  Core/
    HealthERP.Domain/              Entities, identifiers, domain rules
    HealthERP.Application/         Commands, queries, DTOs, interfaces
  Infrastructure/
    HealthERP.Infrastructure/      EF persistence, migrations, Identity, services
  Presentation/
    HealthERP.Api/                 Endpoints, middleware, policies, hubs
    HealthERP.Api.Contracts/       Scaffold, not a complete contract layer
  SharedKernel/
    HealthERP.SharedKernel/        Shared entity/event primitives
health-erp-client/
  src/app/
    core/                         Shared services, authentication, guards
    layout/                       Staff and patient application shells
    features/                     Domain components and data-access services
    app.routes.ts                 Application route configuration
  public/                         Static assets
  proxy.conf.json                 Local API/SignalR proxy
tests/
  HealthERP.Domain.UnitTests/
  HealthERP.Application.UnitTests/
  HealthERP.Api.FunctionalTests/
  HealthERP.Api.IntegrationTests/
docs/                             Design notes and portfolio documentation
```

### API architecture

Endpoint extension classes define routes, authorization, HTTP contracts, and handler dispatch. `/api/v1` is a route convention. Development exposes `/openapi/v1.json`; a Swagger UI is not configured.

| Representative route | Purpose |
| --- | --- |
| `POST /api/v1/patient-account/activation/start` | Start activation |
| `POST /api/v1/patient-account/activation/verify` | Verify code and link patient |
| `GET /api/v1/patient-portal/me/health-record` | Read the current patient's record |
| `GET /api/v1/doctor/schedule?date=YYYY-MM-DD` | Read the current doctor's facility-local daily schedule |
| `GET /api/v1/doctor/patients/{patientId}/snapshot` | Read an authorized, bounded snapshot |
| `POST /api/v1/doctor/patients/{patientId}/clinical-notes` | Record clinical content/vitals with verified authorship |

### Database and concurrency

EF Core maps entities through configurations and migrations. Identity and clinical entities share PostgreSQL persistence. Patient soft-delete filtering, auditing, unique account-link constraints, and PostgreSQL `xmin` concurrency mapping are implemented.

| Workflow | Implemented protection |
| --- | --- |
| Dispensing | Serializable transaction couples prescription state and stock updates. |
| Payment | Invoice row lock and live balance recalculation keep payment/invoice changes atomic. |
| Inpatient transitions | Serializable transactions coordinate admissions, beds, transfers, discharge, and emergency handoff. |
| Patient activation | Ordered locks and atomic challenge consumption/account linking protect competing requests. |
| Staff/account linking | Row locking, unique links, and transactional security-stamp updates protect associations. |

Relevant serialization/deadlock failures become conflict responses; transaction services roll back failed changes. These protections are workflow-specific. Ordinary appointment overlap checks are not a database-enforced booking exclusion constraint, and note creation has no idempotency key.

## Local setup

### Prerequisites

- .NET SDK **10.0.x**.
- Node.js **24.15.0+ within 24.x** and npm; `package.json` declares npm 11.17.0. Use a Node release satisfying Angular's package engine constraints.
- PostgreSQL; **17** is used in CI. Create a dedicated development database and a user able to apply migrations.
- SMTP for real activation/password-reset delivery. Development activation supports log simulation.

Run from a clone of this repository. PowerShell examples use `npm.cmd` to avoid Windows script execution-policy issues.

```powershell
git clone https://github.com/Rafay43/Health-ERP.git
cd Health-ERP
```

### 1. Configure the API

Use .NET user secrets for local credentials. Replace these placeholders locally; do not commit real values. User secrets are development configuration, not a production secret vault.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=health_erp_dev;Username=<db-user>;Password=<db-password>" --project src/Presentation/HealthERP.Api
dotnet user-secrets set "JwtSettings:Secret" "<long-random-signing-key>" --project src/Presentation/HealthERP.Api
```

Generate at least 64 random bytes for a signing key and encode them for storage. The API rejects missing, short, or example signing keys and missing/example connection configuration. Tracked base configuration contains no credential defaults. See [appsettings.example.json](src/Presentation/HealthERP.Api/appsettings.example.json) for the configuration shape; it is a reference, not an automatically loaded secret file.

| Configuration | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Application PostgreSQL connection |
| `JwtSettings:Secret`, `Issuer`, `Audience`, `ExpiryMinutes` | Token signing/validation |
| `Facility:TimeZoneId` | Schedule date boundaries; currently `Asia/Karachi` |
| `EmailSettings:FromName`, `FromAddress` | Outgoing email identity |
| `EmailSettings:Host`, `Port`, `Username`, `Password`, `EnableSsl` | SMTP connection/credentials |
| `EmailSettings:FrontendBaseUrl` | URL used in reset links |
| `DevelopmentSeed:Enabled`, `AdminEmail`, `AdminPassword`, `DoctorEmail`, `DoctorPassword` | Explicit Development-only demo seeding; passwords must come from private configuration |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment; HTTP launch profile selects Development |
| `HEALTHERP_PHARMACY_TEST_CONNECTION` | Separate integration-test maintenance connection |

Environment overrides use double underscores, e.g. `ConnectionStrings__DefaultConnection`. A root `.env` is not automatically loaded. Angular configuration is public: never put signing, database, or SMTP credentials there.

Set email options with the same `dotnet user-secrets set` syntax when testing delivery. The email service treats a blank SMTP host or the literal `localhost` as simulation; use a configured, reachable SMTP hostname (or `127.0.0.1` for a local capture server). Simulation never logs codes or reset links. To complete activation or password reset locally, configure a capture SMTP server (for example, host `127.0.0.1`, port `1025`, SSL disabled) and read the delivered message there.

### 2. Run the backend

```powershell
dotnet restore HealthERP.slnx
dotnet run --project src/Presentation/HealthERP.Api --launch-profile http
```

API: `http://localhost:5268` · Development OpenAPI: `http://localhost:5268/openapi/v1.json`

**Development startup applies EF migrations and initializes roles. Demo accounts/catalogs are disabled by default.** Use a disposable database, never real patient records. To create an initial local Admin and synthetic catalogs, opt in using private configuration:

```powershell
dotnet user-secrets set "DevelopmentSeed:Enabled" "true" --project src/Presentation/HealthERP.Api
dotnet user-secrets set "DevelopmentSeed:AdminEmail" "admin@example.invalid" --project src/Presentation/HealthERP.Api
dotnet user-secrets set "DevelopmentSeed:AdminPassword" "<unique-local-password>" --project src/Presentation/HealthERP.Api
```

Restart the API after configuring these values. Optional Doctor seeding requires both `DevelopmentSeed:DoctorEmail` and `DevelopmentSeed:DoctorPassword`. No credentials are prefilled in the login screen. Existing account credentials are never changed by seeding; retire any old known demo accounts manually. Disable the opt-in after bootstrap. Synthetic seeding can attach sample triage records to an existing patient, so use it only with disposable data. The initializer refuses seeding outside Development. Non-Development migrations/role provisioning require an explicit operational process.

### 3. Run the frontend in a second terminal

```powershell
cd health-erp-client
npm.cmd ci
npm.cmd start
```

Open `http://localhost:4200`. The proxy forwards `/api` and `/hubs` to port 5268, including SignalR WebSockets. Development CORS permits the frontend on port 4200.

For Doctor Workspace demonstrations, use Admin staff management to link an active Staff record of kind Doctor to an account with the Doctor role, then sign in again. Matching names/emails do not establish identity. Create synthetic patients and qualifying appointments.

## Tests and CI

Suites cover domain/application rules, architecture boundaries, API authorization/ownership, and PostgreSQL transaction/concurrency behavior. Frontend tests include Doctor Workspace navigation, stale requests, snapshot states, and note entry. Some backend suites use mocks/in-memory providers; concurrency tests use real PostgreSQL.

```powershell
# Backend unit/functional suites
dotnet test tests/HealthERP.Domain.UnitTests
dotnet test tests/HealthERP.Application.UnitTests
dotnet test tests/HealthERP.Api.FunctionalTests

# Full backend suite: disposable local PostgreSQL only
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = "Host=127.0.0.1;Port=5432;Database=postgres;Username=<test-user>;Password=<test-password>"
dotnet test HealthERP.slnx

# From health-erp-client
npm.cmd test -- --watch=false
npm.cmd run build
```

The fixture requires a loopback connection to maintenance database `postgres` and permission to create/drop databases. It creates uniquely named databases and drops them afterward. Never use a shared/production server. See [integration test instructions](tests/HealthERP.Api.IntegrationTests/PHARMACY_TESTS.md).

[GitHub Actions CI](.github/workflows/ci.yml) restores, builds, and tests .NET against PostgreSQL 17. A separate job installs locked Angular dependencies, builds production assets, and runs frontend tests. Permissions are read-only; no deployment or production credentials are configured.

**Current verification (2 November 2026):** .NET Release build and all **485 backend tests** pass (122 unit/functional; 363 PostgreSQL integration). Clean npm ci, the Angular production build, and all **170 frontend tests** pass. Details are recorded in the [publication review](docs/PUBLICATION_REVIEW.md). Historical report counts are not coverage percentages. GitHub-hosted CI has not been run; publication remains blocked on historical credential cleanup and rotation.

## Screenshots

Application screenshots have not been added. Existing login images are decorative artwork. The [capture guide](docs/screenshots/README.md) provides planned filenames and privacy checks; no artificial captures are presented here.

| Planned capture | Status |
| --- | --- |
| Doctor schedule and patient snapshot | To be captured with synthetic data |
| Clinical note/vitals entry | To be captured with synthetic data |
| Patient Portal and activation | To be captured with synthetic data |
| Pharmacy, laboratory, admissions/billing | To be captured with synthetic data |

## Engineering highlights

- **Explicit identity:** administered staff/account links and server-resolved clinical authorship.
- **Checks accompany writes:** note creation rechecks appointment-based patient access.
- **Facility-aware dates:** local dates become half-open UTC schedule intervals with deterministic ordering.
- **Bounded snapshots:** selected recent records and latest persisted vitals limit response scope.
- **Real database tests:** PostgreSQL scenarios exercise locks, rollbacks, and transaction conflicts.
- **Context-aware UI:** Doctor Workspace tests cover stale responses and asynchronous context changes.

## Security considerations and future work

The current working files have had credential defaults, raw authentication logging, fixed demo passwords, and tracked test output removed. The [publication review](docs/PUBLICATION_REVIEW.md) distinguishes resolved issues from manual history cleanup/credential rotation and remaining production-readiness gaps. Do not publish until the historical exposure is handled. No regulatory-compliance claim is made.

Authentication state currently uses localStorage, making XSS prevention particularly important. Shared APIs do not provide uniform patient-level authorization. Keep development databases/logs separate from real patient information.

Clearly labeled **future work**: strengthen OTP hashing/rate limiting; review shared access/policy consistency; replace dashboard demonstration fallbacks; add browser-level end-to-end tests; evaluate booking constraints and write idempotency. Deployment automation and healthcare interoperability/certification are not implemented features.
