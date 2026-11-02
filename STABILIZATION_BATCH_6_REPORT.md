# Stabilization Batch 6 — Patient Activation Concurrency & OTP Integrity

Implemented and automatically verified on 2026-10-13. Scope is Patient account activation, its email outcome, activation UI, and focused tests. Existing changes from earlier batches were preserved. No migration was created or required. Manual browser scenarios below remain for operator verification; automated HTTP/database testing was performed.

## A. Root causes fixed

Start/resend previously read cooldown before any serialization, allowing simultaneous issuances. Looking only at unconsumed challenges discarded issuance history after consumption/exhaustion. Verification selected an older pending challenge when a newer challenge was unusable, incremented attempts from independent stale snapshots, and made the fifth failure terminal only on another request. Correct verification saved consumption before linking the Patient, allowing a failed link to leave a consumed challenge. SMTP failures were swallowed, and unconfigured email could simulate delivery outside Development. Angular used a local 60-second countdown and Edit Details could bypass its expectation.

## B. Exact transaction and locking strategy

Both operations begin an explicit PostgreSQL **ReadCommitted** transaction, then execute a parameterized `SELECT "Id" ... FROM identity.users ... FOR UPDATE` for the authenticated user's existing row. A missing anchor is unauthorized. Every authoritative activation decision follows that lock. The lock works even when there are no challenges yet; locking a nonexistent challenge cannot provide that protection.

Order is **User anchor → candidate Patient → challenge history**. Start locks the eligible MRN/DOB Patient. Verify discovers only the latest challenge's Patient identifier under the user anchor, locks that Patient, then loads fresh authoritative challenge history. Identifier discovery is not an OTP decision. Queries explicitly include Patient's PostgreSQL `xmin`; verification tracks the fresh row and retains its original `xmin` for the link update. Previously tracked candidate/challenge snapshots are detached before authoritative reads.

Same-account operations serialize at the user row. Different accounts claiming the same Patient serialize at the Patient row. Existing unique non-null Patient.UserId and xmin protection remain. ReadCommitted is sufficient because these stable row locks protect the activation invariants; Serializable and automatic retries are unnecessary.

Each operation has one SaveChanges and one commit. Failed operations roll back and clear the change tracker. EF concurrency failures and PostgreSQL 23505, 40001, and 40P01 become a safe 409 ProblemDetails response. Other database persistence failures become safe 500 ProblemDetails. Database messages and identifiers are not returned. Transaction disposal releases locks before delivery.

## C. Final challenge lifecycle

An issuance creates one hashed-code challenge valid for ten minutes, with a 60-second server deadline. Issuance invalidates every older challenge for the authenticated account in the same transaction. Verification considers only the latest issuance, including consumed rows, and terminalizes older legacy pending rows without falling back to them. Consumed/expired/exhausted latest challenges cannot reveal an older usable challenge.

The existing IsConsumed flag represents terminal state; no schema/status additions were needed. Successful comparison consumes the challenge. Expired/exhausted verification terminalizes it. Fifth wrong comparison terminalizes it immediately. Replacement OTP generation also excludes hashes of older codes still within their original validity window, preventing accidental numeric reuse from undermining resend ordering.

## D. Attempt-limit semantics

Only a current, eligible, nonterminal challenge undergoes a comparison. Each comparison increments AttemptCount under the shared user lock. Wrong requests observe previously committed increments. Four previous failures leave exactly one remaining comparison. A correct fifth comparison may activate; a wrong fifth comparison consumes the challenge immediately. Later requests perform no further comparisons and the count stays five. Attempts, terminal state, and any successful Patient link persist in the same transaction.

## E. Cooldown/resend semantics

Cooldown reads the latest issuance across this authenticated user's activation flow, regardless of consumption or exhaustion. Because verify accepts only a code, one account has one current flow: changing MRN cannot create a second concurrent flow or bypass cooldown. This is an account issuance throttle, not a new Patient-wide quota.

Two starts at an allowed time create/send one code. A following contender observes the committed deadline and returns the cooldown response. After cooldown, two replacement resends create one replacement, invalidate predecessors, and send one email. During cooldown neither issues. The DTO now returns `resendCooldownUntilUtc`. A cooldown response does not invent a destination for a changed MRN: its message directs the user to the previously requested code. Normal issuance still returns the masked stored destination.

Missing/ineligible MRN/DOB retains the intended generic success-shaped eligibility response, without issuance/email or new existence information. Its response includes a UI deadline; that generic response does not create persistent issuance history. Missing stored email retains the existing support-required contract. Linked accounts are rejected under the lock.

## F. Verify/resend ordering

Both operations use the same user anchor. If verify commits first, resend observes the linked account and issues nothing. If resend commits first, the old challenge is consumed and verification of its old code fails against the replacement. Different operations never compare a code using a pre-lock snapshot. Old pending rows cannot resurface after the replacement becomes terminal.

## G. Correct-code atomicity

Successful comparison modifies consumption and Patient.UserId before the only SaveChanges/commit. Any linking/storage failure rolls back both writes and the attempt increment. Concurrent identical correct codes yield at most one successful activation; the other sees the linked account. Replay is rejected. `/status` uses the persisted Patient.UserId relationship. Identity account state is not the linkage state and is not modified.

## H. Email failure behavior

Issuance commits and disposes its transaction before calling the email sender. The real activation email method now throws an activation-specific delivery exception when delivery is known to fail. The activation service returns HTTP 200 with `success:false`, a safe delivery-failure message, and the committed cooldown deadline. The challenge is retained. A request inside that cooldown cannot trigger another delivery attempt.

Unconfigured delivery simulates email only when IHostEnvironment explicitly identifies Development. Production and Staging report failure and do not log raw activation OTPs. SMTP success means acceptance by the configured sender, not guaranteed inbox delivery. Password-reset behavior was not changed. No outbox or delivery retry was added.

## I. Angular changes

Existing shared pending guards now cover all start/resend/verify interactions and block Edit Details during a pending operation. Start also honors an active cooldown after Edit Details. The countdown derives from the server UTC deadline and current time rather than resetting to a hardcoded local minute; a failed delivery response also sets it. Submit displays the remaining wait. Both operation errors read ProblemDetails `detail`, with existing fallbacks. Timers are cleared on destruction. Verification remains usable during resend cooldown. Routes and visual layout were preserved. No automatic retry was introduced.

## J. Files changed for this batch

| Area | Files |
|---|---|
| Application | `src/Core/HealthERP.Application/Common/Interfaces/IPatientActivationService.cs`; `PatientAccount/ActivationExceptions.cs`; `PatientAccount/Models/StartActivationResponseDto.cs`; `PatientAccount/Commands/StartPatientActivation/StartPatientActivationCommandHandler.cs`; `PatientAccount/Commands/VerifyPatientActivation/VerifyPatientActivationCommandHandler.cs` |
| Domain | `src/Core/HealthERP.Domain/Patients/PatientActivationChallenge.cs` |
| Infrastructure | `src/Infrastructure/HealthERP.Infrastructure/Services/PatientActivationService.cs`; `Services/EmailService.cs`; `DependencyInjection.cs` |
| API | `src/Presentation/HealthERP.Api/EndPoints/PatientAccountEndpoints.cs`; `Middleware/GlobalExceptionHandlingMiddleware.cs` |
| Angular | `health-erp-client/src/app/core/services/patient-account.service.ts`; `features/portal-coming-soon/portal-coming-soon.ts`; `features/portal-coming-soon/portal-coming-soon.html`; `features/portal-coming-soon/activation-integrity.spec.ts` (feature files under `src/app`) |
| Tests/report | `tests/HealthERP.Api.IntegrationTests/PatientActivationTests.cs`; `ActivationEmailTests.cs`; this report |

Shared DI and middleware contain only activation additions from this batch. No persistence configurations, model snapshots, migrations, authorization, JWT/session, Patient Portal, or earlier batch logic were changed for Batch 6. Numerous pre-existing worktree modifications remain from earlier work.

## K. Tests added

**36 backend tests: 33 real PostgreSQL activation cases and 3 email environment cases.** The existing guarded disposable fixture creates a random test database in a separate loopback test cluster; it never falls back to the ERP database, migrates/seeds that database, or sends real email. Captured recipients use `.invalid` addresses.

Concurrency tests use independent DbContexts/connections. A separate transaction locks the user or Patient row, operations are launched, and pg_stat_activity must show every contender waiting on PostgreSQL locks before release. No SaveChanges barrier waits for a request blocked at the user anchor. HTTP tests also use separate authenticated request scopes and the real handlers, activation service, Npgsql, endpoints, and exception middleware. The HTTP test session validator is deliberately stubbed; unchanged functional session tests cover that separate boundary.

Coverage includes normal issuance/hash/recipient; simultaneous starts and replacement resends; concurrent cooldown rejection; consumed and exhausted issuance history; permanent replacement invalidation; wrong-attempt increments; final-attempt contention and immediate fifth failure; correct fifth comparison; duplicate correct code and replay; correct/wrong contention; verify/resend valid orders; forced link rollback; two accounts claiming one Patient; one account attempting two Patients; consumed/expired/legacy superseded rejection; missing email; MRN and DOB mismatch; injected PatientId/UserId/email ignored at HTTP binding; status after linkage; unchanged Identity role/stamps/password/account fields; locks released before delivery; failed delivery retention; authorization; and pending model check.

Actual PostgreSQL triggers in the disposable database force link writes to fail with P0001, 23505, 40001, and 40P01. HTTP tests check safe 500/409 details, no private database text, and rollback of both link and consumption. Production/Staging email tests use unconfigured localhost mode and verify no raw OTP logging; Development simulation is explicitly tested.

**8 Angular tests:** duplicate start guards; resend pending guard; cross-action verification guards; server deadline plus Edit Details; cooldown after failed delivery; start ProblemDetails/no retry; verify ProblemDetails/no retry; timer cleanup.

## L. Backend build and test results

Final commands used an isolated temporary artifacts directory to avoid interfering with the running development API:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch6-20261013" --no-restore
# Set this only to a separately created disposable test cluster, Database=postgres.
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = 'Host=127.0.0.1;Port=<test-port>;Database=postgres;Username=health_erp_tests'
dotnet test HealthERP.slnx --no-build --artifacts-path "$env:TEMP\HealthERP-Batch6-20261013" --logger 'trx;LogFileName=batch6-full.trx' --logger 'console;verbosity=quiet'
```

Build: **passed, 0 warnings, 0 errors**. Complete suite: **259 passed, 0 failed, 0 skipped**.

| Suite | Passed |
|---|---:|
| Domain | 41 |
| Application | 39 |
| API functional | 30 |
| API integration | 149 |

TRX results are in each test project's `TestResults/batch6-full.trx`. The isolated cluster on port 57203 was stopped after verification. Maintenance query confirmed zero remaining generated test databases. Earlier verification runs exposed an overlong test MRN and missing xmin projection/tracking; both were corrected before the final passing suite.

## M. Angular build and test results

From `health-erp-client`:

```powershell
npm.cmd run build
npm.cmd test -- --watch=false
```

Build: **passed**, with the existing SweetAlert2 CommonJS optimization warning. Full suite: **59 passed, 1 failed, 60 total**, across nine files. All eight Batch 6 tests passed. The unrelated pre-existing failure remains `src/app/app.spec.ts:22`, `App > should render title`: the scaffold expects an h1 containing its greeting but the current app renders no such h1. That test and application layout were not changed to hide it.

## N. EF model and migration result

Real PostgreSQL test `ActivationRequiresNoModelMigration` passed `Database.HasPendingModelChanges() == false`. Existing earlier-batch model checks also passed in the full suite. **No migration created; no migration required.** Patient xmin and unique filtered UserId configuration remain intact.

## O. Remaining limitations

There is no outbox: a process crash after commit but before sending can leave an issued code undelivered until cooldown permits resend. Delivery may be accepted by SMTP and still fail later. A slow older email can arrive after a later replacement; its challenge remains invalid. A lost response/commit acknowledgement can make the client uncertain; refresh `/status` before retrying. There are no automatic retries.

UI timing depends on browser clock and is only a UX guard; PostgreSQL and server time enforce issuance. A page reload may initially lack the countdown, but a start request is still throttled and returns the deadline. Latest account history is loaded for lifecycle repair; retention/index optimization and Patient-wide anti-abuse quotas are outside this batch. Existing OTP hash design and generic eligibility contract were retained. Six-digit codes can be reused after older validity windows; no permanent uniqueness policy for numeric codes was introduced. Direct database writes outside this service must obey their own invariants.

The complete Angular suite remains red because of the documented existing scaffold test. No manual browser session or real SMTP inbox delivery was claimed as performed.

## P. Exact manual browser/API verification

Use disposable test patients/accounts and a development capture inbox or explicit Development simulation. Never use real patient email for these checks. Start the normal local app in two PowerShell terminals if needed:

```powershell
Set-Location 'F:\Health ERP'
dotnet run --project src/Presentation/HealthERP.Api --launch-profile http
```

```powershell
Set-Location 'F:\Health ERP\health-erp-client'
npm.cmd start -- --proxy-config proxy.conf.json
```

1. Create an unlinked Patient-role account and an unlinked Patient with known MRN/DOB and a captured stored email. Log in and open `http://localhost:4200/portal-activation`.
2. Submit matching MRN/DOB. Network shows one POST `/api/v1/patient-account/activation/start`, success, masked destination and a UTC cooldown. Confirm one captured email to Patient.Email, not the account email. Click repeatedly and try Resend/Edit Details during the request: no competing request. Edit Details after completion and attempt Start during cooldown: no request. Verify accepts the captured code during cooldown.
3. On another fresh account, issue a code and submit a wrong six-digit code five times. Each response is unsuccessful; the fifth is immediately terminal. Submit the correct original code next: it cannot link. Start inside the original cooldown: no new email. Wait until the server deadline passes, then resend: one replacement code is issued; old code fails, replacement succeeds.
4. For direct API races, run this helper in the logged-in browser console. It reads the existing token locally; do not print/share it. Replace only the test MRN and DOB:

```javascript
const activationMrn = 'YOUR_TEST_MRN';
const activationDob = '1990-01-01';
async function activationCall(path, body) {
  const response = await fetch('/api/v1/patient-account' + path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json',
      Authorization: 'Bearer ' + localStorage.getItem('health_erp_token') },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  return { status: response.status, body: await response.json() };
}
const activationStart = () => activationCall('/activation/start', {
  medicalRecordNumber: activationMrn, dateOfBirth: activationDob
});
const activationVerify = code => activationCall('/activation/verify', { code });
await Promise.all([activationStart(), activationStart()]);
```

5. On a fresh eligible account, the simultaneous starts above must produce one email and one challenge, with the other response observing cooldown. Repeat inside cooldown: no additional email. Wait past the returned deadline, race two starts again: exactly one replacement. Check using a fresh DB connection in your disposable manual test database if desired.
6. Issue a code on another fresh account; run `await Promise.all([activationVerify('999999'), activationVerify('999999'), activationVerify('999999')])`, choosing a value different from the actual captured code. Fresh DB state must show three attempts. Perform one further wrong comparison, then race three wrong requests: count remains five and terminal. No extra comparison is granted.
7. On another fresh account, issue a captured code and run `await Promise.all([activationVerify('CAPTURED_CODE'), activationVerify('CAPTURED_CODE')])`. Exactly one success; `/status` through `await activationCall('/status')` shows linked. Replay fails. Patient.UserId equals the authenticated account. Compare Identity role, security/concurrency stamps, password hash, IsActive and EmailConfirmed before/after: unchanged.
8. On another fresh account, issue a code, wait past cooldown, and run `await Promise.all([activationVerify('OLD_CAPTURED_CODE'), activationStart()])`. Either verify succeeds and resend returns already linked without email, or resend issues a replacement and old verification fails. No mixed success with a superseded old challenge is allowed.
9. With two disposable Patient-role accounts and one Patient, issue each an eligible challenge then submit their correct codes concurrently from separate browser profiles. One account links; the other remains unlinked and cannot overwrite ownership. With one account and two Patients, simultaneous matching starts issue one flow; after its successful verification, another start cannot link the second Patient.
10. Submit mismatched MRN or DOB: generic eligibility response, no email/challenge. A Patient without email returns support-required and creates neither. Add extra `patientId`, `userId`, `email` fields to the start/verify JSON: they do not select a recipient or record. Only claims, server matching and stored Patient.Email are authoritative.
11. For email failure in a disposable environment, use Production/Staging with unconfigured activation SMTP, or a deliberately unreachable test SMTP server. Issue: response reports delivery failure with a future deadline; challenge persists; immediate resend does not deliver again. Do not misconfigure a live environment. Restore test SMTP afterwards. Raw activation OTP is absent from non-Development logs.
12. Link-failure rollback and SQLSTATE mapping are best verified by the supplied disposable PostgreSQL tests rather than installing failure triggers in an operational database. Start an isolated cluster using `tests/HealthERP.Api.IntegrationTests/PHARMACY_TESTS.md`, then run the full commands above or filter `FullyQualifiedName~PatientActivationTests|FullyQualifiedName~ActivationEmailTests`. These verify safe HTTP responses and unchanged challenge/Patient state after forced failure. Stop only the cluster you created. For a UI error check, induce an API error in a test environment: its `detail` is displayed and Network shows no automatic retry.

## Requested TRUE/FALSE answers

| Statement | Answer |
|---|---|
| Concurrent challenge creation is now safe | TRUE |
| Concurrent resend cooldown enforcement is atomic | TRUE |
| Failed OTP attempt counting is atomic | TRUE |
| Fifth failed attempt is immediately terminal | TRUE |
| Older superseded OTPs cannot resurface | TRUE |
| Consuming a challenge cannot bypass issuance cooldown | TRUE |
| Correct OTP consumption and Patient linking are atomic | TRUE |
| Same correct OTP can successfully activate at most once | TRUE |
| Verify-versus-resend is serialized safely | TRUE |
| OTP replay after successful activation is prevented | TRUE |
| Patient.Email remains the authoritative OTP destination | TRUE |
| Browser cannot choose authoritative PatientId/UserId/email | TRUE |
| Identity roles/security state remain unchanged by activation | TRUE |
| A database migration was required | FALSE |
