# Stabilization Batch 2: Account status and JWT session invalidation

Implemented 2026-10-11. Existing workspace work and Batch 1 security controls were preserved. No ERP database initialization, migrations, seed or browser/manual testing was performed. The running API was left running; restart it before verifying this batch.

1. **Original login flow.** `/api/v1/auth/login` dispatched `LoginCommand` to `IdentityService.LoginAsync`, looked up the email with `UserManager`, checked its password, loaded roles and issued a JWT. No current account-status checks preceded issuance. Invalid credentials raised `InvalidOperationException`, which middleware classified as a scheduling conflict (409).

2. **Confirmed account-status bugs.** Correct passwords could authenticate users with `ApplicationUser.IsActive == false` or an effective Identity lockout. Admin's account list derived its displayed status from lockout alone and ignored `IsActive`. Password verification did not count/reset failed attempts using Identity lockout APIs.

3. **Original JWT validation.** Bearer validation checked signature/key, issuer, audience and lifetime. Batch 1 also handled browser query-token transport only on hub routes. Tokens contained identity/profile/role claims and `jti`, without a verifiable account session version.

4. **Confirmed revocation problem.** Password reset changed Identity's SecurityStamp, but bearer validation never checked it. Admin lock/unlock and role replacement did not rotate the stamp. A copied JWT could keep working until expiry, including with removed role claims.

5. **Final status semantics.** `IsActive` is an explicit account enable/disable gate. `LockoutEnabled` means the account is eligible for Identity lockout; it alone does not prohibit login. Effective lockout is determined by `UserManager.IsLockedOutAsync`, including `LockoutEnd`. `AccessFailedCount` uses Identity's existing default threshold: five failed attempts, then a five-minute automatic lockout. Correct eligible login resets the failed count. Admin Lock Account now sets `IsActive=false`, enables lockout and sets the existing long lockout end; Unlock/reactivate sets `IsActive=true`, clears the end and resets failed attempts. Admin DTOs report usable status as active AND not locked. No new account-state field or policy was introduced.

6. **Revocation mechanism.** Existing Identity SecurityStamp is the persisted session version. Newly issued JWTs contain a SHA-256 fingerprint of that stamp. The server compares that fingerprint with current Identity state on subsequent bearer authentication. This avoids placing the raw stamp in a token and requires no session table, refresh-token system or expiration-only workaround.

7. **Claims.** Added dedicated `session_version`, a 64-character hex digest calculated exclusively from the server's SecurityStamp. Existing subject, profile, `jti`, role claims and expiry remain unchanged. Fingerprint comparison decodes the digest and uses `CryptographicOperations.FixedTimeEquals`. Missing, malformed or stale fingerprints fail closed. Tokens issued before this batch lack the claim and will return 401 after deployment; every existing session must sign in again once.

8. **JWT validation changes.** After normal signature/issuer/audience/lifetime validation, `AccountJwtBearerEvents` invokes the centralized scoped `IJwtSessionValidator`. It checks the subject GUID, loads the current user once, rejects deleted/inactive/locked users and checks the fingerprint. Failure becomes bearer authentication failure and HTTP 401, not an application 500. Valid authentication without a required role still returns 403. Batch 1's hub-token reader is reused unchanged. Generic invalid login now returns 401 ProblemDetails with `detail: "Invalid credentials."`; unknown emails, incorrect passwords, inactive accounts and locked accounts expose the same message and no JWT. Validation errors for malformed login input remain 400. No constant-time email lookup/dummy password-hash redesign was added.

9. **Stamp update events.** Successful password reset uses Identity's own stamp rotation. Every authorized admin lock/disable or unlock/reactivate rotates the stamp. Every admin role replacement rotates it before mutation and again in `finally`, invalidating tokens issued while replacement was in progress as well as prior tokens. Identity automatic lockout also rotates it when failed attempts reach the threshold, so expiration of that lockout cannot restore the old JWT. Identity operation results are checked; failed security writes are not reported as successful. Registration reads Identity's server-generated stamp when issuing its first JWT. No stamp changes occur for an invalid reset, ordinary failed login below the threshold, account listing or normal API requests.

10. **Admin lock/unlock.** The existing authorized status endpoint and UI remain. Lock/disable rejects both the user's next protected request with an old JWT and fresh login. Unlock/reactivate allows a valid new login, but another stamp rotation ensures the original JWT never becomes usable again. Non-admin callers still receive 403. Authorized repeated status operations also revoke prior sessions.

11. **IsActive.** `false` independently blocks fresh login and bearer authentication even when Identity lockout is not effective. Existing admin status operations now maintain it consistently. Reactivation through that workflow rotates the stamp and requires a fresh token. A direct out-of-band database edit that changes account status back to active must also rotate SecurityStamp; it is not an alternative supported reactivation workflow.

12. **Password reset.** The existing forgot/reset endpoints, generic forgot response, email transport and Identity reset-token provider remain. The reset token is validated before any successful password/stamp update. A valid reset invalidates old JWTs, the old password fails, and a new-password login issues a valid fresh JWT. Invalid resets leave existing sessions intact. The previous redundant explicit stamp update after `ResetPasswordAsync` was removed: real Identity integration tests confirm that the successful reset already rotates it and prevents reset-token replay.

13. **Password change.** No authenticated Change Password feature exists in the current source. None was added. Any future such feature must use Identity's supported password-change API and preserve this session-version contract.

14. **Role add.** The current admin operation replaces the user's complete role set with one selected role; that product behavior remains. Assignment invalidates current sessions as well as removal. A fresh login is required before newly granted privileges appear in a token.

15. **Role remove.** Removing an old privileged role through replacement invalidates old JWTs with 401. A fresh token reflects only current roles; authenticated attempts at a policy no longer granted return 403. No policy definitions or RBAC rules changed.

16. **SignalR.** Both Batch 1 hubs use the same strengthened bearer validation. Valid JWT negotiation/WebSocket handshake works; revoked-token negotiation and new WebSocket connections fail. Browser query-token transport and user targeting are preserved. An already-established WebSocket is not continuously reauthenticated or immediately aborted by an account mutation. It retains Batch 1's `CloseOnAuthenticationExpiration` setting and closes at token expiry or normal disconnect; any new connection/reconnect is revalidated. Immediate server-driven socket termination would require additional connection/revocation infrastructure and was intentionally not introduced. This boundary also means an established connection may continue receiving its previously authorized events until it closes; this batch does not claim immediate live-socket revocation.

17. **Angular 401/logout.** The existing interceptor now logs out only when a rejected request used the still-current token. Parallel/late 401 responses cannot repeatedly navigate or terminate a newer session; anonymous login failures remain on the login page. Logout clears token, user and derived role state and is idempotent. Existing Batch 1 signal-driven connection teardown/private-state clearing remains. Notification connection failure/reconnect exhaustion now refreshes through Angular's protected HTTP pipeline so a revoked session's 401 also triggers that same logout path; SignalR's independent client cannot create an endless authenticated-looking retry session. 403 does not log out a valid user. Guards and the login UI were inspected and retain their existing architecture; the login UI already shows a generic invalid-credentials message.

18. **Files changed in this batch.** Twenty implementation/test/configuration files, plus this report. Prior modifications in shared files were preserved.

| File | Purpose |
| --- | --- |
| [JwtSessionVersion.cs](<F:/Health ERP/src/Core/HealthERP.Application/Common/Security/JwtSessionVersion.cs>) | Dedicated fingerprint claim and safe comparison |
| [IJwtSessionValidator.cs](<F:/Health ERP/src/Core/HealthERP.Application/Common/Interfaces/IJwtSessionValidator.cs>) | Central validation boundary |
| [IJwtTokenGenerator.cs](<F:/Health ERP/src/Core/HealthERP.Application/Common/Interfaces/IJwtTokenGenerator.cs>) | Require server stamp at issuance |
| [LoginCommandHandler.cs](<F:/Health ERP/src/Core/HealthERP.Application/Authentication/Commands/Login/LoginCommandHandler.cs>) | Generic authentication failure |
| [JwtSessionValidator.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/Services/JwtSessionValidator.cs>) | Current Identity status/stamp validation |
| [JwtTokenGenerator.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/Services/JwtTokenGenerator.cs>) | Include session fingerprint |
| [IdentityService.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/Services/IdentityService.cs>) | Login gates, lockout APIs, mutation revocation, consistent admin status |
| [DependencyInjection.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/DependencyInjection.cs>) | Register validator |
| [AccountJwtAuthentication.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Authentication/AccountJwtAuthentication.cs>) | Shared production/test bearer setup and events |
| [Program.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Program.cs>) | Use strengthened bearer setup |
| [GlobalExceptionHandlingMiddleware.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Middleware/GlobalExceptionHandlingMiddleware.cs>) | Map authentication failure to generic 401 without noisy exception logging |
| [AuthEndpoints.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/EndPoints/AuthEndpoints.cs>) | Document login 401 |
| [auth.service.ts](<F:/Health ERP/health-erp-client/src/app/core/services/auth.service.ts>) | Idempotent logout |
| [auth.interceptor.ts](<F:/Health ERP/health-erp-client/src/app/core/interceptors/auth.interceptor.ts>) | Current-session-only 401 handling |
| [notification.service.ts](<F:/Health ERP/health-erp-client/src/app/core/services/notification.service.ts>) | Reconnect failure checks through existing HTTP/logout pipeline |
| [AccountSessionSecurityTests.cs](<F:/Health ERP/tests/HealthERP.Api.FunctionalTests/AccountSessionSecurityTests.cs>) | Real Identity/JWT/HTTP/WebSocket regressions |
| [NotificationSecurityTests.cs](<F:/Health ERP/tests/HealthERP.Api.FunctionalTests/NotificationSecurityTests.cs>) | Preserve Batch 1 tests under new token/validation contract |
| [HealthERP.Api.FunctionalTests.csproj](<F:/Health ERP/tests/HealthERP.Api.FunctionalTests/HealthERP.Api.FunctionalTests.csproj>) | Isolated EF Identity test-store dependency |
| [auth.interceptor.spec.ts](<F:/Health ERP/health-erp-client/src/app/core/interceptors/auth.interceptor.spec.ts>) | Six session/logout regressions |
| [notification.service.spec.ts](<F:/Health ERP/health-erp-client/src/app/core/services/notification.service.spec.ts>) | Preserve/revalidate Batch 1 retry/logout test |

19. **Tests.** Nineteen new functional cases use actual Identity managers, EF stores, hashing, reset-token provider, authentication/admin endpoints, production bearer setup and real local HTTP/WebSocket transport. Only persistence/email are isolated: an EF in-memory Identity database and captured reset email, without the ERP database/startup. Cases cover active login, wrong/unknown/inactive/locked generic failure, lock/unlock old-token rejection, independent inactive flag, valid/invalid/replayed reset, old/new passwords, role removal/addition, non-admin denial, failed-attempt lockout and expiry, failed-count reset, deleted users, missing/malformed/stale session claims and both hubs rejecting revocation. Existing Batch 1 cross-user tests remain, including two connections for one owner and private delivery isolation. Six Angular tests exercise the real AuthService/interceptor/guard for 401 state clearing, concurrent failures, stale responses, anonymous login, 403 and idempotent logout; all ten Batch 1 lifecycle tests remain.

20. **Backend build.** The default `dotnet build HealthERP.slnx` encountered locked DLLs held by the already-running API. That process was not stopped. Equivalent solution validation used `dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch2-20261011"`, passing with zero warnings and zero errors. No repository output-path configuration was changed.

21. **Backend tests.** `dotnet test HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch2-20261011"` passed all 100 tests: Domain 30, Application 39, API Functional 30, API Integration 1. The final compiled output was also tested with `--no-build` in that same folder. These totals include the existing API placeholders; nineteen new tests are executable Identity/session security tests. Batch 1 notification/SignalR tests pass.

22. **Angular build/tests.** `npm.cmd run build` passes; the existing SweetAlert2 CommonJS warning remains. The full `npm.cmd test -- --watch=false` suite has 29 passing tests and one unchanged pre-existing scaffold failure at `src/app/app.spec.ts:22` (expects an `h1` the application does not render). All six new auth tests, ten notification/lifecycle tests and twelve Patient Portal contract tests pass. The focused auth/notification run passes all 16 tests. The unrelated scaffold was not changed.

23. **Migration.** No migration required or created. Existing Identity SecurityStamp, IsActive and lockout fields are sufficient. No new persisted version column, backfill, database seed or schema update was run.

24. **Performance.** A structurally valid signed JWT with a session claim causes one current-user lookup for each bearer authentication event. Lockout and stamp checks use that loaded user; there is no duplicate user fetch within the event, session table, distributed cache or background polling. Hub negotiation and transport requests authenticate independently as required by their HTTP lifecycle. This intentionally prioritizes immediate rejection on subsequent protected requests over stateless JWT validation. A database outage cannot grant access by skipping the check. Already-authenticated requests are not retroactively cancelled if an account changes mid-request.

25. **Manual verification plan — not performed.** Restart the API with the changed code and restart/reload Angular first. Tokens issued by the prior build are intentionally rejected; sign in afresh as an authorized Admin and a separate active Doctor user A in separate browser profiles. Keep the Admin profile signed in throughout. Use the existing Admin Accounts UI for status/role changes and the existing Forgot/Reset Password UI for reset.

    In A's Developer Tools console, retain an old token only in memory and define a status-only probe. Do not print tokens, stamp values, reset tokens or headers:

    ```javascript
    var batch2OldJwt = localStorage.getItem('health_erp_token');
    var batch2Probe = (jwt, path = '/api/v1/notifications/') =>
      fetch(path, { headers: { Authorization: 'Bearer ' + jwt } }).then(r => r.status);
    void 0;
    ```

    Confirm `await batch2Probe(batch2OldJwt)` returns **200** before each relevant mutation. The console variable survives ordinary Angular navigation, but a full browser reload loses it; retain it within the same page session when checking old-token non-resurrection.

    **Test 1 — locked fresh login:** Admin → Accounts → locate A → Lock Account. Sign A out and attempt login with A's correct password. Expect **401**, the same generic credential message used for a wrong password, and no token issued/stored. Admin's account listing shows A unavailable/locked.

    **Test 2 — revoke an existing session:** Unlock A, sign A in and refresh `batch2OldJwt` with A's newly issued token. Verify the probe returns 200. Admin locks A while A stays signed in. `await batch2Probe(batch2OldJwt)` must now return **401**. Trigger a normal Angular protected load (for example, navigate to Patients or let notification initialization/recovery load). Angular clears credentials, user/roles and notification state, stops its connections, and routes to login once. The copied token remains rejected regardless of localStorage clearing.

    **Test 3 — unlock does not resurrect:** Admin unlocks A. Probe the unchanged old token from Test 2: still **401**. Sign A in with correct credentials; probe `localStorage.getItem('health_erp_token')`: **200**. A's new hub connection works, while negotiation with the old token returns 401.

    **Test 4 — password-reset revocation:** With A active and logged in, store its current JWT again in `batch2OldJwt`. Complete the existing Forgot Password and Reset Password flow using the delivered valid link and a new password. After success, probe `batch2OldJwt`: **401**. A login with the previous password returns the generic **401**. An invalid reset attempt before a successful reset must return **400** and must not revoke an otherwise valid session.

    **Test 5 — new password/new token:** Log A in with the new password. Probe the new stored JWT: **200**. The old retained JWT still returns **401**. A valid new notification hub negotiation/connection works. For status-only negotiation testing with a retained token:

    ```javascript
    await fetch('/hubs/notifications/negotiate?negotiateVersion=1', {
      method: 'POST', headers: { Authorization: 'Bearer ' + batch2OldJwt }
    }).then(r => r.status);
    ```

    Expect 401 for a revoked old token and 200 with a valid fresh token. Do not treat an already-open socket as evidence that a revoked token can establish a new connection; the established-socket boundary is described in item 16.

    **Test 6 — removed privileges:** A starts as Doctor. Store its current JWT and verify `await batch2Probe(batch2OldJwt, '/api/v1/patients/')` returns **200**. Admin changes A's role to BillingStaff with the current single-role replacement UI. That old JWT must now return **401**, including against Patients and notification endpoints. Log A in again: its new roles show BillingStaff; notifications remain **200**, while `/api/v1/patients/` returns **403** because the new valid session lacks CanViewPatients. If Admin assigns Doctor again, the BillingStaff token is also revoked; a fresh login is required to use the new privilege. Recheck Batch 1's two-user private-delivery test with fresh tokens: another user's notification remains absent and cannot be marked read (404).

26. **Intentional limits and scope.** No immediate account-change push terminates already-open sockets; no request already authenticated before a change is cancelled. No distributed caching, refresh tokens, new password-change UI, cross-tab broadcast session coordinator or out-of-band database trigger was introduced. Account status/role updates must use the existing authorized service workflow; direct database/security writes must rotate SecurityStamp themselves. Validation against PostgreSQL and browser/manual verification remain for the user; automated tests isolate Identity persistence. The active development API must be restarted to load this implementation. Patient Portal ownership, Batch 1 private targeting/ownership, authorization policies and unrelated audit findings were not changed. No Phase 7 work started.

Explicit confirmations: inactive and locked users cannot obtain a new JWT; lock/disable invalidates an existing JWT on subsequent authentication; unlock does not resurrect it; successful reset invalidates prior JWTs; role removal cannot preserve old JWT privileges; revoked JWTs return 401 from protected APIs and cannot establish new protected hub connections. Batch 1 notification ownership/private delivery remains intact. Patient Portal ownership architecture is unchanged. No Phase 7 or unrelated stabilization fixes were undertaken. Work stops at Stabilization Batch 2.
