# Product Requirements Document (PRD)
## Health ERP System

> **Note on scope:** This PRD documents the Health ERP system as already in active development (.NET 10 API + Angular frontend, Clean Architecture, PostgreSQL). Where the original brief didn't specify details, reasonable assumptions are made and flagged inline with **[ASSUMPTION]**.

---

## 1. Product Vision

Health ERP is a modular, enterprise-grade practice management system for small-to-mid-sized clinics and multi-branch healthcare providers. It unifies patient records, scheduling, billing, pharmacy, and diagnostics into a single system of record, replacing the fragmented spreadsheets-plus-legacy-software stack many clinics still run on.

**[ASSUMPTION]** Primary go-to-market: initially built as a portfolio/reference implementation, positioned toward small clinics and multi-branch outpatient practices (not hospital-scale inpatient systems) as the first addressable segment.

**Vision statement:** Give clinical and administrative staff one system that is fast enough for front-desk use, rigorous enough for compliance, and open enough to integrate with the tools a clinic already relies on.

---

## 2. Target Audience

| Persona | Role | Primary Needs |
|---|---|---|
| **Front Desk / Receptionist** | Non-clinical | Fast patient lookup/registration, appointment booking, check-in |
| **Physician / Nurse** | Clinical | Patient history at a glance, e-prescribing, encounter documentation |
| **Billing Clerk** | Administrative | Invoice generation, insurance claim tracking, payment reconciliation |
| **Pharmacist** | Clinical/Operational | Prescription fulfillment, stock/expiry tracking |
| **Lab Technician** | Clinical | Order intake, result entry, critical-value flagging |
| **Clinic Administrator** | Administrative | Staff scheduling, reporting, system configuration, compliance oversight |
| **System Admin** | Technical/Ops | User/role management, audit review, system health |

**[ASSUMPTION]** No patient-facing portal in v1 — this PRD covers internal staff-facing tooling only. A patient portal is listed under Future Roadmap.

---

## 3. Core Features

### 3.1 Patient Management
Central patient registry with demographics, insurance details, and a longitudinal medical history.
- Registration with auto-generated, sequential Medical Record Number (MRN)
- Problem list, allergies, immunization history, vitals trend
- Document attachments (scanned IDs, referral letters)
- Soft-delete with full audit trail (no hard deletes of clinical data)

### 3.2 Doctor / Staff Management
- Staff profiles with specialty, license number, credential expiry tracking
- Department/branch assignment
- Shift and leave management

### 3.3 Appointment & Scheduling
- Calendar-based booking (day/week/month), per-doctor and per-department views
- Conflict detection at the domain level (no double-booking)
- Real-time updates across concurrent front-desk sessions (SignalR)
- No-show tracking, automated reminders

### 3.4 Billing & Invoicing
- Service/procedure catalog with pricing
- Invoice generation from completed encounters
- Insurance claim lifecycle (Draft → Submitted → Adjudicated → Paid/Denied)
- Payment processing integration, partial payments

### 3.5 Pharmacy & Inventory
- Drug formulary, batch/lot and expiry tracking
- E-prescribing tied to encounters
- Low-stock alerting

### 3.6 Laboratory & Diagnostics
- Lab order creation from encounters, sample tracking, result entry
- Automatic flagging of out-of-range/critical results

### 3.7 Reports & Analytics
- Operational dashboards (volume, utilization), financial dashboards (A/R, claims), clinical dashboards (diagnosis trends)

### 3.8 Notifications
- Appointment reminders, lab-result-ready alerts, low-stock alerts (email/SMS + in-app)

### 3.9 Audit & Compliance
- Full CRUD audit trail per entity, record-view logging for PHI access, exportable compliance reports

### 3.10 Admin & Role Management
- RBAC-based user/role administration, system configuration (branches, departments, working hours)

---

## 4. User Stories

**Patient Management**
- As a receptionist, I want to register a new patient with an auto-suggested MRN so I don't have to invent or track numbering manually.
- As a physician, I want to see a patient's allergy list prominently on their profile so I don't prescribe something contraindicated.

**Scheduling**
- As a receptionist, I want to see a doctor's live calendar so I can book an appointment without double-booking.
- As a patient-facing staff member, I want automatic reminders sent before appointments to reduce no-shows.

**Billing**
- As a billing clerk, I want to generate an invoice directly from a completed encounter so line items match what was actually performed.
- As a billing clerk, I want to track an insurance claim's status so I know when to follow up.

**Pharmacy**
- As a pharmacist, I want to be alerted when a drug's stock falls below a threshold so I can reorder before running out.

**Compliance**
- As a system admin, I want a log of who viewed a given patient's record so I can respond to a compliance audit.

---

## 5. Success Metrics

**[ASSUMPTION]** No client-specific KPIs exist yet since this is pre-launch; the following are proposed target metrics once the system has real users:

| Metric | Target |
|---|---|
| Patient registration time (front desk) | < 90 seconds per new patient |
| Appointment booking conflicts | 0 (enforced at domain level) |
| API p95 response time | < 300ms for standard CRUD endpoints |
| System uptime | 99.5%+ (post-launch) |
| Audit log coverage | 100% of PHI read/write operations |
| Duplicate MRN incidents | 0 |

---

## 6. Future Feature Roadmap

| Phase | Features |
|---|---|
| **Now (v1 in progress)** | Patient Management, Auth (JWT/Identity), core Scheduling |
| **Phase 2** | Billing & Claims, Pharmacy, Lab module |
| **Phase 3** | Reports/Analytics, Notifications, Audit/Compliance dashboard |
| **Phase 4** | Patient self-service portal, HL7/FHIR integration, multi-tenant branch switching, Stripe payments |
| **Phase 5** | Mobile companion app, telemedicine module **[ASSUMPTION — not yet scoped]** |

---

*Related documents: `TRD.md` (technical requirements), `architecture.md` (system architecture), `design.md` (UI/UX), `rules.md` (dev standards), `task.md` (task breakdown), `memory.md` (decision log).*
