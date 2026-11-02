PUBLICATION STATUS: NOT READY

# Publication cleanup and validation

Updated **2 November 2026** after making the safe repository changes. Do not publish or push yet: old credentials remain in Git history and must be treated as compromised. No commit, push, visibility change, history rewrite, or external credential rotation was performed.

## Resolution status

| Status | Issue | Outcome |
| --- | --- | --- |
| RESOLVED | Credentials in current application configuration | Database connection and JWT signing secret are empty in tracked base configuration. Startup rejects absent/example connection configuration and missing/short/example signing keys. A sanitized appsettings example and user-secrets instructions keep local setup explicit. |
| RESOLVED | Raw reset-link/OTP logging | Email delivery logs contain operational outcomes only, including failure paths. No reset token, link, OTP, recipient, or SMTP credential is emitted by those logs. SMTP exceptions are not attached to email log messages. Regression tests cover simulation and failure paths. |
| RESOLVED | Fixed seeded passwords and browser demo access | Login fields start empty; demo buttons/default passwords are removed. Development seeding is opt-in, uses private configuration, and refuses non-Development environments. Existing account passwords are not changed. |
| RESOLVED | Generated files tracked in Git | 53 TestResults/TRX files were removed from the index, preserving local copies. Ignore rules cover results, build output, coverage, backups, environment files, and key material. No source or legitimate test fixture was removed. |
| RESOLVED | Clean npm installation failed | PrimeNG 21 required Angular 21 while the app uses Angular 22. PrimeNG is now pinned to compatible 22.1.4 and the lockfile was regenerated without force/legacy peer overrides. Deprecated message text inputs were migrated to projected content; native pButton label/icon uses were migrated to p-button components, preserving labels, submit/loading/disabled behavior, and styling. Login rendering/error assertions cover these changes. |
| RESOLVED | Stale root-component assertion | Replaced the obsolete scaffold heading assertion with a check for the actual RouterOutlet. |
| RESOLVED | Order-dependent frontend failures | SignalR mocks and TestBed state could leak between files with shared execution/chunks. Angular test execution now isolates files and disables code splitting so mocks bind predictably. Existing notification and billing assertions remain. |
| RESOLVED | Login tests depended on insecure defaults | Navigation tests now submit explicit isolated credentials; an additional test verifies empty login state and rejection of blank submissions. |
| REQUIRES MANUAL ACTION | Historical credential exposure | Earlier committed versions still contain database credentials, a JWT signing key, and fixed demo passwords. Coordinate history cleanup separately, then re-scan all refs before publication. No history was rewritten here. |
| REQUIRES MANUAL ACTION | Credential/account rotation | Rotate database credentials and signing keys wherever used; revoke affected sessions, retire/reset old seeded accounts, and review exposed logs/tokens. Do not assume history cleanup alone makes a credential safe. No external changes were made. |
| REQUIRES MANUAL ACTION | Data/privacy review | Inspect ignored/local files, database exports, historical test output, screenshots, backups, and any existing remote copies for patient information or tokens. This is not a forensic certification of every historical object. |
| REMAINING | Production access consistency | Dedicated Doctor Workspace is appointment-scoped; shared patient APIs retain broader role policies, including discharge using CanEditPatients. Review object-level access before operational use. |
| REMAINING | Other production hardening | Static-salt low-entropy OTP hashes, account/IP abuse controls, localStorage token storage, CSP, operational audit/backup/recovery, and migration/role provisioning need a deployment-specific review. |
| REMAINING | Product completeness | General dashboard demonstration fallbacks, concurrent booking constraints, note idempotency, browser end-to-end testing, and the scaffold Contracts project remain future work. |
| REQUIRES MANUAL ACTION | Portfolio finishing | Capture real screens using synthetic data, choose a license, verify artwork provenance, and obtain a green GitHub-hosted CI run after approval to commit/push. |

## Historical files requiring cleanup

Only paths and credential types are recorded; values are intentionally omitted.

| File | Historical sensitive content |
| --- | --- |
| src/Presentation/HealthERP.Api/appsettings.json | PostgreSQL connection credentials and JWT signing secret |
| src/Infrastructure/HealthERP.Infrastructure/Persistence/ApplicationDbContextInitialiser.cs | Fixed privileged/demo passwords |
| health-erp-client/src/app/features/auth/login/login.ts | Fixed privileged/demo passwords and account identifiers |
| src/Infrastructure/HealthERP.Infrastructure/Services/EmailService.cs | Code that logged reset links/tokens and activation codes; review any retained runtime logs |
| tests/*/TestResults/*.trx | Historical generated execution output; review for machine details, identifiers, or tokens before removing historical copies |

The working-file scan covered tracked text files and new source/config/docs. Reachable history was checked with credential patterns and comparisons against the removed values. Pattern matches in isolated test fixtures, documentation placeholders, npm metadata, and disposable CI credentials were distinguished from application secrets. Deliberately fake test keys/passwords remain in isolated tests; they are not application defaults. The CI PostgreSQL password exists only for an ephemeral job service.

No additional live SMTP password, API key, private key, or access token was identified in current tracked source/config. This is a scoped scan, not proof that ignored files, external copies, or unreachable Git objects contain no sensitive data. Do not paste old values into issues, PRs, or cleanup instructions.

## Validation

All commands use disposable test resources. The normal ERP database and running application were not used.

| Check | Result |
| --- | --- |
| .NET restore | Passed |
| .NET Release solution build | Passed; zero warnings/errors |
| Domain tests | 50/50 passed |
| Application tests | 39/39 passed |
| API functional tests | 33/33 passed |
| PostgreSQL integration tests | 363/363 passed |
| Total backend | 485/485 passed; zero skipped |
| npm ci | Passed with the updated lockfile; no force/legacy peer options |
| Angular production build | Passed; existing SweetAlert2 CommonJS warning remains |
| Complete frontend tests | 170/170 passed across 13 files, twice; zero skipped |
| Generated results tracked | 0; 53 index removals |
| Removed credential values in current files | No matches in credential context |

The PostgreSQL 17 cluster was created only for this task under ignored artifacts on port 55492, with trust authentication confined to loopback. The integration fixture created/dropped its own uniquely named databases; the task cluster was stopped after validation. Backend validation added regression cases, so counts exceed the earlier 119 unit/functional and 356 integration results.

The original reported frontend failures were one obsolete heading assertion, nine notification/SignalR cases, and one billing TestBed case. Their order dependence was confirmed by a later run in which notification/billing passed while the heading and ten login cases failed after credential defaults were removed. The login fixtures are now explicit, and file isolation/mock binding is configured rather than relying on execution order. No tests were disabled or selectively excluded and no meaningful assertions were removed.

The repeat full run also includes the new login button/error rendering assertions. Both complete runs passed. The PrimeNG compatibility changes were checked by production compilation and component tests; a real-browser visual walkthrough remains a useful manual check before hosting a demo.

Local tooling initially hit sandbox network restrictions and a low-space C: drive. Dependency caches, logs, build outputs, test results, and disposable PostgreSQL data were placed under ignored F: artifacts. These environment issues are separate from application/test correctness. A fresh npm installation warned that the existing @primeng/themes package is deprecated; migration to @primeuix/themes is follow-up work. GitHub-hosted workflow execution has not been verified locally.

## Local configuration

Follow the root README. Store database/JWT/SMTP and DevelopmentSeed passwords in user secrets or process environment variables. The example JSON contains placeholders only and is not loaded automatically. Do not copy usable credentials into tracked files. Simulation never reveals activation codes/reset links in logs; use a private capture SMTP server to exercise those flows.

Development still applies migrations and creates roles. Accounts/catalogs require DevelopmentSeed:Enabled plus an explicitly configured AdminEmail/AdminPassword; DoctorEmail/DoctorPassword are optional as a pair. Disabling the opt-in does not delete existing data/accounts. Never run synthetic seeding against clinical data.

## GitHub presentation

**Repository description:** Full-stack hospital ERP with ASP.NET Core, Angular, and PostgreSQL: clinical workflows, patient self-service, verified doctor access, and concurrency-tested transactions.

**Topics:** healthcare, hospital-management, erp, aspnet-core, dotnet, angular, typescript, postgresql, entity-framework-core, clean-architecture, jwt-authentication, signalr, integration-testing.

**Pinned positioning:** Health ERP — Full-stack clinical workflows and transaction integrity. Pin first; lead with explicit staff identity, appointment-scoped access, atomic activation, and real PostgreSQL concurrency tests.

Keep older stabilization reports clearly dated; their historical failures/counts are not current results. Add a license intentionally, curate documentation, and replace duplicate/unverified artwork only after checking provenance.

## CI

The workflow restores/builds/tests .NET 10 against an ephemeral PostgreSQL 17 service and installs/builds/tests Angular in a separate job. Repository permissions are read-only and checkout credentials are not persisted. There is no deployment or use of application/production secrets. Action versions follow the official [checkout](https://github.com/actions/checkout), [setup-dotnet](https://github.com/actions/setup-dotnet), and [setup-node](https://github.com/actions/setup-node) documentation; reviewed full SHA pins are possible follow-up hardening.

**Publication remains NOT READY until manual history cleanup, credential rotation, and privacy review are complete.**
