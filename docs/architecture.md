# Architecture

Health ERP is a layered modular monolith: an Angular client, one ASP.NET Core Minimal API, and PostgreSQL persistence shared by clinical and administrative modules.

## Request flow and boundaries

Angular feature components call typed data-access services. API endpoint classes apply authentication/policies, translate HTTP contracts, and dispatch MediatR requests. Application handlers and FluentValidation enforce use-case rules. Domain entities contain state transitions and typed identifiers. Infrastructure implements persistence, Identity, email, and transactional services.

Domain references SharedKernel rather than Application/Infrastructure/API. The application exposes IApplicationDbContext DbSets; handlers use EF Core directly, without a repository abstraction. The Contracts project is a scaffold, not an established independent public contract layer.

## Source map

| Location | Responsibility |
| --- | --- |
| src/Core/HealthERP.Domain | Entities, typed identifiers, domain rules/events |
| src/Core/HealthERP.Application | Feature commands/queries, validation, DTOs and interfaces |
| src/Infrastructure/HealthERP.Infrastructure | EF configurations/migrations, Identity and workflow services |
| src/Presentation/HealthERP.Api | Minimal API endpoints, authentication, policies, middleware and SignalR hubs |
| src/SharedKernel/HealthERP.SharedKernel | Shared entity, audit and event primitives |
| health-erp-client/src/app/core | Authentication, guards, interceptors and shared services |
| health-erp-client/src/app/features | Feature screens, models and data access |
| tests | Domain/application, functional and real PostgreSQL integration suites |

## Identity and authorization

Identity accounts and hospital Staff records are separate. Admin explicitly links them; matching names or emails do not establish identity. Doctor Workspace resolves an active Doctor-kind Staff record from the authenticated account and checks qualifying appointments before patient snapshots or note writes. Clinical authorship is persisted by staff ID.

Patient Portal resolves the linked patient server-side from the signed-in account. JWT authentication validates token settings and the live account/session version. Security-stamp changes invalidate existing sessions.

Shared patient/staff workflows retain broader named role policies. Doctor Workspace scoping must not be presented as universal patient-level authorization across the application. The [README policy table](../README.md#roles-and-policies) records current role mappings.

## Persistence and competing writes

EF Core configurations and migrations map Identity and clinical entities to PostgreSQL. Audit interception records actor/timestamps, and soft-delete filtering applies where implemented. Patient xmin mapping and unique account-link constraints support concurrency control.

Dispensing and inpatient transitions use Serializable transactions. Payments lock the invoice and re-evaluate its balance. Activation coordinates account/patient/challenge locks and consumes the code atomically. Relevant serialization/deadlock errors become conflict responses. These are workflow-specific protections: booking overlap checks are not a database exclusion constraint, and note writes have no idempotency key.

## Client behavior

Standalone Angular components use signals, reactive forms and lazy routes. Route guards guide navigation; backend checks remain authoritative. SignalR services follow authentication lifecycle and reject stale account responses. Doctor Workspace also guards against stale schedule/snapshot requests.

## Further reading

- [Features and local setup](../README.md)
- [Testing and transaction scenarios](testing.md)
- [Security and operational limitations](security.md)
- [UI conventions](design.md)
- [Third-party licensing](licensing.md)
