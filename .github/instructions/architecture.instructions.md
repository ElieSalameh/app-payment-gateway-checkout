---
applyTo: "**/*.cs,**/*.csproj,**/*.slnx"
---
# Decoupled architecture for this assessment

`CLAUDE.md` section 4 is canonical. `.github/project-context/project-scope.md` is the functional source of truth. The required capabilities are processing a payment and retrieving a payment; architecture must support those without adding unrelated production infrastructure.

## Target structure

Four source projects and four test projects. Dependencies point inward.

```text
src/
  PaymentGateway.Api/             # HTTP endpoints, serialization, pipeline, composition root
  PaymentGateway.Application/     # use cases, validation, ports, commands and results
  PaymentGateway.Domain/          # payment state, value objects, business invariants
  PaymentGateway.Infrastructure/  # bank simulator HTTP client, in-memory repository

tests/
  PaymentGateway.Domain.Tests/
  PaymentGateway.Application.Tests/
  PaymentGateway.Infrastructure.Tests/
  PaymentGateway.Api.IntegrationTests/
```

`Domain` is a separate assembly on purpose: it makes "the business rules depend on nothing" a compiler guarantee rather than a folder convention that quietly erodes. This is the layout Microsoft's reference applications and the common Clean Architecture templates use, so a reviewer reads it without needing the decision explained.

The brief's warning about over-engineering targets speculative **features**, not assembly boundaries. Do not add a real database, queue, outbox, webhook system, payment-provider SDK, MediatR or another mediator, a CQRS bus, AutoMapper, event sourcing, or an idempotency protocol. The README explicitly allows a test-double repository.

All four source projects now exist, along with `Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`, and `Api.IntegrationTests`. Keep growing them the same way — add a folder or a project when it has something real to hold, not in advance.

## Dependency direction

- `Api` depends on `Application`. It may reference `Infrastructure` **only** at the composition root, to register implementations.
- `Application` depends on `Domain` only, plus FluentValidation and the `Microsoft.Extensions` abstractions for dependency injection and logging. Never on `Api`, `Infrastructure`, `HttpClient`, or serialization types.
- `Domain` depends on nothing outside the BCL. No ASP.NET Core, no persistence, no serialization, no external services.
- `Infrastructure` depends on `Application` and `Domain` to implement the ports the inner layers define.
- Never reference an outer project from an inner project. No circular project references.
- Keep provider-specific types at the infrastructure boundary and translate them to application or domain types immediately.

## Use cases and ports

- Model the required operations as explicit use cases: `ProcessPayment` and `GetPayment`.
- Define ports in the layer that needs the capability, implement them at the edge. The only two ports are `IAcquiringBankClient` and `IPaymentRepository`. Do not add `IUnitOfWork` or other speculative abstractions, and do not create an interface for every class solely to enable mocking.
- **Validation belongs in `Application`, on the command**, not on the HTTP request model in `Api`. `Rejected` is a business outcome the brief names, so the rule producing it belongs with the use case and must not be bypassable by a second entry point. Use FluentValidation.
- Return explicit result types for expected outcomes: `ProcessPaymentResult` and `GetPaymentResult`, one per use case, each built by a `From(Payment)` factory. The simulator's authorization result maps to `Authorized` or `Declined`; a simulator failure stays a dependency failure and is not a payment outcome. `Rejected` is the exception to the rule — it leaves the handler as a `ValidationException` and becomes the `400` body, for the reasons in `error-handling.instructions.md`.
- Keep orchestration in handlers and business invariants in domain objects. A handler validates, calls its ports, maps, and persists; it holds no rules of its own.
- A missing payment is `PaymentNotFoundException` from `GetPaymentHandler`, not a null return, so the `404` has a cause the gateway names and no caller can skip the check.

## Domain modeling

- Use value objects where validation and value identity are meaningful: `Money`, `Currency`, `PaymentId`, `CardDetails`. Do not wrap a primitive in a type that adds no behavior.
- `CardDetails` holds the **last four digits only**, plus the expiry month and year. The full PAN never enters the domain model or the repository. `CardDetails.FromCardNumber` is the **single** place a card number is reduced to its last four digits — there is no other masking helper anywhere in the codebase.
- Entities maintain their own valid state, enforced by private constructors and static factories. No public setters, and no way to construct an invalid instance.
- A stored `Payment` is exactly `Authorized` or `Declined`. `Rejected` is **not** a domain status: the brief says a rejected request creates no payment, so there is nothing to store and nothing to give a status to. `Rejected` exists only as the gateway's `400` response. Do not add capture, refund, void, or other transitions.
- `Money` is a `long` count of minor units paired with a `Currency`, and must be greater than zero. `Currency` owns the supported set — `GBP`, `USD`, `EUR` — and parsing is case-insensitive but always yields the canonical uppercase code.
- Domain guards throw `ArgumentException` / `ArgumentOutOfRangeException`, because by the time a value reaches the domain it has already passed validation; a violation is a programmer error, not a merchant error. Guard messages state the rule and never echo the rejected value, so a card number cannot leak through an exception.
- Time never comes from inside the domain. A rule that needs the clock takes it as a parameter, as `CardDetails.HasExpired(DateTimeOffset asOf)` does, which keeps the domain deterministic and leaves `TimeProvider` at the application boundary. Its static sibling `CardDetails.IsExpired(expiryMonth, expiryYear, asOf)` lets the validator ask the same question before a `CardDetails` exists, so expiry has exactly one definition: a card expires once the calendar has moved past its expiry month, and a card expiring this month is still valid until the month ends.
- Keep simulator status codes and retry behavior out of the domain; map them to a stable internal model at the edge.
- Do not add domain events for two synchronous use cases.

## Boundaries

- Map HTTP request models to application commands, and application results to HTTP response models. Never return a domain entity from a controller.
- Do not pass `HttpContext`, `ControllerBase`, provider SDK objects, or `IConfiguration` into application or domain code. Bind typed options instead.
- Keep the in-memory repository behind `IPaymentRepository` so it can be replaced without touching a use case. The port is `Add(Payment, CancellationToken)` and `GetById(PaymentId, CancellationToken)` — it takes `PaymentId`, never a raw `Guid`, because the repository boundary is the mix-up `PaymentId` exists to prevent. `Add` throws on an id that is already stored rather than overwriting a recorded charge, and both methods honour their `CancellationToken`.
- Treat the simulator as unreliable: bounded HTTP timeout, `503` mapped to a clear dependency failure, no false authorization or decline. Do not retry the non-idempotent payment request — a retry risks double-charging, and idempotency is not implemented.
- The bank port is `Authorize(AuthorizationRequest, CancellationToken)` returning `AuthorizationResult`, both defined alongside `IAcquiringBankClient` in `Application/Abstractions/`. The result carries a `PaymentStatus`, not a bool, so a call site cannot confuse "not authorized" with "did not complete". `AuthorizationRequest` is one of only two types that hold the full PAN and CVV — the bank DTO is the other — and both redact `ToString()`.
- Every unsuccessful bank response, not only `503`, becomes `AcquiringBankUnavailableException`, as does a `200` whose body cannot be read. The gateway must never infer `Declined` from a transport or protocol failure.

## Feature organization

`Api` is organised by **transport role** — `Controllers/`, `Contracts/Requests/`, `Contracts/Responses/`, `Middleware/`, `Configuration/`. That layer's reasons to change are a route, a status code, or a serialization rule, and those cut across every capability. It is also the shape ASP.NET Core's own templates use, so a reviewer needs no orientation.

`Application`, `Domain`, and `Infrastructure` are organised by **business capability**:

```text
Application/Payments/
  ProcessPayment/
    ProcessPaymentCommand.cs
    ProcessPaymentHandler.cs
    ProcessPaymentValidator.cs
    ProcessPaymentResult.cs
  GetPayment/
    GetPaymentQuery.cs
    GetPaymentHandler.cs
    GetPaymentResult.cs
```

Code that changes together lives together, so group by whatever actually varies: transport concerns in `Api`, use cases inside the layers that hold business rules. Avoid a global `Services`, `Helpers`, or `Utilities` folder that hides ownership.

## Adding a dependency

Before adding a package or a cross-layer reference, state which boundary requires it and why it cannot stay at the edge. Prefer the BCL and what .NET 8 already provides. The deliberate package set is FluentValidation, the `Microsoft.Extensions` dependency injection and logging abstractions, `Microsoft.Extensions.Http.Resilience`, Swashbuckle, and the test stack in `testing.instructions.md`. Anything beyond that needs a justification recorded in `README.md`.
