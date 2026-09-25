# Project Memory Log
## Health ERP System

> Running log of key decisions, assumptions, open questions, and lessons learned. Update this file as the project progresses — it's the source of truth for "why did we do it this way" questions later.

---

## 1. Key Decisions

| Date/Session | Decision | Rationale |
|---|---|---|
| Planning | Modular Monolith over Microservices | Avoids distributed-transaction complexity for a domain that needs strong relational consistency; module boundaries kept clean enough to extract later if needed |
| Planning | PostgreSQL over MongoDB/MySQL | Health ERP's core complexity is relational integrity (patient↔encounter↔prescription↔claim); JSONB gives flexibility where genuinely needed without sacrificing FK constraints elsewhere |
| Planning | PrimeNG + Tailwind over DaisyUI | ERP screens are data-dense (tables, schedulers); DaisyUI provides styling primitives only, not the complex stateful components needed |
| Build session | **FluentAssertions → Shouldly** | FluentAssertions v8+ requires a commercial license for commercial use; Shouldly is MIT-licensed with near-identical syntax. Applied across all four test projects. |
| Build session | **PrimeNG pinned to v21.x** | PrimeTek introduced a commercial "PrimeUI" license starting v22 — discovered when a fresh `npm install primeng` pulled v22 and rendered an "Invalid PrimeUI License" watermark. v21 is the last permanently MIT-licensed release. Installed with `--force` due to a peer dependency gap (PrimeNG 21 expects Angular ^21; project is on Angular 22) — functional but a version mismatch to monitor. |
| Build session | MRN generation: count-based suggestion, not a DB sequence | Simpler to implement immediately; known race-condition risk under concurrent registrations, explicitly deferred rather than over-engineered before it's needed |
| Build session | `CreatedBy` audit field hardcoded to `"system"` | Auth isn't built yet; revisit once JWT claims provide a real authenticated user identity |
| Build session | Native PostgreSQL install instead of Docker | Discovered mid-session that Docker Desktop was never actually installed (all earlier `docker run` commands had been failing silently as "command not found"); pragmatically switched to a native install rather than blocking progress on Docker setup |
| Build session | Global stylesheet renamed `.scss` → `.css` | Tailwind v4's `@import "tailwindcss"` syntax needs plain CSS/PostCSS processing; routing it through the Sass compiler triggered deprecation warnings for no benefit since no Sass-specific features were in use |
| Post-handoff | Login and registration (JWT + ASP.NET Core Identity) completed | Resolved the `AddIdentity` missing-package issue (needed `Microsoft.Extensions.Identity.Core` alongside `Microsoft.AspNetCore.Identity.EntityFrameworkCore`); Identity schema migration applied; login/register endpoints and matching Angular screens built |
| Post-handoff | Switched primary dev tool to **Antigravity AI** for continued work | Decision made once the documentation suite (`PRD.md` through `memory.md`) was in place, specifically so a new tool could pick up context from these docs rather than needing everything re-explained |

---

## 2. Assumptions Made (Flagged Throughout Documentation)

- No patient-facing portal in v1 — internal staff tooling only (see `PRD.md`)
- No formal HIPAA certification target — HIPAA-*inspired* controls only, not an audited compliance claim (see `TRD.md` §5)
- Branching/review process written as team-ready even though currently solo-developed (see `rules.md`)
- All milestone timelines in `task.md` are rough solo-developer estimates, not committed deadlines
- Font stack (`Inter`/`IBM Plex Sans`) specified in the original design plan but not yet actually implemented in code — current build uses browser default sans-serif
- Notification provider (email/SMS) not yet selected — SendGrid/Twilio proposed as candidates, not confirmed

---

## 3. Open Questions

- **Auth completeness:** login and registration are confirmed done, but it's not yet confirmed whether refresh token rotation, role seeding, `[Authorize]`/policy enforcement on existing endpoints (e.g., Patients), the Angular JWT interceptor, route guards, and the `CreatedBy` audit-field wiring were completed as part of that work or remain outstanding. **Action:** verify each explicitly before treating Milestone 3 as fully closed or starting Milestone 4 (Scheduling) on the assumption that authorization is enforced everywhere it should be.

- **MRN generation:** when should the count-based approach be replaced with a proper Postgres sequence? Low priority while single-user/dev, but must be resolved before any multi-user concurrent testing.
- **CORS policy:** what should the production allow-list look like once there's an actual deployed frontend origin? Currently hardcoded to `localhost:4200`.
- **Multi-tenancy:** the original architecture blueprint recommended a `TenantId` on aggregates from day one — this was **not** actually added to the `Patient` aggregate as built. Decide whether to retrofit it now (cheaper) or defer until multi-branch support is actually needed (risk: expensive migration later).
- **Docker:** still not installed in the dev environment. Needs to happen before the `docker-compose.yml` local-dev environment (Postgres + Redis + Seq) described in the original architecture blueprint can be realized.
- **Testing debt:** no unit/integration tests exist yet beyond the architecture enforcement test. At what point does this become a blocker for adding new features safely?

---

## 4. Lessons Learned

- **Verify file writes, don't assume they succeeded.** Twice during this session, a pasted HTML file (`shell.html`) silently lost its opening `<a>` tag — the editor/paste mechanism dropped a line without any error. The fix each time was to directly inspect file contents via `Get-Content` rather than trust that a paste operation worked, and eventually to write the file directly via PowerShell `Set-Content` to bypass the editor entirely. **Takeaway:** when a template/compile error doesn't match what the code "should" say, check the actual file on disk before debugging further upstream.
- **Restart the process after config changes.** Multiple CORS and DI issues traced back to `Program.cs` being edited while `dotnet run` was still running the old build — ASP.NET Core doesn't hot-reload `Program.cs` changes without `dotnet watch`. **Takeaway:** always restart the API process after any `Program.cs`/DI registration change before assuming a fix didn't work.
- **Check licensing on every new dependency, not just at project start.** Both FluentAssertions and PrimeNG changed their licensing *after* this project's original architecture blueprint was written, and both were only caught because of runtime symptoms (a license warning in test output, a watermark on screen) rather than proactive checking. **Takeaway:** re-verify license terms at the moment of `npm install`/`dotnet add package`, not just once during initial planning — vendors can and do change terms.
- **Date handling needs explicit UTC-awareness.** `.toISOString()` on a date-only value silently shifted dates by one day for a user in a UTC+5 timezone. **Takeaway:** any date-only (not datetime) value crossing the JS Date → string boundary needs to be built from local date parts, never through a UTC-converting method.
- **PowerShell isn't CMD.** Several early commands (`dir /s /b`, `curl -H`) failed because they were CMD/bash syntax rather than PowerShell's actual cmdlets (`Get-ChildItem -Recurse`, `Invoke-RestMethod`/`Invoke-WebRequest`). **Takeaway:** default to PowerShell-native syntax first in a Windows + PowerShell environment rather than assuming cross-shell command compatibility.
- **CORS registration needs both halves.** `AddCors()` in the DI container without a corresponding `app.UseCors()` in the middleware pipeline silently does nothing — no error, just a browser-side CORS failure with no server-side clue. **Takeaway:** when troubleshooting CORS, check both the service registration *and* the middleware pipeline activation, not just one.

---

*Related documents: `PRD.md`, `TRD.md`, `architecture.md`, `design.md`, `rules.md`, `task.md`. Keep this file updated as the single most valuable document for anyone (including future-you) trying to understand why the project looks the way it does.*
