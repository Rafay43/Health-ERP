# Stabilization Batch 4 — Billing payment integrity and concurrency

Completed 12 October 2026. Scope: recording billing payments only. Backend build passed; all 161 backend tests passed. Angular production build passed; 39 Angular tests passed with one unchanged scaffold failure. Migration required: **NO**. Pending EF model changes: **NO**. No browser/manual testing was performed by Codex.

## 1. Original billing/payment flow

`POST /api/v1/invoices/{id:guid}/payments` requires `CanManageBilling`, binds amount, payment method, optional reference and notes, and sends `RecordPaymentCommand`. The former handler queried the invoice with its payments through `IApplicationDbContext`, called `Invoice.RecordPayment`, and called `SaveChangesAsync` once. There was no explicit transaction around the earlier read, row lock, or concurrency token. No separate billing repository exists; EF is the data access layer.

EF's implicit save transaction already kept that individual request's invoice UPDATE and payment INSERT together. It did not protect the read that decided the balance or the value written by a competing request.

## 2. Confirmed root cause

Two requests could read PaidAmount X, independently compute X+A and X+B, insert both payment rows, then overwrite the invoice with their respective calculated amounts. The last UPDATE won. Payment history could total X+A+B while PaidAmount retained X+A or X+B, yielding the wrong balance and potentially the wrong status. Both could also pass the same stale overpayment check.

This was reproduced against real PostgreSQL using the exact former handler algorithm on a disposable test invoice: both 30 and 40 payment rows committed, their sum was 70, but invoice PaidAmount was only 30 or 40. This reproduction is confined to test code and its disposable database. The production payment path now uses the protected service.

## 3. Invoice model discovered

Invoice has a typed GUID ID, required PatientId, optional AppointmentId, unique InvoiceNumber, UTC issue/due dates, persisted decimal TotalAmount/PaidAmount, persisted InvoiceStatus, line items, payments, and audit fields. BalanceDue is calculated as TotalAmount minus PaidAmount; it is not a separate balance column.

Statuses are Draft=1, Pending=2, PartiallyPaid=3, Paid=4, Cancelled=5. The factory creates Pending invoices with zero paid amount. TotalAmount is the sum of line item totals. `AddLineItem` can change total in the domain, but current uses add items during invoice creation; no existing invoice-edit endpoint changes totals after payments. No new edit behavior was introduced.

## 4. Payment model discovered

Payment has a generated GUID ID, InvoiceId foreign key, UTC date, decimal Amount, PaymentMethod, nullable ReferenceNumber and nullable Notes. Methods are Cash=1, CreditCard=2, Insurance=3, BankTransfer=4. Reference has maximum length 100; notes 500. An invoice has many payments, with cascading deletion through its foreign key.

The billing migration and mappings use PostgreSQL `numeric(18,2)` for invoice TotalAmount/PaidAmount, payment Amount, and line item UnitPrice. No payment reference uniqueness or financial CHECK constraints currently exist. InvoiceNumber is unique; that is unrelated to payment request identity.

## 5. Current partial/overpayment semantics

Partial and additional payments are supported. Overpayment and negative payments are unsupported. No refund, credit, adjustment, payment deletion or reversal workflow was found. Cash and other methods may omit references, and references are not defined as unique transaction IDs. Existing historical data was not inspected or repaired.

Cancelled invoices are now explicitly ineligible for payment; the former method could inadvertently change one back to PartiallyPaid/Paid. Draft has no exposed creation/payment workflow beyond the existing domain terminology; no new accounting lifecycle was invented.

## 6. Financial invariants enforced

A payment must be positive, exactly representable in the existing numeric(18,2) amount column, have a defined payment method, and fit existing text limits. The backend must find an eligible invoice and reject payment exceeding its authoritative remaining balance. It rejects cancelled and fully paid invoices. It never clamps, rounds, substitutes amounts, or returns success for an excess payment.

Starting from a valid invoice/payment history, successful payments maintain PaidAmount equal to the sum of recorded payments, BalanceDue equal to TotalAmount minus PaidAmount, and the corresponding status. Known rejected/rolled-back attempts add no payment and leave the original financial state intact. Transport ambiguity after a successful commit is described in sections 15 and 30.

## 7. Chosen concurrency strategy

An explicit PostgreSQL ReadCommitted transaction begins before reading the invoice. A parameterized `SELECT * FROM billing.invoices WHERE "Id" = @id FOR UPDATE` locks that invoice row. The protected read returns authoritative state. Validation, domain mutation, payment insertion, a single SaveChanges, and commit all occur while the row remains locked.

## 8. Why this strategy fits

Payments contend on one invoice aggregate and must accumulate valid partial payments. A waiter reads the preceding commit's amount under ReadCommitted, so 30 and 40 can both succeed without a serialization abort or automatic retry. This avoids imposing Pharmacy's Serializable behavior on billing. It needs no xmin column, migration, distributed transaction, or separate balance calculation in SQL. EF retains the existing domain mutation and unit of work.

The locked read uses no tracking to avoid an old cached snapshot. Normally the fresh invoice is attached. If the scope already tracks this invoice, it is reloaded under the lock before validation. A dedicated test covers a stale tracked invoice after another context has paid 80.

## 9. Transaction/atomicity design

Payment and invoice changes share ApplicationDbContext and one explicit transaction. The service explicitly tracks the newly returned payment, calls SaveChanges once, then commits. On failure it attempts rollback with CancellationToken.None and clears tracked state, preventing failed mutations from leaking into a later save. Rollback connection failure does not mask the original error.

Tests force Payment INSERT failure, Invoice UPDATE failure, and cancellation after both writes but before commit. Fresh reads confirm the original PaidAmount, balance, status, and history remain unchanged. This is database atomicity, not a promise that a lost HTTP response proves a commit did not happen.

## 10. Lost-update protection

Two valid partial payments wait on the same row and operate on consecutive authoritative balances. Real HTTP race test: initial total 100/paid 0, requests 30 and 40, **two 204 responses**, final paid 70, balance 30, status PartiallyPaid, two payment rows summing to 70.

## 11. Overpayment protection

The second request validates after the first commit. Real HTTP race test: initial paid 80, two requests of 15, **one 204 and one 409**, final paid 95, balance 5, PartiallyPaid, one new payment. History contains the initial 80 plus one 15, summing to 95.

## 12. Fully-paid invoice protection

Positive payment against Paid or zero/negative remaining balance is rejected. Real HTTP race test: initial paid 90, two requests of 10, **one 204 and one 409**, final paid 100, balance 0, Paid, one new payment. A sequential additional payment against a fully paid invoice also returns 409 without adding history.

## 13. Payment/invoice consistency behavior

Both financial mutations commit or neither commits. PostgreSQL tests assert payment count and sum alongside PaidAmount, BalanceDue, and Status after success, rejection, contention, and storage failure. No amount is reconstructed from client state, and no historical balance correction or historical payment reconciliation is attempted.

## 14. Invoice status behavior

Existing terminology is preserved: new unpaid invoices remain Pending; successful positive payments below total produce PartiallyPaid; payment reaching total produces Paid. Cancelled stays Cancelled when rejected. Status is persisted with the monetary update in the same transaction. No new accounting states or frontend status inference were added.

## 15. Duplicate-submission behavior and limitations

The existing submit button already disables while pending; the component now also guards repeated calls to `submitPayment`. Backend balance/row locking prevents a final payment from being applied twice and rejects competing overpayments independently of UI checks.

There is **no reliable existing idempotency key**. Equal optional references may represent legitimate distinct cash/manual payments. A test confirms two 30 payments with the same manual reference can record 60 consistently when the balance allows. No uniqueness constraint or invented duplicate rule was added. A repeated partial-payment request with sufficient remaining balance can therefore be recorded again, including from two tabs.

If the database commits but its acknowledgment/HTTP response is lost, the client may receive an error despite an atomic completed payment. The UI reloads history and does not automatically retry. Staff must reconcile the latest history before deliberately retrying an uncertain payment. A full idempotency design requires separately agreed transaction identity semantics.

## 16. Error/status-code behavior

| Outcome | HTTP | Behavior |
|---|---:|---|
| Recorded payment | 204 | Payment and invoice committed |
| Nonpositive amount, fractional cents, numeric overflow, invalid method or overlong fields | 400 | Explicit validation rejection |
| Missing invoice | 404 | Safe not-found response |
| Excess, already-paid, cancelled invoice | 409 | Billing conflict with refresh guidance |
| PostgreSQL 40001/40P01 or EF concurrency exception | 409 | Safe conflict; transaction rolled back |
| Unexpected database write/storage failure | 500 | Safe message asking to refresh invoice/history |
| No authentication / insufficient role | 401 / 403 | Existing server authorization |

Npgsql can wrap transient PostgreSQL failures in InvalidOperationException around DbUpdateException. The billing service traverses the inner exception chain, avoiding the generic scheduling-error mapping and database-detail leakage. Tests force PostgreSQL 40001 and 40P01 errors to verify this translation. They test taxonomy, not a claim to have created a genuine database deadlock. Existing Pharmacy exception cases are unchanged.

## 17. Angular behavior changes

Only `billing-list.ts` changed in product UI. Repeated pending submissions are guarded. Success retains the existing success message and reloads invoices. Reload also replaces selected detail/payment invoice snapshots, so paid amount, balance, status and payment history reflect returned backend data. Rejections and uncertain failures show the safe error and reload; totals are not optimistically mutated and payment is not automatically retried. A failed reload leaves earlier displayed data rather than fabricating fresh data.

The billing service/request contract, template, routing and layout remain unchanged. No BillingStaff landing-page fix was included. Existing JavaScript number serialization/display remains; no new client monetary arithmetic was introduced. Exact very large decimal input remains a limitation of that existing frontend contract.

## 18. Authorization behavior

Production `CanManageBilling` remains Admin or BillingStaff, on the same endpoint group. Patient and Doctor are rejected by staff payment endpoints; unauthenticated requests receive 401. New real HTTP tests cover all these outcomes and financial nonmutation for denied requests.

The billing test host uses the existing JWT authentication extension with a test session validator and the same role policy. These tests establish endpoint/role enforcement; existing Batch 2 tests separately exercise actual session revocation. Patient Portal billing still obtains the current patient server-side and filters Invoice.PatientId to that patient. Portal code and ownership tests were preserved and passed.

## 19. Files changed in this batch

Paths below identify only Batch 4 work; the workspace already contained prior changes.

| File | Change |
|---|---|
| [IBillingPaymentService.cs](<F:/Health ERP/src/Core/HealthERP.Application/Common/Interfaces/IBillingPaymentService.cs>) | Narrow payment service contract |
| [BillingPaymentConflictException.cs](<F:/Health ERP/src/Core/HealthERP.Application/Billing/BillingPaymentConflictException.cs>) | Controlled billing conflict |
| [BillingPaymentStorageException.cs](<F:/Health ERP/src/Core/HealthERP.Application/Billing/BillingPaymentStorageException.cs>) | Safe storage exception |
| [RecordPaymentCommandHandler.cs](<F:/Health ERP/src/Core/HealthERP.Application/Billing/Commands/RecordPayment/RecordPaymentCommandHandler.cs>) | Delegates protected operation |
| [Invoice.cs](<F:/Health ERP/src/Core/HealthERP.Domain/Billing/Invoice.cs>) | Validates eligibility before mutation |
| [Payment.cs](<F:/Health ERP/src/Core/HealthERP.Domain/Billing/Payment.cs>) | Existing precision and field-limit validation |
| [BillingPaymentService.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/Services/BillingPaymentService.cs>) | Row lock, transaction, rollback, error translation |
| [DependencyInjection.cs](<F:/Health ERP/src/Infrastructure/HealthERP.Infrastructure/DependencyInjection.cs>) | Scoped billing service registration |
| [BillingEndpoints.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/EndPoints/BillingEndpoints.cs>) | Documents 409 payment response |
| [GlobalExceptionHandlingMiddleware.cs](<F:/Health ERP/src/Presentation/HealthERP.Api/Middleware/GlobalExceptionHandlingMiddleware.cs>) | Adds billing-specific safe response cases |
| [billing-list.ts](<F:/Health ERP/health-erp-client/src/app/features/billing/billing-list.ts>) | Busy guard and authoritative refresh |
| [BillingPaymentTests.cs](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/BillingPaymentTests.cs>) | 30 PostgreSQL billing cases |
| [PaymentIntegrityTests.cs](<F:/Health ERP/tests/HealthERP.Domain.UnitTests/Billing/PaymentIntegrityTests.cs>) | 10 domain cases |
| [billing-payment.spec.ts](<F:/Health ERP/health-erp-client/src/app/features/billing/billing-payment.spec.ts>) | Five Angular behavior cases |
| [BILLING_TESTS.md](<F:/Health ERP/tests/HealthERP.Api.IntegrationTests/BILLING_TESTS.md>) | Safe integration-test setup and methodology |
| [This report](<F:/Health ERP/STABILIZATION_BATCH_4_REPORT.md>) | Findings, validation and manual plan |

## 20. Tests added/changed

Added 30 PostgreSQL billing cases: full/partial/additional payment; zero/negative/fractional-cent/overflow rejection; exact one-cent payment; original lost-update reproduction; overpayment/fully-paid/cancelled/missing invoice; all three actual HTTP races; failure of either write; wrapped concurrency error translation; cancellation before commit; stale tracked balance; optional-reference semantics; Admin/BillingStaff/Doctor/Patient/unauthenticated access; pending-model check.

Added 10 domain cases for invalid money, invalid method/text lengths, and cancelled invoice nonmutation. Added five UI cases for no optimistic mutation, duplicate pending clicks, refreshed details/history after success, conflict reload, uncertain failure without retry, and corrected deliberate retry. The first UI case covers both no mutation and duplicate clicks. No existing test was deleted or weakened.

## 21. PostgreSQL concurrency-test methodology

Reused the existing Batch 3 guarded fixture and nonparallel collection without altering them. An explicit maintenance connection is mandatory. This run used a fresh temporary PostgreSQL 17 cluster, loopback port 57283, and its randomly generated disposable test database. It did not use the existing ERP database or service, production migrations or seed data.

Each race sends two authenticated HTTP requests through actual billing endpoints, Mediatr, scoped DbContexts, middleware, and the new production service. An independent connection holds the invoice row until `pg_stat_activity` confirms both requests are waiting on locks, then releases it. Fresh contexts assert persisted outcomes. The former flow would wait at its stale UPDATE instead and fail these financial assertions. No EF InMemory database is used to establish concurrency behavior.

The original-flow reproduction uses a test-only barrier after both independent reads and before their saves. Failure triggers are scoped to generated test invoices and removed in finally blocks. A post-save interceptor tests cancellation before commit. The fixture drops only its own generated database. The temporary cluster is stopped after validation. See BILLING_TESTS.md for reproducible commands.

## 22. Backend build result

**PASS**, zero warnings/errors:

```powershell
dotnet build HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch4-20261012"
```

The initial no-restore attempt correctly failed because this new artifacts directory had no assets yet. Restore in the sandbox was blocked from NuGet; the approved build restored successfully. Following the exception-wrapper correction, the final build with `--no-restore` also passed with zero warnings/errors. Isolated artifacts avoid disturbing DLLs held by the user's running API.

## 23. Backend test result

**PASS: 161 passed, 0 failed, 0 skipped.**

| Project | Passed |
|---|---:|
| Domain unit tests | 40 |
| Application unit tests | 39 |
| API functional tests | 30 |
| API integration tests | 52 |

The first run exposed two billing error-wrapper cases; these were fixed in BillingPaymentService. The final complete suite passed, including existing notification/SignalR security, JWT/session revocation, Patient Portal contracts/ownership, and all 21 Pharmacy PostgreSQL cases.

```powershell
$env:HEALTHERP_PHARMACY_TEST_CONNECTION = 'Host=127.0.0.1;Port=57283;Database=postgres;Username=health_erp_tests'
dotnet test HealthERP.slnx --artifacts-path "$env:TEMP\HealthERP-Batch4-20261012" --no-build --no-restore --logger "trx;LogFilePrefix=Batch4Final"
```

Final TRX files are under the four test projects' TestResults folders, named `Batch4Final_net10.0_20261012*.trx`. Port 57283 belongs to the stopped temporary cluster; recreate a separate cluster before rerunning, using BILLING_TESTS.md.

## 24. Angular build result

**PASS:** `npm.cmd run build` from `F:\Health ERP\health-erp-client`. The existing SweetAlert2 CommonJS optimization warning remains. No build error.

## 25. Angular test result

`npm.cmd test -- --watch=false`: **39 passed, 1 failed, 40 total**, across six files. All five new billing cases and existing notification/auth/pharmacy cases passed. The unchanged unrelated failure is `src/app/app.spec.ts:22`, which expects a scaffold `<h1>` greeting absent from the current application. It was not modified to make the suite green.

## 26. Migration required

**NO.** Existing invoice/payment tables, FK relationship, decimal precision, and indexes support row locking and atomic writes. No mapped property, column, xmin token, unique reference constraint, or CHECK constraint was added. PostgreSQL locking provides correct serialization for this payment workflow without schema changes.

## 27. Pending EF model changes

**NO.** The real PostgreSQL billing model check calls `Database.HasPendingModelChanges()` and asserts false; it passed. The existing Pharmacy model check also passed. No migration or model snapshot was changed in this batch.

## 28. Performance implications

Only payments on the same invoice serialize. Payments to different invoices can proceed independently. The normal operation uses one locked invoice read, one SaveChanges containing both writes, and one commit, without loading the full historical payment collection. A previously tracked invoice requires an additional reload under the lock. Locks are held only for this short database operation; there are no external calls while locked, automatic payment retries, or table-wide locks. Contention may increase latency on one heavily used invoice; errors remain controlled.

## 29. Exact manual verification steps

Restart the development backend with the new code when ready. Use a development test patient and a logged-in Admin or BillingStaff account. BillingStaff's unrelated landing behavior remains; navigate to the existing Billing page directly if necessary. API launch settings use HTTP `http://localhost:5268`; Angular's existing `/api` proxy lets the following browser console helper run from the frontend origin. No token is printed by this helper.

Current routes:

- Create invoice: `POST /api/v1/invoices/`.
- Record payment: `POST /api/v1/invoices/{invoiceId}/payments`.
- Read authoritative financial state/history: `GET /api/v1/invoices/?patientId={patientId}&page=1&pageSize=25`.

Create payload:

```json
{
  "patientId": "REPLACE_WITH_EXISTING_TEST_PATIENT_GUID",
  "appointmentId": null,
  "dueDateUtc": "2026-11-12T00:00:00Z",
  "lineItems": [{ "description": "Batch 4 manual test", "quantity": 1, "unitPrice": 100 }]
}
```

Payment payload (amount varies per test):

```json
{ "amount": 30, "paymentMethod": 1, "referenceNumber": null, "notes": "Batch 4 manual test" }
```

Paste once into the browser console while signed in, then replace the patient GUID:

```javascript
var batch4 = (() => {
  async function api(path, method = 'GET', payload) {
    const token = localStorage.getItem('health_erp_token');
    if (!token) throw new Error('Sign in as Admin or BillingStaff first.');
    const response = await fetch('/api/v1/invoices' + path, {
      method,
      headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
      body: payload === undefined ? undefined : JSON.stringify(payload)
    });
    const text = await response.text();
    return { status: response.status, body: text ? JSON.parse(text) : null };
  }
  async function pay(id, amount) {
    return api('/' + encodeURIComponent(id) + '/payments', 'POST', {
      amount, paymentMethod: 1, referenceNumber: null, notes: 'Batch 4 manual test'
    });
  }
  async function create(patientId, initialPaid = 0) {
    const result = await api('/', 'POST', {
      patientId, appointmentId: null,
      dueDateUtc: new Date(Date.now() + 30 * 86400000).toISOString(),
      lineItems: [{ description: 'Batch 4 manual test', quantity: 1, unitPrice: 100 }]
    });
    if (result.status !== 201) throw new Error(JSON.stringify(result));
    const id = result.body.id;
    if (initialPaid > 0) {
      const seed = await pay(id, initialPaid);
      if (seed.status !== 204) throw new Error(JSON.stringify(seed));
    }
    return id;
  }
  async function snapshot(patientId, id) {
    const result = await api('/?patientId=' + encodeURIComponent(patientId) + '&page=1&pageSize=25');
    if (result.status !== 200) throw new Error(JSON.stringify(result));
    const invoice = result.body.find(i => i.id === id);
    if (!invoice) throw new Error('Invoice is outside this page; query further pages explicitly.');
    return invoice;
  }
  return { create, pay, snapshot };
})();
var batch4PatientId = 'REPLACE_WITH_EXISTING_TEST_PATIENT_GUID';
```

Use a fresh invoice for every test. Each snapshot contains backend totalAmount, paidAmount, balanceDue, status, and payments; inspect all five.

| Test | Initial paid | Requests | Expected HTTP | Final paid / balance / status | New / total history rows |
|---|---:|---|---|---|---|
| 1 Partial | 0 | 30 | 204 | 30 / 70 / PartiallyPaid | 1 / 1 |
| 2 Overpayment | 80 | 21 | 409 | 80 / 20 / PartiallyPaid | 0 / 1 |
| 3 Fully paid | 100 | 1 | 409 | 100 / 0 / Paid | 0 / 1 |
| 4 Concurrent valid | 0 | 30 and 40 | 204 + 204 | 70 / 30 / PartiallyPaid | 2 / 2 |
| 5 Concurrent competing | 80 | 15 and 15 | 204 + 409 in either order | 95 / 5 / PartiallyPaid | 1 / 2 |
| 6 Concurrent final | 90 | 10 and 10 | 204 + 409 in either order | 100 / 0 / Paid | 1 / 2 |

```javascript
// TEST 1
var batch4Id1 = await batch4.create(batch4PatientId);
await batch4.pay(batch4Id1, 30);
await batch4.snapshot(batch4PatientId, batch4Id1);

// TEST 2
var batch4Id2 = await batch4.create(batch4PatientId, 80);
await batch4.pay(batch4Id2, 21);
await batch4.snapshot(batch4PatientId, batch4Id2);

// TEST 3
var batch4Id3 = await batch4.create(batch4PatientId, 100);
await batch4.pay(batch4Id3, 1);
await batch4.snapshot(batch4PatientId, batch4Id3);

// TEST 4
var batch4Id4 = await batch4.create(batch4PatientId);
await Promise.all([batch4.pay(batch4Id4, 30), batch4.pay(batch4Id4, 40)]);
await batch4.snapshot(batch4PatientId, batch4Id4);

// TEST 5
var batch4Id5 = await batch4.create(batch4PatientId, 80);
await Promise.all([batch4.pay(batch4Id5, 15), batch4.pay(batch4Id5, 15)]);
await batch4.snapshot(batch4PatientId, batch4Id5);

// TEST 6
var batch4Id6 = await batch4.create(batch4PatientId, 90);
await Promise.all([batch4.pay(batch4Id6, 10), batch4.pay(batch4Id6, 10)]);
await batch4.snapshot(batch4PatientId, batch4Id6);
```

Run statements individually to see each returned status and snapshot. Promise.all starts both fetches together; automated PostgreSQL tests provide deterministic contention proof. After success, reload Billing and inspect the detail history, paid amount, balance and status. For UI conflict refresh, open the payment modal on a stale invoice, pay from another tab, then submit an amount exceeding the new balance: expect a safe conflict and refreshed balance without a success message. Double-click while pending should send one request from that component. Invalid zero/negative/fractional-cent requests through the API should return 400 and preserve history. A Patient token must get 403 on the staff payment route; verify portal billing still shows only its owner's invoices.

## 30. Limitations intentionally left for later

No full duplicate-request/idempotency subsystem or reference uniqueness rule was invented. An uncertain commit acknowledgment requires reconciliation before retry. Existing historical billing corruption, direct SQL writes, and arbitrary new writers bypassing this service are not repaired/protected by financial CHECK constraints; any future invoice edit/payment writer must participate in the concurrency design. No production data audit was performed. Existing invoice-creation line-item precision behavior, large-number frontend representation, billing pagination, refund/credit design and insurance placeholders remain outside this narrow payment operation.

No bed/admission concurrency, Pharmacy logic, DOB, patient pagination, dashboard metrics, activation concurrency, notification/SignalR, JWT/session logic, unrelated permissions, BillingStaff landing UX, reporting period selector, billing redesign, technical-debt cleanup, or Phase 7 work was done. The existing global diff has unrelated whitespace findings; those were left intact. Work stops at Batch 4.

## Explicit TRUE/FALSE confirmations

Financial TRUE statements apply to the current protected payment workflow starting from a valid invoice/history. They do not assert repair of historical corruption or constraints on manual SQL. The two absolute “failed payment” statements are FALSE if “failed” includes a lost response after the database committed; their normal rejection/rollback form is TRUE and tested.

| Statement | TRUE/FALSE | Evidence / qualification |
|---|---|---|
| Successful payment and invoice update are atomic | TRUE | One transaction, one SaveChanges, commit |
| Failed payment leaves invoice unchanged | FALSE as an unconditional transport claim | TRUE for validation/business rejection and confirmed rollback; a lost commit response can accompany a completed atomic payment |
| Failed payment creates no Payment row | FALSE as an unconditional transport claim | TRUE for rejected/rolled-back attempts; same unknown-commit qualification |
| Concurrent valid payments cannot lose an update | TRUE | 30 + 40 HTTP race persists 70 and two matching rows |
| Concurrent competing payments cannot overpay an invoice | TRUE | 80 + 15/15 race persists 95 with one new row |
| Fully paid invoice rejects additional payment | TRUE | Sequential and final-payment race tests |
| PaidAmount cannot exceed TotalAmount under current business rules | TRUE | Fresh locked-balance validation; no overpayment semantics |
| Invoice status remains consistent with financial state | TRUE | Status and amounts saved atomically and asserted in fresh reads |
| Backend is the financial source of truth | TRUE | Request contains payment input only; authoritative locked database read |
| CanManageBilling was not weakened | TRUE | Production policy remains Admin/BillingStaff |
| Patient cannot use staff payment-management endpoint | TRUE | Actual HTTP 403 and unchanged financial state |
| Batch 1 notification security remains intact | TRUE | Existing suites preserved and passed; logic unchanged |
| Batch 2 JWT/session security remains intact | TRUE | Existing suites preserved and passed; logic unchanged |
| Batch 3 Pharmacy integrity remains intact | TRUE | All 21 existing PostgreSQL Pharmacy cases passed; logic unchanged |
| Patient Portal billing read ownership remains intact | TRUE | Portal handler unchanged; ownership/contracts passed |
| Bed/admission concurrency was NOT addressed | TRUE | Outside this batch |
| BillingStaff /forbidden landing UX was NOT addressed | TRUE | Routing/login unchanged |
| No Phase 7 work was started | TRUE | Billing payment scope only |
| Unrelated audit findings were not fixed | TRUE | Prior changes preserved; no unrelated implementation edits |
