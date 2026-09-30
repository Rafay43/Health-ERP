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

## Milestone 6A: Pharmacy & Medication Management (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| Drug & Prescription aggregates | ✅ Done | `Drug`, `Prescription` aggregates + Postgres `pharmacy` schema |
| Inventory & Stock tracking | ✅ Done | Auto-decrement stock on dispense, low-stock & expiry alerts |
| Pharmacy REST Endpoints | ✅ Done | `GET /api/v1/pharmacy/inventory`, `POST /api/v1/pharmacy/prescriptions` |
| Angular Pharmacy Dashboard UI | ✅ Done | Inventory table, prescription modal, dispense action |

---

## Milestone 6B: Laboratory & Diagnostic Module (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| LabTest & LabOrder aggregates | ✅ Done | `LabTest`, `LabOrder` aggregates + Postgres `laboratory` schema |
| Test Catalog & Ordering Workflow | ✅ Done | Catalog management, order placement, priority flagging (STAT/Urgent) |
| Result Entry & Abnormal Flagging | ✅ Done | Result recording with abnormal value highlighting |
| Angular Laboratory Dashboard UI | ✅ Done | Test catalog, pending/urgent orders list, result entry modal |

---

## Milestone 7: Unified Patient EHR & Medical History Timeline (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| Patient EHR Detail Route (`/patients/:id`) | ✅ Done | Comprehensive single-page EHR view |
| Aggregated Clinical History | ✅ Done | Unified timeline for Encounters, Prescriptions, Labs, Billing, Clinical Notes |
| Clinical Note Creation | ✅ Done | Direct clinical note entry attached to patient record |

---

## Milestone 8: Hospital Reports & Analytics Dashboard (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| Multi-Module Aggregated Analytics | ✅ Done | Financial revenue breakdown, patient volume, lab order metrics |
| Export Features | ✅ Done | CSV export capabilities for hospital operational metrics |
| Angular Reports UI | ✅ Done | Visual KPI cards, distribution charts, custom date-range filters |

---

## Milestone 9: Admin Console, Security Audit Logs & RBAC (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| System Configuration Settings | ✅ Done | Tenant settings, feature toggles, security policy config |
| Audit Logging & Activity Viewer | ✅ Done | Centralized audit log tracking with search & filter |
| Role & Permission Management | ✅ Done | User role assignment console for RBAC |

---

## Milestone 10: Hospital Inpatient Ward & Bed Management (ADTI) (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| Bed & Admission Aggregates | ✅ Done | `Bed` (Available, Occupied, Maintenance) & `Admission` aggregates + Postgres `inpatient` schema |
| Stay Duration & Automatic Rate Calculation | ✅ Done | Automatic stay calculation and total daily room charge computation on discharge |
| Angular Inpatient Dashboard UI | ✅ Done | Interactive Ward Bed Allocation Grid, occupancy rate KPIs, admission/discharge modals |

---

## Milestone 11: Emergency Department (ED / Triage) & Casualty Module (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| TriageRecord Aggregate & ESI Severity Levels | ✅ Done | ESI Levels 1–5 (`Resuscitation` to `NonUrgent`) + Postgres `triage` schema |
| Vital Signs Monitoring & Bay Allocation | ✅ Done | Blood Pressure, HR, SpO2, Temp, Resp Rate tracking & ER Trauma Bay assignment |
| Direct Ward Transfer Workflow | ✅ Done | Seamless ER-to-Inpatient Bed transfer with automated admission creation |
| Angular Emergency Dashboard UI | ✅ Done | ESI Triage Queue, vital sign alert highlighting, intake & transfer modals |

---

## Milestone 12: Real-time Notification & Alerting Center (SignalR & In-App Alerts) (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| Notification Aggregate & Severity Levels | ✅ Done | `Notification` aggregate (`Info`, `Warning`, `Critical`) + Postgres `notifications` schema |
| SignalR NotificationHub WebSockets | ✅ Done | `/hubs/notifications` real-time WebSocket push broadcasting to connected clients |
| Persistent Notification CQRS & REST API | ✅ Done | `GET /api/v1/notifications`, `POST /api/v1/notifications/{id}/read` & `read-all` |
| Angular Bell Icon & Floating Notification Drawer UI | ✅ Done | Live unread badge count, severity-coded alert list, click-to-navigate links |

---

## Milestone 13: UI Polish, SweetAlert2 Integration & System Audit Enhancements (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| Automated Notification Triggers | ✅ Done | Auto-emit system notifications on staff creation, appointment booking, and ER triage intake |
| System Audit Trail User Resolution | ✅ Done | Resolved raw User GUIDs in "Performed By" column to human-readable user names/emails via `IIdentityService` |
| SweetAlert2 Integration | ✅ Done | Replaced native alerts with SweetAlert2 popups across all 13 modules (Patients, Staff, Appointments, Billing, Pharmacy, Lab, Inpatient, ER, EHR, Admin, Auth) |
| Angular `AlertService` | ✅ Done | Created `alert.service.ts` helper wrapping SweetAlert2 for consistent success, error, warning & confirm dialogs |
| Search Bar & Form Layout UX Polish | ✅ Done | Fixed search icon overlap padding (`!pl-10`, `z-10`) and patient selector bindings in modal dialogs |
| EF Core Concurrency & Entity Configurations | ✅ Done | Fixed `DbUpdateConcurrencyException` on payments via `.ValueGeneratedNever()` on `Payment.Id` |

---

## Milestone 14: Executive Hospital Management Dashboard (✅ Complete)

| Task | Status | Notes |
|---|---|---|
| CQRS Dashboard Metrics Endpoint | ✅ Done | `GET /api/v1/dashboard/metrics` returning real-time KPIs, revenue trend, bed distribution, and alerts |
| Top Metric KPI Row | ✅ Done | Glassmorphic KPI cards for Bed Occupancy %, Today's Consultations, Monthly Revenue, and ER Intake |
| Financial & Ward Visual Charts | ✅ Done | Monthly revenue comparison bar visualizer & Inpatient Ward bed occupancy allocation breakdown |
| Today's Schedule & ER Watch Feed | ✅ Done | Live consultation timeline with status badges and Emergency Department vital alert watch |
| Quick Action Launcher Bar | ✅ Done | 1-click quick launcher bar to register patients, book appointments, record ER triage, and add prescriptions |

---

*Related documents: `PRD.md`, `TRD.md`, `architecture.md`, `design.md`, `rules.md`, `memory.md`.*


