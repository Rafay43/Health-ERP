# Development Rules
## Health ERP System

> Companion to `TRD.md` and `architecture.md`. Governs coding standards, branching, review, and testing for this repo.

**[ASSUMPTION]** This project is currently solo-developed; the branching/review process below is written as if for a small team, since the repo is explicitly intended as a professional portfolio piece and should reflect team-ready practices even while worked solo.

---

## 1. Coding Standards

### 1.1 C# / .NET
- **Nullable reference types enabled** project-wide (`Directory.Build.props`)
- **Implicit usings enabled**
- Follow Clean Architecture dependency direction strictly: `Domain` has zero dependencies; violations are caught automatically by the NetArchTest suite (`ArchitectureTests.cs`) — a failing architecture test blocks merge
- Domain entities are never publicly constructible — use private constructors + static factory methods (e.g., `Patient.Register(...)`) so invariants are enforced at creation, not scattered across callers
- Strongly-typed IDs (`readonly record struct`) for every aggregate root — never raw `Guid` in method signatures
- CQRS: one command/query per file, colocated with its validator and handler under `Application/{Module}/Commands|Queries/{Name}/`
- All commands/queries pass through the `ValidationBehavior` MediatR pipeline — do not hand-roll validation inside handlers
- Naming: `PascalCase` for types/methods, `camelCase` with `_underscorePrefix` for private fields (enforced via `.editorconfig`)
- No commercial-licensed dependencies without an explicit, documented decision (see `memory.md` for precedent — FluentAssertions and PrimeNG both required a swap/pin due to licensing changes)

### 1.2 TypeScript / Angular
- Standalone components only — no `NgModule`-based components in new code
- Prefer Angular **signals** for local component state; reserve NgRx SignalStore for genuinely complex, multi-step state (Billing, Scheduling per `TRD.md`)
- One feature folder per module under `features/`, with a `data-access/` subfolder for services and models — mirrors the backend's CQRS-per-module organization
- All HTTP calls go through a dedicated `*.service.ts` — components never call `HttpClient` directly
- Reactive Forms only (no template-driven forms) for anything beyond a single input
- Global date-handling rule: **never use `.toISOString()` for date-only values** — it performs a UTC conversion that can shift the date by one day depending on the user's timezone offset. Build date strings from local date parts instead (see the date-of-birth bug fix in `memory.md`)

### 1.3 General
- All new files created via direct file write/PowerShell `Set-Content` rather than editor copy-paste where a previous paste has silently dropped content (see `memory.md` — this happened twice with an HTML tag during shell development). When in doubt, verify file contents with `Get-Content` before assuming a save succeeded.
- Every third-party package addition that could carry a license or cost implication gets a one-line note in `TRD.md` §2 and a decision entry in `memory.md`.

---

## 2. Branching Strategy

**[ASSUMPTION]** No branching strategy was previously defined; proposing a lightweight trunk-based approach suited to a small team / solo dev:

- `main` — always deployable; direct commits discouraged even solo, prefer feature branches + PR for anything non-trivial
- `feature/{module}-{short-description}` — e.g., `feature/patients-registration-form`
- `fix/{short-description}` — for bug fixes
- No long-lived `develop` branch — `main` is the integration branch, kept green via CI (once CI is set up per `task.md`)

**Commit message convention:** short imperative summary line (e.g., `Add patient registration form and CORS policy`), consistent with the commit history already established in this repo.

---

## 3. Code Review Process

**[ASSUMPTION — aspirational, since currently solo]**
- Every feature branch opens a PR against `main`, even for solo work — creates a reviewable diff and a natural point to run the architecture test + build before merge
- PR description should note: what changed, why, and any known limitations (following the pattern already used in this project's own documentation — e.g., flagging the MRN race-condition simplification explicitly rather than silently shipping it)
- Minimum review checklist before merge:
  - [ ] Builds clean (`dotnet build` / `ng build`)
  - [ ] Architecture test passes (`dotnet test tests/HealthERP.Domain.UnitTests`)
  - [ ] No secrets committed (connection strings, API keys)
  - [ ] New third-party dependencies checked for license compatibility

---

## 4. Testing Requirements

| Layer | Tool | Current Status |
|---|---|---|
| Architecture enforcement | NetArchTest | ✅ In place (Domain has zero outward dependencies) |
| Domain unit tests | xUnit + Shouldly | ⏳ Not yet written beyond architecture test |
| Application unit tests | xUnit + Shouldly (mocked `IApplicationDbContext`) | ⏳ Not yet written |
| Integration tests | Testcontainers (real Postgres) | ⏳ Not yet written |
| Frontend unit tests | Jasmine/Karma or Vitest (Angular CLI default) | ⏳ Not yet written |
| E2E tests | Playwright | ⏳ Not yet set up |

**Rule going forward:** every new CQRS command/query handler should ship with at least one unit test covering its happy path and its primary validation failure path, before being considered "done" — this has not been consistently followed so far (see `memory.md` open questions) and should be corrected as testing infrastructure is built out per `task.md`.

**Testing library note:** FluentAssertions was replaced with **Shouldly** across all test projects due to FluentAssertions v8+ requiring a commercial license for commercial use. Do not reintroduce FluentAssertions without revisiting that decision.

---

*Related documents: `PRD.md`, `TRD.md`, `architecture.md`, `design.md`, `task.md`, `memory.md`.*
