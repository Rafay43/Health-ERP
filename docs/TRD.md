# Technical Requirements Document (TRD)
## Health ERP System

> Companion to `PRD.md`. Reflects the architecture actually implemented so far plus the plan for what remains.

---

## 1. System Architecture Overview

**Pattern:** Modular Monolith, following Clean Architecture layering with tactical DDD patterns and selective CQRS (via MediatR).

**Rationale:** A Health ERP's core complexity is enforcing correct relational invariants across clinical/billing data — better served by a single strongly-consistent transactional boundary than a distributed microservices topology. Each business module (Patients, Scheduling, Billing, Pharmacy, Lab) owns its own PostgreSQL schema, keeping internal boundaries clean enough to extract into standalone services later if a concrete scaling need arises.

**Layers** (dependency flows one direction only, enforced by an automated NetArchTest rule):
```
Domain  →  (no dependencies)
Application  →  Domain, SharedKernel
Infrastructure  →  Application, Domain
Api (Presentation)  →  Application, Infrastructure
```

---

## 2. Technology Stack

| Layer | Technology | Status |
|---|---|---|
| Backend runtime | .NET 10, ASP.NET Core Minimal API | ✅ In use |
| CQRS / Mediator | MediatR 14 | ✅ In use |
| Validation | FluentValidation 12 | ✅ In use |
| ORM | EF Core 10 + Npgsql provider | ✅ In use |
| Database | PostgreSQL 16 | ✅ In use |
| Auth | ASP.NET Core Identity + JWT Bearer | 🚧 In progress |
| Real-time | SignalR | ⏳ Planned (Scheduling module) |
| Background jobs | Hangfire | ⏳ Planned |
| Testing (unit/integration) | xUnit, Shouldly, NetArchTest, Testcontainers | ✅ Partially in use (architecture test only so far) |
| Testing (E2E) | Playwright | ⏳ Planned |
| Frontend framework | Angular 22 (standalone components, signals) | ✅ In use |
| UI components | PrimeNG **v21 (pinned — MIT license)** | ✅ In use |
| Styling | Tailwind CSS v4 | ✅ In use |
| HTTP client | Angular `HttpClient` | ✅ In use |
| State management | Angular signals (default); NgRx SignalStore reserved for Billing/Scheduling | ⏳ Planned |
| CI/CD | GitHub Actions | ⏳ Planned |
| Containerization | Docker Compose | ⏳ Planned (not yet installed in dev environment) |

**Critical licensing note:** PrimeNG version 22+ requires a paid "PrimeUI" commercial license (introduced by PrimeTek after this project's initial planning). This project is explicitly pinned to **PrimeNG 21.x**, the last permanently MIT-licensed release. `package.json` must not use a caret (`^`) on this dependency to prevent an accidental upgrade.

---

## 3. Data Models

### 3.1 Implemented: Patient Aggregate

| Field | Type | Notes |
|---|---|---|
| Id | `PatientId` (strongly-typed GUID) | Primary key |
| MedicalRecordNumber | string(20) | Unique, auto-suggested sequential format `MRN-0001` |
| FirstName | string(100) | Required |
| LastName | string(100) | Required |
| DateOfBirth | DateOnly | Required, must not be future-dated |
| Gender | string(20)? | Optional |
| PhoneNumber | string(20)? | Optional |
| Email | string(256)? | Optional |
| IsDeleted | bool | Soft-delete flag, global query filter applied |
| CreatedAtUtc / CreatedBy | DateTime / string | Auto-populated via `SaveChangesInterceptor` |
| ModifiedAtUtc / ModifiedBy | DateTime? / string? | Auto-populated on update |

**Known simplification:** MRN generation currently uses a `COUNT(*) + 1` approach for the suggested next value, computed at the time the registration form loads. This is not atomically race-safe under concurrent registrations. **Planned fix:** move to a PostgreSQL sequence for gap-tolerant, race-safe numbering (see `memory.md`).

### 3.2 Planned: Identity Schema (in progress)
- `ApplicationUser` (extends `IdentityUser<Guid>`) — FirstName, LastName, IsActive, CreatedAtUtc
- `ApplicationRole` (extends `IdentityRole<Guid>`)
- Standard Identity tables (`user_roles`, `user_claims`, `user_logins`, `role_claims`, `user_tokens`), mapped into a dedicated `identity` schema, separate from `patients`

### 3.3 Planned Aggregates (not yet implemented)
Doctor/Staff, Appointment, Invoice, InsuranceClaim, Prescription, Drug, LabOrder, LabResult — see `PRD.md` §3 for feature scope and `architecture.md` for the entity relationship sketch.

---

## 4. API Specifications

Base path convention: `/api/v1/{resource}`

### Implemented endpoints

| Method | Route | Purpose | Status |
|---|---|---|---|
| POST | `/api/v1/patients` | Register a new patient | ✅ |
| GET | `/api/v1/patients` | List patients (paginated) | ✅ |
| GET | `/api/v1/patients/next-mrn` | Get suggested next MRN | ✅ |

### Error handling
Global exception middleware maps exceptions to RFC 7807 `ProblemDetails`:

| Exception | HTTP Status |
|---|---|
| `ValidationException` (FluentValidation) | 400 |
| `DbUpdateException` — Postgres unique violation (`SqlState 23505`) | 409 |
| `DbUpdateException` — other | 400 |
| `ArgumentException` | 400 |
| Unhandled | 500 |

### Planned endpoints (not yet built)
- `POST /api/v1/auth/register`, `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`
- `GET/POST/PUT /api/v1/appointments`
- `GET/POST /api/v1/invoices`
- Full CRUD for Staff, Pharmacy, Lab modules per `PRD.md`

**Conventions:** URL versioning (`/v1/`), pagination via `?page=&pageSize=` query params, RFC 7807 error envelope for all failures.

---

## 5. Security Considerations

| Concern | Approach |
|---|---|
| Authentication | ASP.NET Core Identity + JWT (access token short-lived, refresh token rotation) — **in progress** |
| Authorization | RBAC via claims-based policies; resource-based auth for record-level access (planned) |
| Password storage | ASP.NET Identity's built-in PBKDF2 hasher (default) |
| Transport | HTTPS enforced (`UseHttpsRedirection`) |
| CORS | Explicit allow-list policy (`AllowAngularDev`) — currently scoped to `localhost:4200` for dev; **must be tightened before any non-local deployment** |
| Secrets | .NET User Secrets locally; never committed. Production secrets via environment variables / hosting platform secret store |
| PHI audit | Audit interceptor auto-stamps `CreatedAtUtc`/`CreatedBy`/`ModifiedAtUtc`/`ModifiedBy`; `CreatedBy` currently hardcoded to `"system"` pending real user context from JWT claims |
| Field-level encryption | Planned for PII fields (SSN, insurance ID) — not yet implemented |

**[ASSUMPTION]** Given no explicit compliance certification was specified, this system is being built with HIPAA-*inspired* controls (audit trails, access logging, encryption at rest via managed Postgres) as a due-diligence baseline, not as a certified/audited HIPAA-compliant system. A real deployment handling PHI would require a formal compliance review beyond what's implemented here.

---

## 6. Scalability Plan

- **Current state:** single Postgres instance, single API instance, no caching layer — appropriate for current dev/demo scale.
- **Near-term:** Redis for session cache and SignalR backplane once real-time features (Scheduling) are built.
- **Medium-term:** read replicas for reporting/analytics queries to avoid contention with transactional load.
- **Long-term / conditional:** extraction of individual modules (e.g., Billing, Lab) into standalone services only if a concrete scaling or team-ownership need emerges — not planned preemptively, consistent with the modular monolith decision in §1.

---

*Related documents: `PRD.md`, `architecture.md`, `design.md`, `rules.md`, `task.md`, `memory.md`.*
