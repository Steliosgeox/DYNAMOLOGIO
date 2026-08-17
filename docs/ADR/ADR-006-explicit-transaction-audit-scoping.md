# ADR-006: Explicit Transaction and Audit Scoping

## Status
Accepted (V7.2)

## Context
In earlier versions, the `IAuditService` was injected directly into domain services (`PersonnelService`, `AbsenceService`, `DutyService`) and wrote directly to the database via `IUnitOfWork`. This violated the single responsibility principle and made it difficult to ensure that domain transactions and audit events were atomic. Furthermore, domain objects (like `Personnel` or `StatusEvent`) had implicit side-effects, such as setting their own `CreatedAt` and `ModifiedAt` properties to `DateTime.Now`, which hindered testability and time-travel logic. The unit of work exposed `BeginTransaction`, `Commit`, and `Rollback` directly to the domain services, leaking persistence concerns.

## Decision
We decided to:
1. Introduce an `ITransactionRunner` abstraction to encapsulate the transactional boundary. Domain services execute their mutating logic inside a lambda passed to `RunInTransaction`.
2. Extract audit logging into an event-driven model using `IAuditEventPublisher` and `IAuditEventSink`. The domain services publish audit events, which are handled by sinks (like `LiteDbAuditSink`) rather than writing directly to the database themselves.
3. Remove implicit date assignments (`DateTime.Now`) from domain entities (`EntityBase`). Instead, inject an `IClock` into domain services, which explicitly populate `CreatedAt` and `ModifiedAt`.
4. Inject an `ICurrentActor` to explicitly supply the `Username` to domain entities and audit events, rather than relying on `Environment.UserName` directly in the entities or services.
5. Move all domain validation logic into the domain services (e.g., `ValidatePersonnel`, `ValidateAbsence`, `ValidateDuty`) and call these authoritative validations inside the domain transaction.

## Consequences
- **Positive:** Full decoupling of time, actor, auditing, and transactional state from the core domain. Tests can now inject a `TestClock`, `TestCurrentActor`, and a `FailingAuditPublisher` to reliably verify rollback and time-related logic without touching real persistence.
- **Positive:** Guaranteed atomicity between domain mutation and audit trail creation.
- **Negative:** Increased constructor injection complexity in the composition root (`App.xaml.cs`).
