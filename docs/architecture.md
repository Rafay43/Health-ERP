# Architecture Document
## Health ERP System

> Companion to `TRD.md`. Text-based diagrams (Mermaid-compatible where noted) since this repo doesn't yet have a dedicated diagramming tool wired in.

---

## 1. High-Level System Diagram

```
                        ┌─────────────────────────┐
                        │      Angular Client       │
                        │  (health-erp-client, :4200)│
                        │  PrimeNG v21 + Tailwind    │
                        └────────────┬───────────────┘
                                     │ HTTPS / JSON (REST)
                                     │ CORS: AllowAngularDev policy
                                     ▼
                        ┌─────────────────────────┐
                        │   HealthERP.Api (:5268)   │
                        │  ASP.NET Core Minimal API │
                        │  Global Exception MW      │
                        │  (JWT auth — in progress) │
                        └────────────┬───────────────┘
                                     │ MediatR (in-process)
                                     ▼
                        ┌─────────────────────────┐
                        │  HealthERP.Application    │
                        │  CQRS Commands/Queries     │
                        │  FluentValidation pipeline │
                        └────────────┬───────────────┘
                                     │
                                     ▼
                        ┌─────────────────────────┐
                        │    HealthERP.Domain        │
                        │  Aggregates, Value Objects  │
                        │  Domain Events               │
                        └────────────┬───────────────┘
                                     ▲
                                     │ implements interfaces defined here
                        ┌────────────┴───────────────┐
                        │ HealthERP.Infrastructure     │
                        │  EF Core (ApplicationDbContext)│
                        │  Identity (Auth — in progress) │
                        │  Audit Interceptor              │
                        └────────────┬───────────────┘
                                     │ Npgsql
                                     ▼
                        ┌─────────────────────────┐
                        │   PostgreSQL (healtherp)   │
                        │  Schemas: patients, identity│
                        │  (planned: billing, pharmacy,│
                        │   lab, scheduling, audit)    │
                        └─────────────────────────┘
```

---

## 2. Component Breakdown

### 2.1 Frontend (`health-erp-client`)
```
src/app/
├── core/            # auth interceptors, singleton services (planned)
├── shared/          # reusable components/pipes/directives (planned)
├── layout/shell/     # ✅ built — sidebar nav, top bar, router outlet host
├── features/
│   ├── dashboard/     # ✅ placeholder
│   └── patients/       # ✅ built
│       ├── data-access/   # PatientService, models
│       ├── feature-registration/  # registration form
│       └── patient-list.ts/.html  # list screen
├── app.routes.ts     # ✅ routing config
└── app.config.ts     # ✅ providers: router, HttpClient, PrimeNG theme
```

### 2.2 Backend (`HealthERP.sln`)
```
src/
├── Core/
│   ├── HealthERP.Domain/
│   │   └── Patients/                 # ✅ Patient aggregate, PatientId, events
│   └── HealthERP.Application/
│       ├── Common/
│       │   ├── Interfaces/            # ✅ IApplicationDbContext
│       │   └── Behaviors/              # ✅ ValidationBehavior (MediatR pipeline)
│       └── Patients/
│           ├── Commands/RegisterPatient/  # ✅
│           └── Queries/GetPatients, GetNextMrn/  # ✅
├── Infrastructure/
│   └── HealthERP.Infrastructure/
│       ├── Persistence/
│       │   ├── ApplicationDbContext.cs    # ✅ (becoming IdentityDbContext)
│       │   ├── Configurations/             # ✅ PatientConfiguration
│       │   ├── Interceptors/                # ✅ AuditableEntitySaveChangesInterceptor
│       │   └── Migrations/                   # ✅ InitialCreate
│       └── Identity/                          # 🚧 ApplicationUser, ApplicationRole
├── Presentation/
│   └── HealthERP.Api/
│       ├── Endpoints/PatientEndpoints.cs  # ✅
│       ├── Middleware/GlobalExceptionHandlingMiddleware.cs  # ✅
│       └── Program.cs                       # ✅ DI wiring, CORS, middleware pipeline
└── SharedKernel/
    └── HealthERP.SharedKernel/         # ✅ BaseEntity, IDomainEvent, IAuditable
```

---

## 3. Data Flow — Patient Registration (Implemented Example)

```
1. User fills form in Angular (patient-registration.ts)
2. On submit → PatientService.registerPatient() → HttpClient POST
3. Request crosses CORS boundary (AllowAngularDev policy validates origin)
4. Minimal API endpoint (PatientEndpoints.MapPost) receives RegisterPatientCommand
5. MediatR dispatches to ValidationBehavior pipeline
6.   → FluentValidation validates command (RegisterPatientCommandValidator)
7.   → On failure: ValidationException thrown → caught by GlobalExceptionHandlingMiddleware → 400 response
8. RegisterPatientCommandHandler invoked
9.   → Patient.Register() factory method enforces domain invariants, raises PatientRegisteredEvent
10.  → _context.Patients.Add(patient)
11.  → SaveChangesAsync() triggers AuditableEntitySaveChangesInterceptor
12.     → stamps CreatedAtUtc / CreatedBy on the entity before it hits the database
13.  → EF Core issues INSERT via Npgsql to PostgreSQL
14.     → unique index on MedicalRecordNumber enforced at DB level
15.     → On violation: DbUpdateException → caught by middleware → 409 Conflict response
16. On success: 201 Created returned with new patient's Id
17. Angular navigates to /patients, which triggers GetPatientsQuery → table repopulates
```

---

## 4. Integration Points

| Integration | Status | Notes |
|---|---|---|
| PostgreSQL | ✅ Live | Local native install, port 5432 |
| DBeaver (dev tooling) | ✅ Live | Manual inspection/verification during development |
| Angular ↔ API (CORS) | ✅ Live | `localhost:4200` → `localhost:5268`, dev-only policy |
| ASP.NET Core Identity | 🚧 In progress | Tables being added to `identity` schema |
| JWT issuance | ⏳ Planned | Next step after Identity tables are confirmed working |
| SignalR (real-time scheduling) | ⏳ Planned | Not yet started |
| Hangfire (background jobs) | ⏳ Planned | Reminders, report generation |
| Email/SMS (Notifications) | ⏳ Planned | Provider not yet selected — candidates: SendGrid (email), Twilio (SMS) |
| Stripe (payments) | ⏳ Planned | Phase 4 per `PRD.md` roadmap |
| HL7/FHIR | ⏳ Planned | Phase 4, integration stub only |
| Docker / CI-CD | ⏳ Planned | Docker Desktop not yet installed in dev environment; deferred |

---

## 5. Known Architectural Debt / Deferred Decisions

- **MRN generation** is count-based, not using a DB sequence — flagged as a race-condition risk under concurrent load (see `TRD.md` §3.1 and `memory.md`).
- **`CreatedBy` audit field** is hardcoded to `"system"` — depends on JWT auth being completed to resolve to a real authenticated user.
- **CORS policy** is currently wide-open for `localhost:4200` only, appropriate for dev but must be revisited before any staging/production deployment.
- **No global exception logging sink** beyond console output yet — Serilog/Seq integration planned but not implemented.

---

*Related documents: `PRD.md`, `TRD.md`, `design.md`, `rules.md`, `task.md`, `memory.md`.*
