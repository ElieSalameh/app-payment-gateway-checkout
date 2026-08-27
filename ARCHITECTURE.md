# Payment Gateway Checkout Architecture

## 1. Purpose and architectural style

This project is a .NET 8 ASP.NET Core payment gateway. It should be implemented as a **modular monolith** first: one deployable API with strict internal boundaries and separate projects for dependency enforcement. This keeps local development and deployment simple while allowing individual components to evolve independently.

The recommended style combines:

- Clean Architecture dependency direction.
- Domain-driven design for payment and merchant rules.
- Ports-and-adapters boundaries for databases and external providers.
- Explicit API contracts that do not expose domain entities.
- A separate test project for each meaningful production boundary.

The existing `src/PaymentGateway.Api` project is currently the ASP.NET Core template. It is the host and composition root, not the place for all business logic. The WeatherForecast template files should be removed when implementation begins.

## 2. Recommended solution layout

```text
PaymentGatewayCheckout.slnx
|
+-- src/
|   +-- PaymentGateway.Api/                 ASP.NET Core host and HTTP adapter
|   |   +-- Controllers/
|   |   +-- Middleware/
|   |   +-- Contracts/                      HTTP request/response DTOs
|   |   +-- Configuration/
|   |   +-- Program.cs                      Composition root
|   |   +-- appsettings*.json
|   |
|   +-- PaymentGateway.Application/         Use cases and inbound/outbound ports
|   |   +-- Payments/
|   |   |   +-- Commands/
|   |   |   +-- Queries/
|   |   |   +-- Services/
|   |   |   +-- Interfaces/
|   |   +-- Merchants/
|   |   +-- Common/
|   |
|   +-- PaymentGateway.Domain/              Business model and invariants
|   |   +-- Payments/
|   |   +-- Merchants/
|   |   +-- ValueObjects/
|   |   +-- Exceptions/
|   |   +-- Common/
|   |
|   +-- PaymentGateway.Infrastructure/     I/O and external-system adapters
|       +-- Persistence/
|       +-- Bank/
|       +-- Security/
|       +-- Resilience/
|       +-- Configuration/
|       +-- DependencyInjection.cs
|
+-- tests/
|   +-- PaymentGateway.Domain.Tests/         Fast invariant and value-object tests
|   +-- PaymentGateway.Application.Tests/   Use-case tests with fake/mock ports
|   +-- PaymentGateway.Infrastructure.Tests/ Adapter and persistence tests
|   +-- PaymentGateway.Api.IntegrationTests/ HTTP pipeline and endpoint tests
|   +-- PaymentGateway.ArchitectureTests/   Dependency-rule tests (optional)
|
+-- build/
|   +-- Directory.Build.props               Shared compiler settings
|   +-- Directory.Packages.props            Central package versions, if adopted
|
+-- docs/
	+-- adr/                                Architecture Decision Records
```

### When to add another project

Create a project when it protects a meaningful boundary, has an independent test strategy, or has a separate deployment/runtime concern. Do not create projects merely to move a few files. The four production projects above are appropriate for this payment gateway because they isolate business rules, use cases, external I/O, and the HTTP host.

A separate `PaymentGateway.Contracts` project is optional. Add it only when contracts must be shared with another service, SDK, or consumer. Otherwise keep HTTP DTOs in the API project to avoid creating a shared model dependency prematurely.

## 3. Dependency direction

Dependencies must point toward business policy:

```text
PaymentGateway.Api ----------------------> PaymentGateway.Application
	   |                                            |
	   +--------------------------------------------+
	   |                                            v
	   +------------------------------> PaymentGateway.Domain

PaymentGateway.Infrastructure ----------> PaymentGateway.Application
	   |
	   +--------------------------------> PaymentGateway.Domain
```

Rules:

- `Domain` references no other application project and no infrastructure, web, database, or logging implementation.
- `Application` references `Domain` and defines interfaces for required external capabilities.
- `Infrastructure` implements Application interfaces and may reference `Domain`.
- `Api` references `Application` and `Infrastructure` only to register implementations in `Program.cs`.
- `Domain` and `Application` must not reference ASP.NET Core types such as `HttpContext`, `ControllerBase`, or `IFormFile`.
- Controllers call application use cases; they do not query a DbContext, call `HttpClient`, or contain payment rules.
- Infrastructure types do not leak through API responses.
- Project references must make these rules visible in the `.csproj` files; namespaces alone are not sufficient protection.

## 4. Responsibilities by layer

### Domain

Owns rules that must always be true regardless of how the system is called:

- Payment and merchant entities or aggregates.
- Payment lifecycle and allowed state transitions.
- Value objects such as `Money`, `MerchantId`, and `PaymentId` where useful.
- Domain exceptions and domain events, if required.
- Pure policies that do not perform I/O.

The domain should be deterministic and easy to test. It must not know whether data is stored in SQL, memory, or an external provider.

### Application

Owns the payment gateway use cases:

- Authorize payment.
- Retrieve payment for the authenticated merchant.
- Issue a merchant token.
- Validate idempotency and coordinate payment state changes.
- Define ports such as `IPaymentRepository`, `IMerchantRepository`, and `IBankClient`.
- Define application-level results and errors.
- Apply cancellation, authorization context, and transaction boundaries to use cases.

Use-case handlers or application services should orchestrate; they should not contain HTTP serialization or database-specific queries.

### Infrastructure

Owns implementations of external concerns:

- Entity Framework Core `DbContext`, migrations, repositories, and database transactions.
- The acquiring-bank HTTP client using `IHttpClientFactory`.
- JWT signing and HMAC cryptographic integration where those details are not domain policy.
- Resilience policies, timeouts, and provider error translation.
- Cache, clock, ID generation, and other replaceable services.
- Observability exporters and hosted reconciliation jobs.

External clients must have typed options, explicit timeouts, structured logging, and tests that do not call a real provider.

### API

Owns transport concerns only:

- Routing, authentication/authorization configuration, model binding, and HTTP status codes.
- Request/response DTOs and mapping at the boundary.
- Middleware for exception mapping, correlation, request logging, and request signing.
- OpenAPI/Swagger configuration.
- Dependency-injection composition in `Program.cs`.
- Health endpoints and API versioning policy.

Use RFC 7807 `ProblemDetails` for consistent errors. Do not return stack traces, database errors, provider responses, PAN, CVV, or access tokens in API responses.

## 5. Payment request flow

The normal authorization flow should be explicit and observable:

1. HTTPS terminates at the API boundary.
2. Authentication validates the merchant token and establishes the merchant identity.
3. Authorization ensures the merchant can perform the requested operation.
4. Request validation checks shape, required fields, ranges, currency, and card-data rules.
5. HMAC validation verifies the raw request body using a constant-time comparison when request signing is part of the contract.
6. The application checks the idempotency key in the authoritative store, scoped to the merchant.
7. A `Processing` payment record is persisted before calling the bank.
8. The application calls the bank through `IBankClient`; the API does not call the provider directly.
9. The result is persisted as `Authorized` or `Declined` and returned using an API response DTO.
10. A bank timeout or ambiguous response remains visible as `Processing` for reconciliation; it must not be blindly retried if the provider operation is not idempotent.

Idempotency must be enforced by a database uniqueness constraint or equivalent atomic operation. An in-memory cache can improve latency but cannot be the source of truth in a multi-instance deployment.

## 6. Persistence and consistency

The current reference implementation uses in-memory repositories for an exercise. Production implementation should use a durable relational database, normally through EF Core:

- Store only the minimum payment data required by the business and compliance obligations.
- Persist a payment record before an external charge/authorization call.
- Add a unique constraint on `(MerchantId, IdempotencyKey)`.
- Use optimistic concurrency for payment updates.
- Keep external calls outside a long-running database transaction.
- Use an outbox or durable work queue when a state change must publish an event reliably.
- Add a reconciliation process for payments left in `Processing` after an ambiguous provider result.
- Treat migrations as reviewed source code and apply them through deployment automation.

The repository interface belongs to Application; its EF Core implementation belongs to Infrastructure.

## 7. Security and sensitive payment data

Payment data requires stricter handling than ordinary application data:

- Never log CVV, full card number, authorization headers, merchant secrets, JWT signing keys, or HMAC secrets.
- Do not persist CVV. Prefer tokenization and store only a provider token or the last four digits where necessary.
- Keep secrets out of source control and ordinary `appsettings.json`.
- Use .NET User Secrets for local development and a managed secret store such as Azure Key Vault in deployed environments.
- Validate issuer, audience, signature, and expiry for JWTs.
- Derive merchant identity from authenticated server-side claims; never trust a client-supplied merchant ID.
- Use HTTPS outside explicitly isolated local development.
- Use constant-time comparison for signatures and secrets.
- Apply rate limiting and account/provider abuse controls before production launch.
- Review PCI DSS scope with the security/compliance owner before accepting real card data.

## 8. Configuration and observability

Use strongly typed options with validation at startup for bank, JWT, database, and resilience settings. Configuration precedence should be environment-specific configuration, environment variables/secret providers, and local User Secrets as appropriate; secrets must not be committed.

Use structured `ILogger` messages with a correlation/trace ID. Record request method, route, status, duration, payment ID, and merchant ID only when those values are safe. Add metrics for authorization outcomes, provider latency, timeouts, circuit state, idempotency hits, and payments stuck in `Processing`.

Use OpenTelemetry for traces and metrics when deployment observability is defined. Health checks should distinguish process readiness from dependency readiness; do not expose sensitive dependency details publicly.

## 9. Testing architecture

| Test project | Test focus | Dependencies allowed |
|---|---|---|
| `PaymentGateway.Domain.Tests` | State transitions, value objects, invariants, pure policies | Domain only |
| `PaymentGateway.Application.Tests` | Use cases, authorization scope, idempotency decisions, error mapping | Application and Domain; fakes preferred |
| `PaymentGateway.Infrastructure.Tests` | Repository behavior, EF mappings, bank adapter translation, resilience | Infrastructure; test database/container or in-memory substitute where appropriate |
| `PaymentGateway.Api.IntegrationTests` | Routing, middleware order, auth, serialization, status codes, full application composition | API host via `WebApplicationFactory`, test dependencies |
| `PaymentGateway.ArchitectureTests` | Project dependency rules and forbidden references | Architecture test library; optional |

Test naming should describe behavior, for example `AuthorizePayment_WhenIdempotencyKeyExists_ReturnsOriginalPayment`. Unit tests must be deterministic and independent. Integration tests should use isolated data and fake bank responses; they must not depend on a developer's running database or internet service.

Use xUnit as the test framework, `Microsoft.NET.Test.Sdk` and the Visual Studio test adapter for discovery, and `Microsoft.AspNetCore.Mvc.Testing` for API integration tests. Add FluentAssertions, a mocking library, WireMock.Net, Testcontainers, or an equivalent only when the test boundary needs it; do not add packages without a test or runtime justification.

## 10. Architecture decisions to record

Create an ADR under `docs/adr/` when a decision affects boundaries or operational behavior. At minimum, record decisions for:

- Database/provider selection and migration strategy.
- Idempotency and payment reconciliation behavior.
- Card-data/tokenization and PCI scope.
- External bank timeout, retry, and circuit-breaker policy.
- Authentication, secret storage, and key rotation.
- API versioning and backwards compatibility.

Each ADR should state the context, decision, alternatives considered, consequences, and review date.
