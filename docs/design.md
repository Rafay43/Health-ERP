# Design Document
## Health ERP System — UI/UX Principles & Style Guide

> Companion to `TRD.md` and `architecture.md`. Covers the visual/interaction language used across the Angular frontend.

---

## 1. Design Principles

1. **Clarity over decoration.** Clinical and billing staff use this system under time pressure; every screen should be scannable in seconds. No unnecessary animation, no decorative imagery competing with data.
2. **Safety-critical information is never subtle.** Allergy flags, critical lab values, and overdue-payment states use color *and* icon *and* text label — never color alone (colorblind accessibility, and reduces misreading under stress).
3. **Data density where it's earned.** List/table screens (patient lists, appointment calendars) favor PrimeNG's dense table components; form screens stay generously spaced and single-column where possible to reduce input error.
4. **Consistency over novelty.** One card style, one button hierarchy, one spacing scale — reused everywhere rather than bespoke treatments per screen.

---

## 2. Component Library

**Decision:** PrimeNG (components) + Tailwind CSS (layout, spacing, custom utility styling).

**PrimeNG version note:** Pinned to **v21.x** — PrimeTek introduced a commercial "PrimeUI" license starting v22; v21 is the last permanently MIT-licensed release. See `memory.md` for the full decision log.

**Why this pairing:** PrimeNG supplies the data-dense, stateful components an ERP genuinely needs out of the box (Table with virtual scroll/lazy loading, Scheduler, Tree, DatePicker, FileUpload) that a utility-only library like DaisyUI doesn't provide. Tailwind handles everything else — layout, spacing, custom one-off styling — so the app doesn't read as a generic out-of-the-box admin template.

**Components in active use:**
| Component | Module | Purpose |
|---|---|---|
| `p-button` | Global | Primary actions |
| `p-table` | Patients (built), future: all list screens | Data grids |
| `p-inputtext` | Forms | Text inputs |
| `p-datepicker` | Forms | Date selection |
| `p-message` | Forms | Inline error/success messaging |
| `p-avatar`, `p-badge` | Shell top bar | User avatar, notification count |

**Planned additions:** `p-scheduler` (Appointments), `p-toast` (global notifications), `p-tree` (org hierarchy), `p-fileupload` (document attachments).

---

## 3. Style Guide

### 3.1 Color Palette

| Role | Hex | Usage |
|---|---|---|
| Primary | `#0F766E` | Brand color, active nav state, primary buttons |
| Primary Dark | `#115E59` | Hover/active states |
| Secondary | `#2563EB` | Links, informational states |
| Accent | `#F59E0B` | High-emphasis CTAs |
| Success | `#16A34A` | Confirmed states, normal values |
| Warning | `#D97706` | Pending states, borderline values |
| Danger | `#DC2626` | Critical flags, allergy alerts, overdue states — reserved for genuine urgency |
| Background | `#F8FAFC` | App shell background |
| Surface | `#FFFFFF` | Cards, panels |
| Text Primary | `#1E293B` | Body text |
| Text Secondary | `#64748B` | Labels, metadata |
| Border | `#E2E8F0` | Dividers |

Implemented as CSS custom properties in `styles.css`, consumed via Tailwind arbitrary-value syntax (`text-[var(--color-primary)]`) throughout components.

**Dark mode:** planned but not yet implemented; PrimeNG's theme system supports a `darkModeSelector` config already wired (`.dark-mode` class), token overrides not yet written.

### 3.2 Typography

**[ASSUMPTION — not yet implemented in code]** `Inter` or `IBM Plex Sans`, loaded via Google Fonts, with tabular figures enabled for numeric table columns. Current implementation uses browser default sans-serif; font loading to be added.

### 3.3 Spacing & Layout

- Tailwind's default spacing scale used throughout (no custom scale defined)
- App shell: fixed sidebar (`w-64` expanded / `w-16` collapsed) + fluid main content area
- Forms: `max-w-xl` single-column layout, `gap-4` between fields
- Tables: full-width within content area, horizontal scroll on overflow rather than column truncation

---

## 4. Key Screen Descriptions

### 4.1 App Shell (✅ Built)
- Collapsible left sidebar with icon + label nav items for all 9 planned modules (Dashboard, Patients, Appointments, Billing, Pharmacy, Laboratory, Staff, Reports, Admin)
- Top bar: search placeholder (not yet functional), notification bell with badge count, user avatar
- Active route highlighted in sidebar via `routerLinkActive`

### 4.2 Dashboard (Placeholder — not yet built out)
**[ASSUMPTION]** Planned layout per `PRD.md`: KPI card row (today's appointments, pending lab results, revenue MTD, low-stock alerts), followed by a two-column layout — calendar widget (left) + activity feed (right). Role-adaptive content once auth/roles are complete.

### 4.3 Patient Registration (✅ Built)
- Single-column form: Medical Record Number (auto-suggested, read-only, greyed out), First Name, Last Name, Date of Birth (PrimeNG DatePicker)
- Inline validation messages below each field on blur/touch
- Submit button shows loading state during the API call
- Server-side errors (duplicate MRN, validation failures) surface via a `p-message` banner at the top of the form

### 4.4 Patient List (✅ Built)
- `p-table` with columns: MRN, First Name, Last Name, Date of Birth
- "Register Patient" button routes to the registration form
- Empty state message when no patients exist yet

### 4.5 Appointment Calendar (Not yet built)
**[ASSUMPTION]** Per `PRD.md`: PrimeNG Scheduler, week view default, doctor-column resource view for reception/admin. Color-coded by appointment status. Drag-to-reschedule with conflict-check feedback.

### 4.6 Patient Profile Detail (Not yet built)
**[ASSUMPTION]** Header band with photo placeholder, name, MRN, age/DOB, and prominent allergy badges. Tabbed body: Overview, Medical History, Appointments, Prescriptions, Lab Results, Billing, Documents.

---

## 5. Accessibility

- WCAG 2.1 AA is the target standard (not yet formally audited)
- Color contrast: primary text/background combinations verified to meet 4.5:1; PrimeNG component-level contrast inherited from theme, not independently audited yet
- Status/urgency information paired with icon + text, never color alone (design principle, enforcement pending as more screens are built)
- Keyboard navigation and `aria-live` regions: relies on PrimeNG's built-in ARIA support; no custom accessibility testing performed yet

**Open item:** a formal accessibility audit pass is not yet scheduled — see `task.md`.

---

*Related documents: `PRD.md`, `TRD.md`, `architecture.md`, `rules.md`, `task.md`, `memory.md`.*
