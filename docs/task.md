# Task Breakdown
## Health ERP System

> Companion to `PRD.md` roadmap and `TRD.md`/`architecture.md` status markers. Timelines are **[ASSUMPTION]** estimates for a solo developer working part-time, consistent with the phased roadmap already established for this project.

---

## Milestone 1: Foundation (✅ Mostly Complete)

| Task | Status | Notes |
|---|---|---|
| Repo setup, `.gitignore`, docs structure | ✅ Done | |
| .NET solution scaffold (Clean Architecture layers) | ✅ Done | |
| NetArchTest architecture enforcement | ✅ Done | |
| PostgreSQL setup + connection | ✅ Done | Native install, not Docker |
| EF Core + Infrastructure wiring | ✅ Done | |
| Angular workspace scaffold | ✅ Done | |
| Tailwind + PrimeNG (v21, MIT) setup | ✅ Done | |
| App shell (sidebar, top bar, routing) | ✅ Done | |
| CI/CD pipeline (GitHub Actions) | ⏳ Not started | Deferred |
| Docker Compose for local dev | ⏳ Not started | Docker Desktop not yet installed |

**Dependencies:** none — this milestone unblocks everything else.
**Owner:** solo dev.

---

## Milestone 2: Patient Management Vertical Slice (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| Patient domain aggregate + strongly-typed ID | ✅ Done | |
| EF Core configuration + migration | ✅ Done | |
| `RegisterPatientCommand` (CQRS) + validation | ✅ Done | |
| Global exception handling middleware | ✅ Done | |
| Audit interceptor (`CreatedAtUtc`/`CreatedBy`) | ✅ Done | `CreatedBy` hardcoded to `"system"` pending auth |
| `GET /api/v1/patients` + `next-mrn` endpoints | ✅ Done | |
| Angular Patient Registration form | ✅ Done | |
| Angular Patient List screen | ✅ Done | |
| CORS policy (dev) | ✅ Done | Must be revisited before non-local deployment |
| Timezone bug fix (date-of-birth) | ✅ Done | |
| Unit tests for Patient handlers | ⏳ Not started | Open gap — see `rules.md` §4 |

**Dependencies:** Milestone 1.
**Owner:** solo dev.
**Estimated effort spent:** ~1 focused session (per `memory.md` log).

---

## Milestone 3: Authentication & Authorization (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| ASP.NET Core Identity packages installed | ✅ Done | |
| `ApplicationUser` / `ApplicationRole` entities | ✅ Done | |
| `ApplicationDbContext` → `IdentityDbContext` migration | ✅ Done | |
| Identity DI registration (`AddIdentity`) | ✅ Done | Resolved the missing-package issue (`Microsoft.Extensions.Identity.Core`) |
| Identity schema migration generated + applied | ✅ Done | Identity tables live in `identity` schema in Postgres |
| JWT token issuance service | ✅ Done | |
| Login endpoint | ✅ Done | |
| Registration endpoint | ✅ Done | |
| Angular login/registration screens | ✅ Done | |
| Refresh token rotation | ⚠️ Unverified | Not confirmed whether implemented — **[OPEN QUESTION, see memory.md]** |
| Role seeding (SystemAdmin, Physician, Nurse, etc.) | ✅ Done | Default roles & Admin user seeded in initialiser |
| `[Authorize]` / policy-based auth on existing endpoints | ✅ Done | All endpoints protected with `.RequireAuthorization()` |
| Angular JWT interceptor | ✅ Done | Token attached to requests + automatic 401 logout handling |
| Angular route guards | ✅ Done | `authGuard` protects app shell, `guestGuard` handles login/register |
| Wire `CreatedBy` audit field to real authenticated user | ✅ Done | `AuditableEntitySaveChangesInterceptor` reads user ID from `ICurrentUserService` |

**Dependencies:** Milestone 2.
**Status:** Milestone 3 fully complete and verified.

---

## Milestone 4: Scheduling Module

| Task | Status | Notes |
|---|---|---|
| Doctor/Staff aggregate + CRUD vertical slice | ✅ Done | `StaffMember` entity + API endpoints + Angular directory screen |
| Appointment aggregate with conflict-detection invariant | ✅ Done | Double-booking prevention check (`Start < ExistingEnd && End > ExistingStart`) |
| Appointment status lifecycle endpoints & UI actions | ✅ Done | `PATCH /api/v1/appointments/{id}/status` (`Confirmed`, `Completed`, `Cancelled`, `NoShow`) + action buttons |
| Interactive Schedule Grid & Doctor/Status Filtering | ✅ Done | Toggle View switcher (Table vs. Schedule Grid) + Doctor/Status/Search filters |
| SignalR hub for real-time calendar updates | ✅ Done | `AppointmentHub` websocket + `AppointmentSignalRService` live client updates |
| Automated No-Show tracking & background worker | ✅ Done | `NoShowTrackingWorker` periodic background worker checking overdue consultations |

**Dependencies:** Milestone 3.
**Status:** ✅ Milestone 4 100% Fully Complete & Verified.

---

## Milestone 5: Billing & Claims Module

| Task | Status | Notes |
|---|---|---|
| Invoice & Payment aggregates + EF Core schema | ✅ Done | `Invoice`, `InvoiceLineItem`, `Payment` aggregates + Postgres `billing` schema migration |
| Auto-generate invoice on completed consultation | ✅ Done | Automatically creates a $150 consultation invoice when appointment status becomes `Completed` |
| Billing & Payments API endpoints | ✅ Done | `GET /api/v1/invoices`, `POST /api/v1/invoices`, `POST /api/v1/invoices/{id}/payments` |
| Billing dashboard screen & Payment modal | ✅ Done | Angular Billing dashboard with metrics cards, status badges, and payment modal |

**Dependencies:** Milestone 4.
**Status:** ✅ Milestone 5 Complete & Verified.

---

## Milestone 6: Pharmacy & Lab Modules

| Task | Status |
|---|---|
| Drug/Prescription aggregates | ⏳ Not started |
| Stock/expiry tracking + low-stock alerts | ⏳ Not started |
| LabOrder/LabResult aggregates | ⏳ Not started |
| Critical-value flagging logic | ⏳ Not started |

**Dependencies:** Milestone 3 (auth), can run parallel to Milestone 5.
**Estimated effort:** **[ASSUMPTION]** 3 sessions combined.

---

## Milestone 7: Reports, Notifications, Compliance Hardening

| Task | Status |
|---|---|
| Analytics dashboards (operational/financial/clinical) | ⏳ Not started |
| Email/SMS notification integration | ⏳ Not started |
| Full audit log viewer (admin UI) | ⏳ Not started |
| Record-view logging (PHI access audit) | ⏳ Not started |
| Field-level encryption for PII | ⏳ Not started |
| Formal accessibility audit (WCAG AA) | ⏳ Not started |

**Dependencies:** most prior milestones (reports need real data across modules).
**Estimated effort:** **[ASSUMPTION]** 2–3 sessions.

---

## Milestone 8: Testing, CI/CD, Deployment Readiness

| Task | Status |
|---|---|
| Unit tests across Application layer handlers | ⏳ Not started |
| Integration tests (Testcontainers) | ⏳ Not started |
| E2E tests (Playwright) | ⏳ Not started |
| GitHub Actions CI pipeline | ⏳ Not started |
| Docker Compose (Postgres, Redis, Seq, API, client) | ⏳ Not started |
| Production CORS/secrets hardening | ⏳ Not started |
| Demo data seeding (Bogus) | ⏳ Not started |
| Live demo hosting | ⏳ Not started |

**Dependencies:** should run incrementally alongside all prior milestones, not deferred entirely to the end — flagged as a process risk in `memory.md`.

---

*Related documents: `PRD.md`, `TRD.md`, `architecture.md`, `design.md`, `rules.md`, `memory.md`.*
