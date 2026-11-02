# Stabilization Batch 3: Pharmacy dispensing and inventory integrity

Completed 2026-10-12. Current source was re-inspected before changes. Existing Batch 1/2 work and unrelated workspace edits were preserved. No browser/manual verification was performed. The ERP database and existing PostgreSQL service were not used for tests or modified.

1. **Original dispense flow.** The authorized endpoint sent `DispensePrescriptionCommand`. Its handler loaded the prescription, patient and linked drug through the shared EF context, called `prescription.Dispense()` immediately, then deducted stock only if the drug existed and stock was sufficient. It saved and returned success even if deduction was skipped.

2. **Confirmed root cause.** Insufficient or missing inventory bypassed deduction rather than rejecting the operation. Prescription mutation preceded inventory validation. Nonpositive quantities silently became one. No pharmacy concurrency tokens, locking or serializable transaction protected the two reads/writes; concurrent requests could use the same original stock or pending prescription state.

3. **Model discovered.** Each prescription represents one medication, optionally linked to one inventory `Drug` by nullable GUID-based `DrugId`. It stores medication/dosage/frequency text, duration, refills, instructions, Pending/Dispensed/Cancelled status and dispensing timestamp. There is no item collection, persisted dispense quantity, partial fulfillment model, dispensing staff field or refill fulfillment operation. Inventory has integer `StockQuantity`, name, generic name, category, expiry and unit price. Names are indexed, not a safe unique inventory identifier. The foreign key can become null on drug deletion. The existing DTO/API shape remains.

4. **Stock consumption semantics.** The existing dispensing request explicitly supplies integer `QuantityToDeduct`; omission defaults to one. Angular's current dispense button sends the existing one-unit default. That behavior is preserved. Positive API quantities such as three or four mean those inventory units, not a quantity inferred from dosage/frequency/duration text. Invalid zero/negative quantities now return 400 instead of silently becoming one. Free-text/unlinked prescriptions remain creatable under existing business behavior but cannot be inventory-dispensed until linked by a supported workflow; no association by medication name or new medical semantics were invented.

5. **Invariants.** A dispense requires a pending prescription, a concrete linked inventory row, a positive quantity and sufficient stock. All checks precede entity mutation. Successful mutation deducts exactly the requested units and transitions the prescription once. Stock cannot go negative through this operation. Expected insufficient-stock, unlinked, terminal-state and concurrent conflicts leave both persisted rows unchanged. Database write failures before commit roll back both rows. Repeated dispensing cannot decrement again. No partial dispensing or stock clamping/fabrication was added.

6. **Concurrency strategy.** The new scoped infrastructure `PharmacyDispensingService` starts an explicit PostgreSQL **Serializable** transaction before reading the prescription and drug. PostgreSQL detects competing read/write transactions and rejects the losing transaction with a serialization failure. Deadlock and EF concurrency failures are also treated as controlled conflicts. The MediatR handler delegates through a narrow application interface; the production context and existing domain methods remain.

7. **Fit with architecture.** Both relevant rows are already in one Npgsql-backed `ApplicationDbContext`. Serializable isolation protects the original read-dependent transition across both rows using existing columns. It needs no new version column, xmin mapping, model change, distributed transaction or custom lock dictionary. Patient xmin was inspected but not extended to pharmacy because that would change the EF model and require migration consideration. Each API request uses its own scoped context and transaction. Conflicts are returned for refresh/retry instead of automatically replaying a clinical action.

8. **Atomicity.** Transaction begins before dependent reads. After validation, `Drug.DeductStock` and `Prescription.Dispense` mutate the two tracked rows; one `SaveChangesAsync` participates in the transaction. Commit occurs only after that save succeeds. Failure attempts rollback with a non-cancelled token and clears EF tracking so failed mutations cannot be persisted by a later save. The original failure is preserved if a broken connection also prevents rollback. No intermediate save or inventory-only commit is introduced. A forced PostgreSQL prescription-write failure verifies rollback and unchanged inventory/prescription from a fresh context.

9. **Same-prescription protection.** Sequential repeat and cancelled/terminal records are rejected before stock mutation. Concurrent pending reads run under serializable isolation; only one transaction can commit that prescription transition. The loser rolls back its attempted stock write and returns 409. The real concurrent HTTP test starts two independently scoped requests after both have read their original values and verifies a single decrement and a single final Dispensed state.

10. **Oversale protection.** Two prescriptions sharing five units and requesting four each cannot both commit. The database-backed test verifies one 200, one 409, stock one, one Dispensed record and one untouched Pending record with no dispensing timestamp. Even sufficient-stock overlapping transactions can receive a serialization conflict; refreshing/retrying is deliberate and safe.

11. **Insufficient stock.** Missing/unlinked inventory, stock below the requested amount and zero stock fail before mutation with a meaningful safe conflict message. No SaveChanges success is returned, no prescription is marked dispensed and inventory remains unchanged. Existing domain negative-stock checks remain. No inventory value or fallback is fabricated.

12. **Errors and HTTP status.** Successful dispense: 200 with existing PrescriptionDto. Invalid/nonpositive quantity: 400. Missing prescription: 404 with a generic message. Insufficient inventory, unavailable inventory link, already dispensed/cancelled and serialization/deadlock conflicts: 409 with safe ProblemDetails. Unexpected dispensing storage failures: 500 with a refresh-before-retry message, without SQL details, stack traces or concurrency identifiers in the response. Unauthenticated access remains 401; unauthorized roles remain 403. Only pharmacy-specific error cases were added to shared middleware; existing Batch 2 authentication behavior is preserved. Expected pharmacy conflicts do not generate noisy exception logs.

13. **Angular.** The current disabled button behavior was retained, with a direct-action guard against duplicate calls while submitting or against non-pending rows. The UI still updates from a successful server response and reloads backend inventory/prescriptions. Failures clear busy state, show server ProblemDetails/fallback failure text, and never optimistically mark Dispensed or adjust quantities. 409 refreshes the existing data flow; retry is available afterward. 401 remains delegated to Batch 2's interceptor. No UI redesign, quantity selector or mock inventory was added. Existing create-drug stock form defaults are user-input defaults, not displayed inventory fallbacks, and were not changed.

14. **Authorization.** Existing `CanDispenseMedication` remains Admin/Pharmacist and is still required by the dispense endpoint. PostgreSQL-backed HTTP tests verify Admin and Pharmacist succeed, Doctor and Patient receive 403 without changes, and unauthenticated callers receive 401. Policy definitions were not edited. Existing unrelated prescription-list/view policy mismatches were intentionally not changed; use an authorized Admin profile for the full UI verification plan.

15. **Files changed.** Thirteen implementation/test/documentation files plus this report. Shared files retain prior edits.

| File | Change |
| --- | --- |
| [IPharmacyDispensingService.cs](<F:/Health ERP/src/Core/HealthERP.Application/Common/Interfaces/IPharmacyDispensingService.cs>) | Narrow dispensing boundary |
| [PharmacyDispenseConflictException.cs](<F:/Health ERP/src/Core/HealthERP.Application/Pharmacy/PharmacyDispenseConflictException.cs>) | Controlled stock/state/race error |
| [PharmacyDispenseStorageException.cs](<F:/Health ERP/src/Core/HealthERP.Application/Pharmacy/PharmacyDispenseStorageException.cs>) | Safe unexpected storage error |
| [DispensePrescriptionCommandHandler.cs](<F:/Health ERP/src/Core/HealthERP.Application/Pharmacy/Commands/DispensePrescription/DispensePrescriptionCommandHandler.cs>) | Delegate atomic database operation |
| [PharmacyDispensingService.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/Services/PharmacyDispensingService.cs>) | Serializable transaction, checks, mutation, rollback |
| [DependencyInjection.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/DependencyInjection.cs>) | Scoped service registration |
| [PharmacyEndpoints.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/EndPoints/PharmacyEndpoints.cs>) | Document 409 response |
| [GlobalExceptionHandlingMiddleware.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Middleware/GlobalExceptionHandlingMiddleware.cs>) | Pharmacy-specific safe 409/500 responses |
| [pharmacy-list.ts](<F:/Health ERP/health-erp-client/src/app/features/pharmacy/pharmacy-list.ts>) | Duplicate-action guard and meaningful failures |
| [pharmacy-dispensing.spec.ts](<F:/Health ERP/health-erp-client/src/app/features/pharmacy/pharmacy-dispensing.spec.ts>) | Five UI behavior tests |
| [HealthERP.Api.IntegrationTests.csproj](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/HealthERP.Api.IntegrationTests.csproj>) | Existing-version Moq dependency for isolated auth boundary |
| [PharmacyDispensingTests.cs](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/PharmacyDispensingTests.cs>) | Real PostgreSQL fixture and 21 cases |
| [PHARMACY_TESTS.md](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/PHARMACY_TESTS.md>) | Safe isolated database setup/reproduction |

16. **Tests added.** Twenty-one PostgreSQL integration cases cover stock 10→7, exact stock 3→0, insufficient/zero stock unchanged, invalid quantities, unlinked inventory, cancellation, sequential repeat, two concurrent same-prescription requests, two concurrent shared-inventory requests, forced database failure/rollback, safe 500, role/auth boundaries, API 400/409/404 and pending-model inspection. Five Angular cases cover no optimistic transition, server-driven inventory refresh, meaningful conflict messages, duplicate-action prevention, retry and preserved 401 handling. No existing test was deleted or weakened.

17. **Concurrency methodology and isolation.** A separate PostgreSQL 17 cluster was initialized in a newly generated temporary directory and bound to loopback on a free port. It did not use the existing PostgreSQL service or HealthErpDb. The fixture requires an explicit `HEALTHERP_PHARMACY_TEST_CONNECTION`, rejects non-loopback/non-maintenance connections, creates an empty `health_erp_pharmacy_test_<GUID>` database and drops only its generated database. It uses the actual EF model with EnsureCreated in that disposable database, not production migrations or seed. The two race tests use real HTTP requests and independent contexts; a test-only SaveChanges barrier ensures both original reads occur before either writes. They assert actual PostgreSQL persisted state after responses. No EF InMemory or mocked concurrency is used. A trigger confined to the disposable database injects a write failure for atomicity verification. The temporary cluster is stopped after validation. Missing test configuration fails explicitly rather than silently skipping database proof.

18. **Backend build.** Solution build passes with zero warnings and zero errors. Isolated SDK outputs were used to preserve the running development API:

    ```powershell
    dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch3-20261012"
    ```

19. **Backend tests.** With the isolated test-cluster connection configured, solution tests pass: **121**, zero failures/skips (Domain 30, Application 39, API Functional 30, API Integration 22 including the existing placeholder). Batch 1 notification/private SignalR tests, Batch 2 Identity/JWT revocation tests and Patient Portal ownership/security tests all pass. Test command:

    ```powershell
    dotnet test HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch3-20261012"
    ```

20. **Angular build.** `npm.cmd run build` passes. The existing SweetAlert2 CommonJS optimization warning remains.

21. **Angular tests.** All five new pharmacy tests pass. Full `npm.cmd test -- --watch=false`: **34 passed, one unchanged pre-existing scaffold failure** at `src/app/app.spec.ts:22`, which expects an `h1` the application no longer renders. Existing notification lifecycle, auth/logout and Patient Portal contract tests pass. The unrelated scaffold test was not altered.

22. **Migration required: NO.** Serializable transactions and existing integer stock/status/FK columns provide the required dispensing protection. No pharmacy entity mappings, schema columns or migration files changed. No nonnegative database CHECK currently exists; adding one would be optional defense in depth, not required for these database-serialized application operations, and no migration was created for it. Direct SQL/imports outside this workflow still require their own integrity controls.

23. **Pending EF model changes: NO.** The integration test calls the production context's `Database.HasPendingModelChanges()` and passes against the current migration snapshot. The temporary test database's trigger is test-only and is not an EF model or production schema change.

24. **Performance.** One short serializable transaction spans the existing prescription/patient/drug load, one save and one commit. PostgreSQL may block/abort overlapping writes, including conservative conflicts while enough total stock exists. The loser refreshes and retries explicitly; no automated dispensing replay, distributed lock, cache, background polling or retry infrastructure was added. Transaction overhead is limited to this flow.

25. **Exact manual verification — not performed.** Restart/rebuild the running API so it loads Batch 3, reload Angular, and use an authorized Admin with development/test patient/drug records. Select a real inventory Drug when creating each prescription; free-text medication names alone are intentionally not dispensable. Check inventory and prescription state from backend reloads, not local guesses.

    The GUI button currently consumes **one unit**. For the exact API quantities below, use the existing endpoint from Developer Tools without printing JWTs:

    ```javascript
    var batch3Dispense = async (id, quantity) => {
      const response = await fetch('/api/v1/pharmacy/prescriptions/' + id + '/dispense', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: 'Bearer ' + localStorage.getItem('health_erp_token')
        },
        body: JSON.stringify({ quantityToDeduct: quantity })
      });
      const body = await response.json();
      return { httpStatus: response.status, message: body.detail,
        prescriptionStatus: typeof body.status === 'string' ? body.status : undefined };
    };
    void 0;
    ```

    Get prescription IDs from the existing prescriptions API response in Network tools. Replace the placeholder IDs below with those exact GUIDs. These direct fetches do not update Angular signals; refresh the Pharmacy view afterward.

    **Test 1 — sufficient/exact stock:** Set a test drug's stock to 10 through Adjust Stock and create a linked Pending prescription. Run `await batch3Dispense('RX_ID', 3)`. Expect 200/Dispensed, stock **7**, and a dispensing timestamp. For a separate linked prescription with stock **3**, request **3** and expect stock **0**. For GUI-only verification, stock 10 plus one button click yields **9**, not seven.

    **Test 2 — insufficient stock:** Set stock **2**, create another linked Pending prescription and request **3**. Expect **409** with an insufficient-inventory message. Reload: stock remains **2**, prescription remains **Pending**, timestamp remains empty. With stock zero, the GUI's one-unit attempt also returns 409 and shows the error without success. Zero/negative API quantities return 400 and do not consume the previous one-unit fallback.

    **Test 3 — double dispense:** Using Test 1's successfully dispensed ID, call its endpoint again. Expect **409**. Reload and confirm stock is still **7**, not four; the original dispensing timestamp/state remains. In two browser tabs with the same initially Pending prescription, near-simultaneous attempts must similarly yield only one success/decrement. The automated barrier test is the primary race proof.

    **Test 4 — concurrent oversale:** Set one test drug to **5**, create two distinct Pending prescriptions referencing that same DrugId, and do not adjust stock during the test. Run:

    ```javascript
    await Promise.all([
      batch3Dispense('RX_A_ID', 4),
      batch3Dispense('RX_B_ID', 4)
    ]);
    ```

    Expect one **200** and one **409**. Backend reload must show stock **1**, exactly one Dispensed prescription and the other Pending without a dispensing timestamp. A later attempt on that Pending prescription for four units fails 409 without further changes. Manual request timing does not prove all interleavings; use the documented PostgreSQL integration tests for deterministic race reproduction.

26. **Intentional limitations and scope.** No multi-item/partial/refill dispensing, persisted dispense-unit history, new inventory linkage editor, automatic conflict retries or medical dosage-to-stock mapping was introduced. Free-text/unlinked prescriptions need a supported inventory association before dispensing; no matching by name is attempted. The existing absolute stock-adjustment workflow and unrelated authorization mismatches were not redesigned. Direct database/import operations are not protected by a newly added CHECK constraint. If the connection is lost while acknowledging commit, the client may not know whether the complete transaction committed; refresh before retrying rather than assuming failure means no commit. The database still commits both updates together or neither, and a committed prescription prevents a second decrement. Controlled business/concurrency failures and known failed writes are proven unchanged by rollback tests. Browser verification remains for the user. Billing, bed/admission, activation, DOB/pagination/dashboard and unrelated technical debt were not addressed.

Explicit confirmations: insufficient stock cannot produce a successful dispense; controlled failed dispensing leaves prescription and inventory unchanged; concurrent dispensing cannot oversell inventory; the same prescription cannot decrement twice; successful dispensing updates both rows atomically. Backend remains final authority. CanDispenseMedication was not weakened and Patient cannot dispense. Batch 1 notification security, Batch 2 JWT/session security and Patient Portal ownership architecture remain intact. Billing concurrency and bed/admission concurrency were not addressed. No Phase 7 work or unrelated audit fixes started. Work stops at Stabilization Batch 3.
