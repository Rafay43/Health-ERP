# Health ERP System — Architecture & Planning Blueprint

> Living reference document. Keep this in `/docs` at the root of your project repo and update it as decisions evolve.

---

## Table of Contents

1. [Phase 1: Architecture & Design — Step-by-Step Guide](#1-phase-1-architecture--design--step-by-step-guide)
2. [Technology Decisions Summary](#2-technology-decisions-summary)
3. [System Architecture](#3-system-architecture)
4. [Core Modules & Features](#4-core-modules--features)
5. [UI/UX Design Plan](#5-uiux-design-plan)
6. [Technical Implementation Details](#6-technical-implementation-details)
7. [Portfolio Presentation Strategy](#7-portfolio-presentation-strategy)
8. [Full Project Flow (All Phases)](#8-full-project-flow-all-phases)
9. [Phased Development Roadmap](#9-phased-development-roadmap)

---

## 1. Phase 1: Architecture & Design — Step-by-Step Guide

This is the immediate action plan. Phase 1 assumes Phase 0 (requirements gathering) is either done or being done in parallel by you as solo architect/developer. Target duration: **1–2 weeks**.

### Step 1 — Lock the Module List and Priority Order
- [ ] Write down the final module list for v1 (use the list in [Section 4](#4-core-modules--features) as your checklist)
- [ ] Tag each module/feature as **Must-have (MVP)**, **Should-have (Phase 2)**, or **Could-have (Phase 3/4)**
- [ ] Output: a `MODULES.md` or a simple table — this becomes your backlog source of truth

### Step 2 — Finalize the Architecture Pattern (Decision, Not Exploration)
- [ ] Confirm: **Modular Monolith**, Clean Architecture layering, CQRS via MediatR (selective, not full event sourcing)
- [ ] Draw the dependency diagram (Domain → Application → Infrastructure → API) — even a quick Mermaid diagram in your docs is enough
- [ ] Decide schema-per-module boundary now (e.g., `patients`, `scheduling`, `billing`, `pharmacy`, `lab`, `identity`, `audit`) so you don't retrofit this later
- [ ] Write a one-paragraph ADR (Architecture Decision Record) explaining *why* — this becomes portfolio gold later

### Step 3 — Scaffold the Solution Structure
- [ ] Create the .NET solution and empty projects matching the structure in [Section 3.2](#32-net-10-solution-structure)
- [ ] Set up `Directory.Build.props` and `.editorconfig` for consistent code style across projects
- [ ] Add the **NetArchTest** enforcement test immediately (empty projects, but the rule exists from commit #1)
- [ ] Set up the Angular workspace with standalone components, routing skeleton, and the `core/`, `shared/`, `features/`, `layout/` folder structure

### Step 4 — Design the Core Database Schema (First Pass ERD)
- [ ] Model the core aggregates first: `Patient`, `Encounter`, `Appointment`, `Doctor/Staff`, `Invoice`, `Prescription`
- [ ] Define strongly-typed IDs for each aggregate (`PatientId`, `EncounterId`, etc.)
- [ ] Mark every entity that needs audit tracking with an `IAuditable` interface convention
- [ ] Add `TenantId` to relevant tables now, even if you run single-tenant initially
- [ ] Draw the ERD (dbdiagram.io, draw.io, or Mermaid `erDiagram` block in your docs) and save it to `/docs/ERD.md`
- [ ] Decide soft-delete convention (`IsDeleted` + global query filter) and apply it consistently

### Step 5 — Security Architecture Sign-Off (Do This Before Writing Feature Code)
- [ ] Finalize auth approach: ASP.NET Core Identity + JWT (access + refresh token rotation)
- [ ] Define the initial role list: `SystemAdmin`, `Physician`, `Nurse`, `Receptionist`, `BillingClerk`, `Pharmacist`, `LabTech`
- [ ] Decide which fields need column-level encryption (SSN, insurance ID, etc.) and pick the mechanism (EF Core value converters + AES, or `pgcrypto`)
- [ ] Design the audit log table structure (`audit.entity_changes`: entity name, entity id, action, old values, new values, user id, timestamp) — decide if you use EF Core `SaveChangesInterceptor` or DB triggers
- [ ] Document this in `/docs/SECURITY.md`

### Step 6 — API Contract Skeleton
- [ ] Define the URL versioning convention (`/api/v1/...`)
- [ ] Sketch endpoint groups per module (don't implement yet — just list routes and verbs)
- [ ] Decide on the error response shape (RFC 7807 `ProblemDetails`) and pagination envelope
- [ ] Set up Swagger/NSwag so the contract is visible and testable from day one

### Step 7 — UI/UX Foundation
- [ ] Install and configure PrimeNG + Tailwind in the Angular project
- [ ] Set up the design tokens (colors, typography) from [Section 5.2](#52-color-palette) as Tailwind config + CSS variables, so PrimeNG components inherit your theme instead of default styling
- [ ] Build the app shell first: sidebar nav, top bar, routing outlet — before any feature screens
- [ ] Wireframe (even low-fidelity, e.g., Figma or Excalidraw) the three anchor screens: Dashboard, Patient Profile, Appointment Calendar — these set the visual language for everything else

### Step 8 — Environments & CI/CD Setup (Do Not Defer)
- [ ] Set up GitHub Actions (or equivalent) pipeline: restore → build → test → docker build
- [ ] Create `docker-compose.yml` for local dev: API + Angular + Postgres + Redis + Seq (logging)
- [ ] Provision dev/staging environments now, even if minimal — retrofitting deployment infrastructure after 10 modules are built is far more painful
- [ ] Set up structured logging (Serilog) and a log viewer (Seq) from the start

### Step 9 — Write the ADRs and Close Out Phase 1
- [ ] Consolidate all decisions above into `/docs/ARCHITECTURE.md` (this file can be the seed)
- [ ] Confirm non-functional requirements are documented: expected concurrent users, data volume assumptions, uptime target
- [ ] Checkpoint: you should be able to run `docker compose up`, hit a health-check endpoint, and load an empty Angular shell — that's your Phase 1 "done" signal
- [ ] Move into Phase 2 (MVP build) using vertical slices, starting with Auth → Patient Management

---

## 2. Technology Decisions Summary

| Layer | Decision | Rationale (short) |
|---|---|---|
| Backend | .NET 10 Web API, Minimal API endpoint groups | Modern, performant, strong typing |
| Architecture | Modular Monolith, Clean Architecture, DDD (tactical), CQRS via MediatR | Testable, maintainable, microservices-ready without premature complexity |
| Database | PostgreSQL (primary) | Best relational integrity for ERP data + JSONB flexibility where needed |
| Caching / Realtime backplane | Redis | Session cache, SignalR backplane, reference data cache |
| Frontend | Angular (latest), standalone components, Signals | Modern, no NgModules overhead |
| Frontend state | Signals + services (default), NgRx SignalStore (Billing/Scheduling only) | Right-sized complexity per module |
| UI Components | PrimeNG | Data-dense component richness (tables, scheduler, tree) needed for ERP |
| Styling | Tailwind CSS | Full design control, avoids "generic admin template" look |
| Auth | ASP.NET Core Identity + JWT (access + refresh), optional OIDC/SSO | Standard, extensible to enterprise SSO |
| AuthZ | RBAC + claims + resource-based policies | Role-specific and record-level access control |
| Real-time | SignalR | Live scheduling updates, notifications, inventory alerts |
| Background jobs | Hangfire | Reminders, report generation, stock alerts |
| Testing | xUnit, FluentAssertions, NSubstitute, Testcontainers, Playwright | Full pyramid: unit → integration → E2E |
| Deployment | Docker + GitHub Actions CI/CD | Portable, reproducible, cloud-agnostic |
| Hosting (demo) | Azure App Service / VPS (API), Vercel/Cloudflare Pages (frontend), Neon/Supabase (Postgres) | Low-cost, portfolio-appropriate |

---

## 3. System Architecture

### 3.1 Architecture Pattern Justification

**Clean Architecture + DDD (tactical patterns) + CQRS (selective) + Modular Monolith.**

| Pattern | Why it fits | Trade-off |
|---|---|---|
| Clean Architecture | Isolates domain/business rules from frameworks; testable; demonstrates senior structuring | More upfront boilerplate than 3-tier |
| DDD (tactical) | Health domains have rich invariants (e.g., a claim can't be submitted without a valid diagnosis code) | Overkill for trivial CRUD lookup tables |
| CQRS (via MediatR) | Read-heavy modules (dashboards, search) benefit from separate optimized read paths | Don't do full Event Sourcing — added complexity without ROI at this stage |
| Modular Monolith (not microservices) | Single deployable, enforced internal module boundaries, avoids distributed-transaction pain, still splits into microservices later if needed | No independent per-module scaling until actually split out |

**System design pattern comparison (monolith vs. microservices vs. serverless):**

| Pattern | Pros | Cons | Verdict |
|---|---|---|---|
| Traditional Monolith | Simple to build/deploy/debug | Becomes unmaintainable as modules grow | Too rigid past ~8-10 modules |
| Microservices | Independent scaling/deployment, fault isolation | Distributed transactions are hard; heavy operational overhead | Right *later*, wrong as a starting point |
| Modular Monolith | Single transaction boundary, enforced internal boundaries, low ops overhead | Requires discipline to maintain boundaries | **Chosen approach** |
| Serverless | Pay-per-use, auto-scales | Cold starts hurt real-time workflows; awkward for stateful multi-step flows | Good for slices (notifications, PDF generation, batch reports) not the whole system |

### 3.2 .NET 10 Solution Structure

```
HealthERP.sln
├── src/
│   ├── Core/
│   │   ├── HealthERP.Domain/            # Entities, Value Objects, Aggregates, Domain Events
│   │   └── HealthERP.Application/       # Use cases, CQRS handlers, DTOs, validators
│   ├── Infrastructure/
│   │   ├── HealthERP.Infrastructure/    # EF Core, Identity, Notifications, File Storage
│   │   └── HealthERP.Infrastructure.Reporting/  # Optional separate read-model context
│   ├── Presentation/
│   │   ├── HealthERP.Api/               # ASP.NET Core Web API, Minimal APIs, SignalR Hubs
│   │   └── HealthERP.Api.Contracts/     # Shared request/response contracts
│   └── SharedKernel/
│       └── HealthERP.SharedKernel/      # Result<T>, Guard clauses, pagination helpers
├── tests/
│   ├── HealthERP.Domain.UnitTests/
│   ├── HealthERP.Application.UnitTests/
│   ├── HealthERP.Api.IntegrationTests/
│   └── HealthERP.Api.FunctionalTests/
└── build/
```

**Dependency rule:** `Domain` → no dependencies. `Application` → depends only on `Domain`. `Infrastructure`/`Api` → depend on `Application`/`Domain`. Enforce with NetArchTest:

```csharp
[Fact]
public void Domain_Should_Not_HaveDependencyOnOtherProjects()
{
    var result = Types.InAssembly(DomainAssembly)
        .ShouldNot()
        .HaveDependencyOnAny("HealthERP.Application", "HealthERP.Infrastructure", "HealthERP.Api")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

### 3.3 Angular Frontend Structure

```
health-erp-client/
├── src/app/
│   ├── core/          # auth, interceptors, singleton services
│   ├── shared/         # dumb/reusable components, pipes, directives
│   ├── layout/         # shell: sidebar, topbar, notifications panel
│   ├── features/       # lazy-loaded: patients, appointments, billing, pharmacy,
│   │                    # laboratory, staff, reports, admin
│   ├── app.routes.ts
│   ├── app.config.ts
│   └── app.component.ts
└── environments/
```

**State management approach:**

| Option | When to use |
|---|---|
| Plain Angular Signals + Services | Default choice for most feature modules |
| NgRx SignalStore | Billing and Scheduling only — where multi-step/optimistic-update complexity justifies it |
| RxJS + Services | Server-driven state, real-time streams (SignalR), integrated via `toSignal()` |
| Full NgRx | Not used — too heavy for this scale; mention as "evaluated but not needed" |

### 3.4 Database Choice & Schema Design

**Chosen: PostgreSQL** (primary), **Redis** (cache/backplane), object storage (S3/Azure Blob) for binaries.

| Criteria | PostgreSQL | MySQL | MongoDB |
|---|---|---|---|
| Complex relationships | Excellent — native FKs, joins, CTEs | Good, weaker query planner for complex joins | Poor — relational data needs manual embedding/referencing |
| HIPAA/compliance fit | Row-level security, `pgaudit`, PITR | Comparable, fewer native extensions | Requires custom tooling |
| Semi-structured data | JSONB with GIN indexing | Weaker JSON indexing | Native strength |
| Scalability | Vertical excellent; horizontal via Citus/read replicas | Proven at scale | Native sharding, sacrifices relational integrity |

**Schema design approach:**
- Aggregate-per-schema grouping: `patients`, `scheduling`, `billing`, `pharmacy`, `lab`, `identity`, `audit`
- Soft deletes via `IsDeleted` + global query filters
- Temporal/audit tables via EF Core `SaveChangesInterceptor` writing to `audit.entity_changes`
- Strongly-typed IDs (record structs) to prevent primitive obsession bugs

**Core entity relationships:**

```
Patient (1) ──< Encounter (1) ──< Diagnosis
   │                  │
   │                  └──< Prescription ──< PrescriptionItem >── Drug (Pharmacy)
   │
   ├──< Appointment >── Doctor (Staff)
   ├──< Invoice ──< InvoiceLineItem
   │        └──< InsuranceClaim
   └──< LabOrder ──< LabResult
```

### 3.5 Authentication / Authorization Strategy

- **AuthN:** ASP.NET Core Identity + JWT bearer tokens (access token ~15 min TTL, refresh token rotation via httpOnly cookie). Optional OAuth2/OIDC via Azure AD B2C or Auth0 for enterprise SSO.
- **AuthZ:** RBAC + claims-based policies. Roles: `SystemAdmin`, `Physician`, `Nurse`, `Receptionist`, `BillingClerk`, `Pharmacist`, `LabTech`, `Patient` (if portal added). Resource-based authorization via `IAuthorizationHandler` for record-level access (e.g., doctor limited to assigned department's patients).
- **Multi-tenancy:** `TenantId` on relevant aggregates from day one, even if single-tenant initially.
- **Compliance hardening:** encryption at rest for PII (EF Core value converters + AES), TLS everywhere, field-level audit logging, session timeout policies.

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy("CanManagePrescriptions", policy =>
        policy.RequireRole("Physician", "SystemAdmin"))
    .AddPolicy("CanAccessPatientRecord", policy =>
        policy.Requirements.Add(new PatientRecordAccessRequirement()));
```

---

## 4. Core Modules & Features

### 4.1 Patient Management
- Registration (demographics, insurance info, emergency contacts, consent forms)
- Unique MRN (Medical Record Number) generation
- EHR/EMR: problem list, allergies, immunizations, vitals history, chronic conditions
- Medical history timeline (encounters, diagnoses, procedures)
- Document uploads (scanned IDs, referral letters, imaging attachments)
- Family/emergency contact management
- Patient portal (Phase 3): view own records, book appointments

### 4.2 Doctor/Staff Management
- Staff profiles: specialties, license numbers, credentials/certifications with expiry tracking
- Department/branch assignment
- Work schedule & shift management
- Leave/time-off requests and approval workflow
- Performance metrics feeding the Reports module

### 4.3 Appointment & Scheduling System
- Calendar view (day/week/month) per doctor/department
- Slot-based booking with conflict detection
- Appointment types (new patient, follow-up, telemedicine, procedure)
- Waitlist management
- Recurring appointments
- Check-in/check-out workflow with real-time status updates (SignalR)
- No-show tracking and automated reminders

### 4.4 Billing & Invoicing
- Service/procedure catalog with CPT-style codes and pricing
- Invoice generation from completed encounters
- Insurance claim submission workflow (Draft → Submitted → Adjudicated → Paid/Denied)
- Payment processing (Stripe) + partial payments, payment plans
- Co-pay and deductible tracking
- Refunds and adjustments with audit trail
- Aging reports (A/R by 30/60/90 days)

### 4.5 Pharmacy & Inventory Management
- Drug formulary/catalog (name, dosage forms, NDC codes)
- E-prescribing linked to encounters, with drug interaction warning stubs
- Stock management: batch/lot tracking, expiry alerts
- Purchase orders to suppliers, goods receipt
- Low-stock threshold alerts
- Dispensing log tied to prescriptions (controlled substance tracking flag)

### 4.6 Laboratory & Diagnostics
- Lab test catalog (panel definitions, reference ranges)
- Lab order creation from encounter
- Sample tracking (collected → in-lab → resulted)
- Result entry with automatic flagging (high/low/critical)
- Result attachment (PDF reports, imaging files)
- HL7/FHIR integration stub (architecture-ready extension point)

### 4.7 Reports & Analytics Dashboard
- Executive dashboard: patient volume, revenue trends, appointment utilization
- Clinical dashboard: common diagnoses, prescription patterns
- Financial dashboard: claims approval rate, outstanding A/R, revenue by department
- Exportable reports (PDF via QuestPDF, Excel via ClosedXML)

### 4.8 Notifications
- Appointment reminders (email + SMS, configurable lead time)
- Lab result ready notifications
- Low stock / expiry alerts
- In-app notification center (SignalR real-time push + persisted log)
- Template-driven messaging

### 4.9 Audit Logs & Compliance (HIPAA-oriented)
- Full CRUD audit trail per entity (who, what, when, before/after values)
- Login/access logs including record-view logs (who viewed which patient record)
- Data retention & soft-delete policies
- Role/permission change logs
- Exportable compliance reports

### 4.10 Admin & Role Management
- User management (create/deactivate staff accounts)
- Role & permission matrix editor
- System configuration (branches, departments, working hours, tax rates)
- Audit log viewer with filters
- Feature flags/module toggles

---

## 5. UI/UX Design Plan

### 5.1 Design System & Component Library

| Library | Component richness for ERP | Accessibility | Dev speed | Customization ceiling |
|---|---|---|---|---|
| DaisyUI | Low — styling primitives only, no data grid/scheduler/tree | Depends on implementation | Fast for simple UI, slow for complex components | High (Tailwind classes) |
| Material / Angular Material | Good, but tables/calendars need heavy customization | Excellent (CDK a11y) | Fast for standard CRUD | Medium — fighting Material's look |
| Ant Design | Very strong (Table, Tree, Cascader, DatePicker) | Good | Very fast for admin UIs | Medium — recognizable "enterprise" look |
| **PrimeNG (chosen)** | **Very strong** — Table w/ virtual scroll & lazy loading, full Scheduler, Tree, FileUpload, Chart wrappers | Good, ARIA-based, improving | Very fast for ERP screens | High when paired with Tailwind |

**Decision: PrimeNG (components) + Tailwind CSS (layout/utility/theming).** DaisyUI was evaluated but rejected for this project — it lacks the data-dense, stateful components (data tables, schedulers, trees) that make up the bulk of ERP screen complexity, and building those from scratch on top of DaisyUI would cost more time than it saves.

### 5.2 Color Palette

| Role | Color | Hex | Rationale |
|---|---|---|---|
| Primary | Deep Clinical Teal | `#0F766E` | Medical + calming, less corporate-cold than pure blue |
| Primary Dark (hover/active) | — | `#115E59` | Interactive states, small-text-safe |
| Secondary | Trust Blue | `#2563EB` | Links, secondary actions, informational states |
| Accent/CTA | Warm Amber | `#F59E0B` | Primary buttons (Book Appointment, Submit Claim) |
| Success | — | `#16A34A` | Confirmed appointments, paid invoices, normal lab values |
| Warning | — | `#D97706` | Pending claims, upcoming expirations, borderline lab values |
| Danger/Critical | — | `#DC2626` | Critical lab flags, overdue payments, allergy alerts (reserved for genuinely urgent states) |
| Neutral background | — | `#F8FAFC` | Main app background |
| Surface (cards) | — | `#FFFFFF` | Card/panel backgrounds |
| Text primary | — | `#1E293B` | Softer than pure black |
| Text secondary | — | `#64748B` | Labels, metadata, timestamps |
| Border/divider | — | `#E2E8F0` | Subtle separators |

**Accessibility notes:**
- `#1E293B` on `#F8FAFC` exceeds WCAG AA (4.5:1) for body text
- Verify `#0F766E` contrast for small text; use `#115E59` where needed
- Never rely on color alone for status — pair with icon + text label (colorblind accessibility, ~8% of men affected)
- Support dark mode for night-shift staff: `#0F172A` background, `#E2E8F0` text, status colors adjusted for dark-surface contrast

**Typography:** `Inter` or `IBM Plex Sans`, tabular figures enabled for numeric columns (dosages, invoice amounts).

### 5.3 Key Screen Wireframe Descriptions

**Dashboard (role-adaptive):**
- Top bar: global search (patient/MRN lookup), notification bell, branch switcher, user menu
- Left sidebar: collapsible module nav
- Main area: KPI cards row (today's appointments, pending lab results, revenue MTD, low-stock alerts), then two-column layout: calendar widget + activity feed
- Role-adaptive content (Physician: "My Patients Today"; Admin: financial KPIs; Pharmacist: stock alerts)

**Patient Profile:**
- Header band: photo, name, MRN, age/DOB, allergy badges (always visible, red — safety-critical UX pattern)
- Tabs: Overview | Medical History | Appointments | Prescriptions | Lab Results | Billing | Documents
- Overview tab: vitals trend mini-charts, active problem list, upcoming appointment card

**Appointment Calendar:**
- PrimeNG Scheduler, week view default, doctor-column resource view for admin/reception
- Color-coded by appointment type/status
- Drag-to-reschedule with conflict-check toast
- Quick-create slide-over panel for booking

### 5.4 Responsive & Accessibility Considerations
- Mobile-first for reception/nurse tablet use; desktop-optimized dense tables for back-office
- WCAG 2.1 AA target: 4.5:1 contrast minimum, full keyboard navigation, `aria-live` regions for real-time updates, focus trapping in modals
- Sticky headers, sortable/filterable columns, virtual scrolling (`cdk-virtual-scroll`) for large lists
- Print-friendly stylesheets for invoices and lab reports

---

## 6. Technical Implementation Details

### 6.1 Key Packages

**.NET Backend:**

| Package | Purpose |
|---|---|
| MediatR | CQRS command/query dispatch |
| FluentValidation | Request/command validation |
| Mapster | DTO mapping |
| Npgsql.EntityFrameworkCore.PostgreSQL | EF Core provider |
| Serilog.AspNetCore + Serilog.Sinks.Seq | Structured logging |
| Microsoft.AspNetCore.Authentication.JwtBearer | JWT auth |
| Hangfire | Background jobs |
| QuestPDF | PDF generation |
| ClosedXML | Excel export |
| Microsoft.AspNetCore.SignalR | Real-time notifications |
| Swashbuckle / NSwag | OpenAPI + Angular client generation |
| Testcontainers.PostgreSql | Integration testing |
| Respawn | DB reset between tests |
| xUnit, FluentAssertions, NSubstitute | Testing |

**Angular Frontend:**

| Package | Purpose |
|---|---|
| primeng + primeicons | Component library |
| tailwindcss | Utility styling |
| @microsoft/signalr | Real-time client |
| @ngrx/signals | SignalStore for Billing/Scheduling |
| ngx-charts or chart.js + ng2-charts | Dashboards |
| date-fns | Date handling |
| ngx-toastr | Toast notifications |
| @angular/cdk | Accessibility, overlay, virtual scroll |

### 6.2 API Design Conventions
- RESTful naming: `/api/v1/patients`, `/api/v1/patients/{id}/encounters`, `/api/v1/appointments`
- URL versioning via `Asp.Versioning.Http`
- RFC 7807 `ProblemDetails` for error responses
- Pagination via query params (`?page=1&pageSize=25`) + `X-Total-Count` header
- Minimal API endpoint groups per module via extension methods

```csharp
public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/patients")
            .WithTags("Patients")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetPatientById)
             .WithName("GetPatientById")
             .Produces<PatientDto>(200)
             .Produces(404);

        group.MapPost("/", RegisterPatient)
             .RequireAuthorization("CanRegisterPatients");
    }
}
```

### 6.3 Real-Time Features (SignalR)

| Hub | Use case |
|---|---|
| NotificationHub | In-app notifications (lab results, appointment reminders) |
| AppointmentHub | Live calendar updates to avoid double-booking races |
| InventoryHub | Real-time low-stock alerts to pharmacy dashboard |

Use groups per branch/tenant for multi-tenant scoping.

### 6.4 Testing Strategy

| Layer | Approach | Tools |
|---|---|---|
| Domain unit tests | Test aggregate invariants directly | xUnit, FluentAssertions |
| Application unit tests | Mocked repositories | xUnit, NSubstitute |
| Integration tests | Real Postgres via Testcontainers | Testcontainers, Respawn, WebApplicationFactory |
| API/functional tests | End-to-end HTTP calls | xUnit + HttpClient |
| Frontend unit tests | Component/service logic | Jasmine/Karma or Vitest |
| E2E tests | Registration → appointment → billing flow | Playwright |
| Architecture tests | Clean Architecture dependency rules | NetArchTest |

### 6.5 Deployment Approach
- Docker Compose locally: `api`, `client`, `postgres`, `redis`, `seq`
- CI/CD via GitHub Actions: restore/build → unit tests → integration tests → docker publish → deploy
- Hosting: API on Azure App Service or VPS; frontend on Cloudflare Pages/Vercel/Azure Static Web Apps; DB on managed Postgres (Neon/Supabase)

```yaml
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
```

---

## 7. Portfolio Presentation Strategy

### 7.1 GitHub Repo & README Structure

```
README.md               # Hero section: GIF/screenshot, live demo link, tech badges
docs/
  ARCHITECTURE.md         # Condensed architecture + diagrams (Mermaid)
  API.md                  # Link to Swagger/Scalar UI
  SECURITY.md             # HIPAA-inspired controls implemented
CONTRIBUTING.md
docker-compose.yml
/src, /tests
```

README should lead with: live demo link + test-role credentials, a 30-second GIF walkthrough, architecture diagram, tech stack badges, and a "Why these decisions" section.

### 7.2 Demo Data & Seeding
- `IDataSeeder` run via CLI flag or startup job, using **Bogus**: ~50 patients, 10 doctors across specialties, 200+ historical appointments, mixed-status invoices/claims, pharmacy inventory with intentional low-stock items
- Seed at least one "story-complete" patient (registration → encounters → prescriptions → lab results → invoice → paid claim) for a coherent demo walkthrough

### 7.3 Live Demo Hosting
- Frontend: Vercel/Cloudflare Pages
- API + DB: low-cost VPS or Azure free tier
- Role-based demo logins (Admin / Doctor / Receptionist / Pharmacist)

### 7.4 Key Selling Points to Highlight
1. Domain-driven design applied to a real regulated industry
2. HIPAA-aware engineering (audit trails, record-view logging, encrypted PII)
3. Modular monolith with clean boundaries — scale-ready without premature complexity
4. Real-time UX via SignalR
5. Automated architecture enforcement + test coverage
6. Multi-tenant-ready schema

---

## 8. Full Project Flow (All Phases)

**Phase 0 — Discovery & Requirements (1–2 weeks)**
- Stakeholder interviews mapping real clinical/billing/admin workflows
- Define regulatory scope (HIPAA, and GDPR/PHIPA if applicable)
- Requirements doc with MoSCoW prioritization
- Define non-functional requirements: concurrent users, data volume, uptime SLA, RPO/RTO

**Phase 1 — Architecture & Design (1–2 weeks)** — *see Section 1 for the detailed step-by-step*

**Phase 2 — Core MVP Build (6–10 weeks)**
- Build in vertical slices: complete one module end-to-end before the next
- Priority order: Auth/RBAC → Patient Management → Scheduling → Billing (basic) → Staff Management
- Testing discipline from day one

**Phase 3 — Extended Modules (4–8 weeks)**
- Pharmacy/Inventory, Lab/Diagnostics, Advanced Billing (claims), Notifications
- Begin UAT with real/representative clinical staff on completed modules

**Phase 4 — Hardening & Compliance Validation (2–3 weeks)**
- Security audit / penetration testing
- Performance/load testing
- Accessibility audit (WCAG AA)
- Finalize audit logging, backup/recovery drills, incident response docs

**Phase 5 — Deployment & Go-Live**
- Staged rollout (pilot department/branch first, if applicable)
- Data migration plan and validation if replacing an existing system
- Staff training and documentation
- Monitoring/alerting in place before go-live

**Phase 6 — Maintenance & Iteration (ongoing)**
- Bug triage/response SLA
- Regular security patching cadence
- Quarterly compliance review
- Feature roadmap driven by usage analytics and feedback

**Best practices throughout:**
- Compliance-by-design, not compliance-as-afterthought
- Version everything: API versioning, tracked DB migrations, feature flags
- Document architectural decisions as ADRs as you go

---

## 9. Phased Development Roadmap

| Phase | Scope | Duration (solo dev estimate) |
|---|---|---|
| Phase 1 — MVP | Auth/RBAC, Patient CRUD + basic EHR, Doctor/Staff mgmt, basic Appointment booking, basic Billing, Angular shell + theming | 4–6 weeks |
| Phase 2 — Core ERP | Full appointment calendar w/ SignalR, insurance claims workflow, pharmacy inventory + e-prescribing, lab orders/results, notifications, audit logging | 4–5 weeks |
| Phase 3 — Analytics & Polish | Reports/analytics dashboards, PDF/Excel export, role-adaptive dashboards, responsive/mobile pass, accessibility audit, Playwright E2E suite, CI/CD | 3 weeks |
| Phase 4 — Advanced/Differentiators | Patient portal, HL7/FHIR stub, multi-tenant branch switching, Stripe integration, Hangfire reminders, advanced RBAC editor, deployment + live demo | 3–4 weeks |
| Phase 5 — Showcase Refinement | Realistic demo data, walkthrough video, polished README/docs, case-study blog posts | 1 week |

**Total: ~15–19 weeks part-time**, front-loading Phases 1–2 since that's where most reviewers/clients will actually click around.

---

*Document generated as a working reference for Health ERP project planning. Update as decisions are finalized or revisited during development.*
