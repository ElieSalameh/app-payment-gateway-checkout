---
applyTo: "**/*.cs,**/*.csproj"
---
# Decoupled architecture for this assessment

Use `.github/project-context/project-scope.md` as the functional source of truth. The required capabilities are processing a payment and retrieving a payment; architecture must support those capabilities without adding unrelated production infrastructure.

## Preferred boundaries

Use a pragmatic layered or vertical-slice architecture. For this assessment, the business rules must not depend on ASP.NET Core, HTTP clients, SDKs, or the bank simulator. A single API project is acceptable initially; split projects only when the boundary improves compilation safety or testability.

A solution can evolve toward this structure:

```text
src/
  PaymentGateway.Api/             # HTTP endpoints, serialization, pipeline, composition root
  PaymentGateway.Application/     # process/retrieve use cases, validation, ports, DTOs
  PaymentGateway.Domain/          # payment state, value objects, and business invariants
  PaymentGateway.Infrastructure/  # bank HTTP client and in-memory repository

tests/
  PaymentGateway.Domain.Tests/
  PaymentGateway.Application.Tests/
  PaymentGateway.Api.IntegrationTests/
```

Do not add a real database, queue, outbox, webhook system, or payment-provider SDK for this assessment. The README explicitly allows a test-double repository. Create a separate project when it enforces a useful dependency boundary—not merely to create more folders.

## Dependency direction

- `Api` may depend on `Application`, and the composition root may register `Infrastructure`.
- `Application` may depend on `Domain`, but not on `Api`, `Infrastructure`, the bank HTTP client, or serialization types.
- `Domain` should have no dependency on ASP.NET Core, persistence, serialization, or external services.
- `Infrastructure` may depend on `Application` and `Domain` to implement ports defined by the inner layers.
- Dependencies point inward. Do not reference an outer project from an inner project.
- Keep provider-specific types at the infrastructure boundary. Translate them to application or domain types immediately.

## Use cases and ports

- Model the required operations as explicit use cases: `ProcessPayment` and `GetPayment` (or equivalent names matching the public API).
- Define interfaces (ports) in the inner layer where the capability is needed, then implement them at the edge. The useful ports here are a bank client and a payment repository; do not add `IUnitOfWork` or other speculative abstractions.
- Keep interfaces narrow and outcome-oriented. Do not create an interface for every class solely to enable mocking.
- Return explicit result types for expected outcomes: invalid input is `Rejected`, the simulator's authorization result maps to `Authorized` or `Declined`, and a simulator failure remains a dependency failure.
- Keep orchestration in application services or handlers; keep business invariants in domain objects.

## Domain modeling

- Use value objects for concepts with meaningful validation and identity by value, such as `Money`, `Currency`, and `PaymentId`. Do not introduce a value object solely to wrap a primitive without behavior.
- Keep entities responsible for maintaining valid state. Do not expose mutable collections or public setters for state that has invariants.
- Represent the assessment's payment outcome explicitly: `Authorized`, `Declined`, or `Rejected`. Do not add capture, refund, or other state transitions that are outside the requirements.
- Keep provider status codes and provider-specific retry behavior out of the domain model; map them to a stable internal model.
- Do not add domain events for the two synchronous use cases unless a demonstrated requirement needs them.

## Boundaries and transactions

- Map HTTP request models to application commands and map application results to HTTP response models. Do not expose domain entities directly from controllers.
- Do not pass `HttpContext`, `ControllerBase`, `DbContext`, provider SDK objects, or `IConfiguration` into domain or application code.
- Use the in-memory or test-double repository allowed by the assessment. Keep it behind the repository port so it can be replaced without changing the use case.
- Treat the simulator as unreliable: configure an HTTP timeout, map its `503` response to a clear dependency failure, and do not report a false authorization or decline. Do not add retries that could duplicate a payment request unless idempotency is implemented deliberately.

## Feature organization

Prefer grouping code by business capability once the API grows:

```text
Application/Payments/
  CreatePayment/
	ProcessPaymentCommand.cs
	ProcessPaymentHandler.cs
	ProcessPaymentValidator.cs
	ProcessPaymentResult.cs
  GetPayment/
	GetPaymentQuery.cs
	GetPaymentHandler.cs
	GetPaymentResult.cs
```

Keep code that changes together close together. Avoid a single global `Services`, `Helpers`, or `Utilities` folder that hides ownership and dependencies.

## Adding a dependency

Before adding a package or cross-layer reference, explain which boundary requires it and why the dependency cannot remain at the edge. Prefer framework and BCL capabilities already available in .NET 8. Verify licensing, maintenance, security, and transitive dependency impact for third-party packages.
