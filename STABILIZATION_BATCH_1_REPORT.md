# Stabilization Batch 1: Notification security and SignalR privacy

Completed 2026-10-11. Changes are limited to notification ownership, private delivery, related hub authentication and Angular connection lifecycle. Existing workspace changes were preserved.

1. **Original architecture.** Notification commands and appointment, staff-registration and emergency-triage handlers created notification rows. EF persisted `TargetUserId` (nullable GUID), `TargetRole`, content and a row-level read flag. The notifications endpoint queried through MediatR. Only the generic POST published `ReceiveNotification`, using SignalR. The ERP shell initialized the Angular notification service, which loaded the list and displayed received events. Workflow-created rows did not previously publish private notification events. A separate appointment hub sent a change-only event, also used by the background no-show worker.

2. **Confirmed security problems.** The current source allowed unscoped retrieval, cross-user read-one and read-all mutations, arbitrary recipient input on the generic POST, unauthenticated hub access and private broadcasts to all connections. Angular supplied no hub bearer token and retained the notification connection/state through logout. Workflow notifications were created without a user owner. These problems were confirmed by reading current source before changes.

3. **Retrieval root cause.** The endpoint constructed the query without `UserId`; the handler added ownership filtering only when that optional argument was present. Even its conditional filter included `TargetRole == "All"`, permitting cross-user visibility. Browser-supplied query ownership has been removed from the application contract.

4. **Delivery root cause.** The generic POST explicitly invoked `hubContext.Clients.All.SendAsync("ReceiveNotification", dto)`. Hub mappings required no authorization, and JWT configuration did not handle the query-token transport used by browser WebSocket/SSE connections.

5. **Files changed in this batch.** The following 25 implementation/test/configuration files changed, plus this report. Some already contained unrelated user changes; those were preserved.

| Area | File | Change |
| --- | --- | --- |
| Application | [IPrivateNotificationPublisher.cs](<F:/Health ERP/src/Core/HealthERP.Application/Common/Interfaces/IPrivateNotificationPublisher.cs>) | Private publication boundary |
| Application | [NotificationOwner.cs](<F:/Health ERP/src/Core/HealthERP.Application/Notifications/NotificationOwner.cs>) | Required current-user GUID resolution |
| Application | [NotificationDtos.cs](<F:/Health ERP/src/Core/HealthERP.Application/Notifications/Models/NotificationDtos.cs>) | Safe shared projection |
| Application | [GetUserNotificationsQuery.cs](<F:/Health ERP/src/Core/HealthERP.Application/Notifications/Queries/GetUserNotificationsQuery.cs>) | Mandatory owner filter |
| Application | [MarkNotificationAsReadCommand.cs](<F:/Health ERP/src/Core/HealthERP.Application/Notifications/Commands/MarkNotificationAsReadCommand.cs>) | Read-one and read-all ownership |
| Application | [SendNotificationCommand.cs](<F:/Health ERP/src/Core/HealthERP.Application/Notifications/Commands/SendNotificationCommand.cs>) | Self-owned creation and publication |
| Workflow | [BookAppointmentCommandHandler.cs](<F:/Health ERP/src/Core/HealthERP.Application/Appointments/Commands/BookAppointment/BookAppointmentCommandHandler.cs>) | Actor recipient, safe summary, publication |
| Workflow | [CreateStaffCommandHandler.cs](<F:/Health ERP/src/Core/HealthERP.Application/StaffMembers/Commands/CreateStaff/CreateStaffCommandHandler.cs>) | Actor recipient and publication |
| Workflow | [CreateTriageRecordCommand.cs](<F:/Health ERP/src/Core/HealthERP.Application/Triage/Commands/CreateTriageRecordCommand.cs>) | Actor recipient, safe summary, publication |
| API | [Program.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Program.cs>) | JWT events, security/publisher DI, protected mappings |
| API | [NotificationEndpoints.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/EndPoints/NotificationEndpoints.cs>) | Owner policy, safe 404, remove broadcast |
| API | [NotificationSecurity.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Hubs/NotificationSecurity.cs>) | Policy, GUID user provider, JWT transport, hub mappings |
| API | [AppointmentHub.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Hubs/AppointmentHub.cs>) | Server-controlled authorized viewer group |
| API | [NotificationPublisher.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Services/NotificationPublisher.cs>) | Persisted owner targeting |
| API | [AppointmentNotificationService.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Services/AppointmentNotificationService.cs>) | Authorized viewer-group change event |
| Angular | [notification.model.ts](<F:/Health ERP/health-erp-client/src/app/core/models/notification.model.ts>) | Remove recipient/role fields |
| Angular | [notification.service.ts](<F:/Health ERP/health-erp-client/src/app/core/services/notification.service.ts>) | JWT, authenticated lifecycle, reconnect recovery, stale-session guards |
| Angular | [appointment-signalr.service.ts](<F:/Health ERP/health-erp-client/src/app/core/services/appointment-signalr.service.ts>) | JWT and account/logout teardown |
| Angular | [shell.ts](<F:/Health ERP/health-erp-client/src/app/layout/shell/shell.ts>) | Notification teardown when shell exits |
| Angular | [proxy.conf.json](<F:/Health ERP/health-erp-client/proxy.conf.json>) | Hub HTTP/WebSocket forwarding |
| Tests | [NotificationOwnershipTests.cs](<F:/Health ERP/tests/HealthERP.Application.UnitTests/Notifications/NotificationOwnershipTests.cs>) | Ownership, safe projection and create/save ordering |
| Tests | [WorkflowNotificationTests.cs](<F:/Health ERP/tests/HealthERP.Application.UnitTests/Notifications/WorkflowNotificationTests.cs>) | Three workflow producer regressions |
| Tests | [NotificationSecurityTests.cs](<F:/Health ERP/tests/HealthERP.Api.FunctionalTests/NotificationSecurityTests.cs>) | Real HTTP/JWT/WebSocket security tests |
| Tests | [HealthERP.Api.FunctionalTests.csproj](<F:/Health ERP/tests/HealthERP.Api.FunctionalTests/HealthERP.Api.FunctionalTests.csproj>) | Existing-version mock persistence dependencies |
| Tests | [notification.service.spec.ts](<F:/Health ERP/health-erp-client/src/app/core/services/notification.service.spec.ts>) | Ten Angular notification/hub lifecycle tests |

6. **Query ownership fix.** Every query resolves the current Identity GUID through `ICurrentUserService`, then applies `TargetUserId == currentUserId` before unread filtering, sorting and the existing 50-row limit. No role-based bypass remains. Missing, malformed or empty IDs fail closed. The API's owner policy rejects authenticated identities without a nonempty GUID. Extra browser `userId`/`role` parameters cannot override ownership.

7. **Read ownership fix.** Read-one selects by both notification ID and current user ID before mutation. Missing and foreign records both return HTTP 404 with the same empty response; neither is modified. Read-all selects only unread rows belonging to the current user. Successful owner operations retain HTTP 200 and `{ "success": true }`.

8. **SignalR authentication.** Both hubs now require authorization using ASP.NET Core's existing JWT authentication. `SignalRJwtBearerEvents` accepts `access_token` only under the two hub paths, allowing browser WebSocket/SSE transport authentication. Ordinary API routes still require the Authorization header. The notification hub requires the authenticated owner policy; the appointment hub requires existing `CanViewPatients`. Hub connections close when authentication expires. Unauthenticated negotiation returns 401, and unauthenticated WebSocket upgrades are rejected. This was tested against real local HTTP/WebSocket connections.

9. **SignalR user identification.** Identity uses GUID user IDs; the existing token generator emits that ID as JWT `sub`, and bearer inbound mapping resolves it to `ClaimTypes.NameIdentifier`, which `CurrentUserService` reads. A centralized `NotificationUserIdProvider` uses that same claim and normalizes it to canonical GUID text. This matches publisher targeting even if claim text uses uppercase. The real WebSocket test uses the actual token generator with uppercase GUID subjects.

10. **Replacement for `Clients.All`.** `NotificationPublisher` sends `ReceiveNotification` through `Clients.User(persistedOwnerGuid)` and refuses ownerless publication. All authenticated connections for that user receive the event naturally through SignalR. The separate payload-free `AppointmentUpdated` invalidation now uses a server-controlled group whose members passed `CanViewPatients`; patients cannot join. It does not carry notification content or record IDs. No `Clients.All` usage remains in backend source. No custom connection dictionary was introduced.

11. **Angular changes.** The ERP notification service opts in through the staff shell, connects only with an authenticated non-patient session, supplies the current JWT via `accessTokenFactory`, and refreshes after connection/reconnection. Automatic reconnection remains enabled; initial failures and exhausted reconnects retry after five seconds while the session remains active. Logout, account changes and shell destruction stop connections and clear private state. Late responses/events from an old session cannot restore its data. Events are deduplicated, and read state changes only after server success. The appointment client also supplies JWT authentication and stops on logout/account changes. The development proxy now forwards `/hubs` including WebSocket upgrades. No frontend user-ID filtering is used as authorization.

12. **Creation authorization review.** The generic POST retains existing `CanEditPatients` protection and now always derives its recipient from the authenticated actor. `TargetUserId` and `TargetRole` were removed from its command and unused Angular request interface; extra legacy JSON properties cannot redirect delivery. Patients cannot create these staff notifications (403). Appointment booking, staff registration and triage creation assign their notification to the authenticated staff actor and publish only after persistence. These workflows contain no trusted Identity recipient association for a different user, so no recipient is inferred from a patient/staff record or browser input. This deliberately replaces former global notifications with actor-owned notifications. Existing policies protecting the business workflows remain unchanged. The no-show worker creates no private notification row and still emits only the authorized appointment refresh signal. No other notification producers were found in the current source.

13. **Payload privacy review.** Public DTOs omit recipient user ID and target role; the notification's own ID remains because the UI needs it to mark the record read. No patient ID, creator identity or provider notes are projected. Newly generated appointment/triage messages omit patient names, clinical reason/complaint and vital signs; their authorized destination pages retain the clinical details. Staff-registration content remains confined to its creating authorized actor. Legacy owned messages are not rewritten. No JWT/password/content logging was added; the existing ASP.NET logging threshold suppresses informational request logging, and client hub URL/error logging was removed. Test-host request logging is disabled so query tokens do not appear in test output.

14. **Patient-role impact.** The Patient Portal still has no notification UI or connection initialization; no feature was added. If a patient directly calls notification list/read endpoints, the same ownership rules apply. The patient-role HTTP test returns only that patient's seeded owned row, and staff notification creation/appointment-hub access returns 403. Patient Portal linkage, queries and ownership architecture were not changed.

15. **Tests added.** Twelve application test cases cover both users' retrieval, legacy ownerless/All rows, owner and cross-user read, owner-only read-all, malformed/missing identities, creation/save/publication order, DTO privacy and all three workflow producers. Ten API cases cover protected routes, indistinguishable 404 responses, actual JWT transport/path restrictions, patient boundaries and real WebSocket delivery. The delivery test connects two A sockets and one B socket, proves both A connections receive A's event, and uses each user's subsequent own event as an ordering barrier to prove the other user's event was never queued. Ten Angular tests cover token supply, authenticated initialization, patient exclusion, event updates/deduplication, reconnect refresh/recovery, read success/404 handling, logout, account changes and stale responses.

16. **Backend build.** `dotnet build HealthERP.slnx` passed with zero warnings and zero errors.

17. **Backend tests.** `dotnet test HealthERP.slnx` passed: 81 total, zero failures (Domain 30, Application 39, API Functional 11, API Integration 1). This includes the existing API placeholder tests; the newly added functional tests exercise real isolated HTTP and WebSocket boundaries. Persistence in those tests is mocked; the live ERP/PostgreSQL database was not used.

18. **Angular build/tests.** `npm.cmd run build` passed. The existing SweetAlert2 CommonJS optimization warning remains. `npm.cmd test -- --watch=false` returned 23 passing tests and one pre-existing failure: `src/app/app.spec.ts:22`, the generated title test expects an `h1` that the current app does not render. All ten new notification/lifecycle tests and all twelve existing Patient Portal contract tests pass. The unrelated scaffold test was not changed.

19. **Migration.** None required or created. The existing nullable GUID `TargetUserId` column and index are sufficient. Legacy rows without a concrete owner remain stored but are hidden from retrieval and cannot be marked read or published. No owner was guessed or backfilled. No database migration, seed or development API startup was run during this work.

20. **Exact two-user manual verification plan.** This browser/manual test has not been performed. Run it after restarting the API and Angular development server so the new JWT/hub/proxy configuration is loaded.

    1. Sign in as distinct staff users A and B with `CanEditPatients` permission, using separate browser profiles (for example, normal and incognito). Open a second tab for A. Keep the ERP shell loaded in all three tabs. In Developer Tools, confirm authenticated notification-hub connections exist. Do not copy tokens into logs or share them.
    2. In A's browser console, create a harmless actor-owned notification with the following request. Record only its returned notification ID, referred to below as `A_ID`.

       ```javascript
       const response = await fetch('/api/v1/notifications/', {
         method: 'POST',
         headers: {
           'Content-Type': 'application/json',
           Authorization: 'Bearer ' + localStorage.getItem('health_erp_token')
         },
         body: JSON.stringify({
           title: 'Batch 1 ownership test A',
           message: 'Harmless test for user A only',
           type: 5, severity: 0
         })
       });
       const createdNotification = await response.json();
       console.log(response.status, createdNotification.id);
       ```

    3. Expect **201**. Both A tabs receive `ReceiveNotification` and show the notification/unread badge without refreshing. A's authenticated `GET /api/v1/notifications/` returns it. B's list does not include it; inspect B's WebSocket messages to confirm no A event arrives. B may receive the separate authorized appointment-change signal only when an appointment changes, never A's private notification payload.
    4. In B's profile, replace `A_ID` below with A's notification ID. Expect **404**, identical to a random nonexistent GUID. Reload A's list and confirm A's record is still unread.

       ```javascript
       await fetch('/api/v1/notifications/A_ID/read', {
         method: 'POST',
         headers: { Authorization: 'Bearer ' + localStorage.getItem('health_erp_token') }
       }).then(r => r.status);
       ```

    5. From B, POST `/api/v1/notifications/read-all` with B's Authorization header. Expect **200** and `{ "success": true }`; A's record remains unread. Appending `?userId=<A's Identity ID>&role=All` to B's list request must still return only B's rows.
    6. In A's profile, mark the record read through the drawer or send the same read request with A's token. Expect **200**, `{ "success": true }`, A's read flag/timestamp updated and its unread count reduced. A's `GET /api/v1/notifications/?unreadOnly=true` excludes that row.
    7. Repeat creation as B with a distinct title. Expect only B's connections/list to receive it. As A, try reading B's ID and expect **404**. To test recipient tampering, add `targetUserId` containing B's Identity ID and `targetRole: 'All'` to A's creation body: it must still persist and deliver only to A.
    8. In a signed-out profile, call `GET /api/v1/notifications/`, either read operation, and POST `/hubs/notifications/negotiate?negotiateVersion=1` without a bearer token. Expect **401**. An unauthenticated WebSocket upgrade must be rejected. A signed-in patient attempting generic notification creation or appointment-hub negotiation gets **403**.
    9. Briefly take one A tab offline, create another A notification from the other tab, then restore connectivity. The reconnected tab refreshes and shows the missed owned notification. Sign out of A: the hub stops and cached notification state clears. Signing in as B in that same profile must never show A's old data. Patient Portal navigation must not initialize a new notification feature.

Explicit scope/security confirmations: no private notification uses `Clients.All`; retrieval is server-side user scoped; both read operations are ownership protected; unauthenticated SignalR connections are rejected; Patient Portal ownership architecture is unchanged; no Phase 7 work started. Disabled-account login, password-reset JWT invalidation, unrelated authorization mismatches, dashboard values, pagination, DOB persistence, unrelated concurrency and technical debt were not fixed in this batch. Work stops at Stabilization Batch 1.
