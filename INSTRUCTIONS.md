# Payment Gateway Checkout Development Instructions

These instructions apply to `C:\Dev\app-payment-gateway-checkout`. The application targets .NET 8 and is hosted as an ASP.NET Core Web API.

## 1. Current starting point

The repository currently contains:

- `src/PaymentGateway.Api`: a .NET 8 ASP.NET Core Web API template with Swagger.
- `PaymentGatewayCheckout.slnx`: the solution.
- Template `WeatherForecast` files that should be removed before adding payment functionality.
- No test project yet.

Open `PaymentGatewayCheckout.slnx` in Visual Studio Community 2026. Set `PaymentGateway.Api` as the startup project and use the HTTPS profile. Swagger should be enabled only for development or protected environments.

## 2. Create the recommended projects

Use Visual Studio's **Add > New Project** or the equivalent .NET CLI commands. Keep all projects targeting `net8.0`.

```text
src/PaymentGateway.Api                  ASP.NET Core Web API
src/PaymentGateway.Application          Class Library
src/PaymentGateway.Domain                Class Library
src/PaymentGateway.Infrastructure        Class Library

tests/PaymentGateway.Domain.Tests        xUnit Test Project
tests/PaymentGateway.Application.Tests  xUnit Test Project
tests/PaymentGateway.Infrastructure.Tests xUnit Test Project
tests/PaymentGateway.Api.IntegrationTests xUnit Test Project
```

Recommended production references:

```text
Api            -> Application, Infrastructure
Infrastructure -> Application, Domain
Application    -> Domain
Domain         -> no other production project
```

Recommended test references:

```text
Domain.Tests          -> Domain
Application.Tests     -> Application, Domain
Infrastructure.Tests  -> Infrastructure, Application, Domain
Api.IntegrationTests  -> Api, Application, Infrastructure
```

A test project may reference the production project under test and its legitimate dependencies. Do not add a reference from production code to a test project.

If the codebase is still small, the same folders may temporarily live under `PaymentGateway.Api`, following the same dependency rules. Split them into projects when the boundaries become useful. Do not put all code into `Program.cs` or controllers.

## 3. Where each file belongs

### Domain project

Put business concepts and rules here:

- Entities and aggregates: `Payments/Payment.cs`, `Merchants/Merchant.cs`.
- Value objects: `ValueObjects/Money.cs`, `ValueObjects/PaymentId.cs`.
- Domain enums and state transitions.
- Domain exceptions.
- Pure domain policies.

Do not put EF Core attributes, ASP.NET types, HTTP clients, configuration readers, or logging implementations here.

### Application project

Put use cases and ports here:

- Use-case handlers or services under `Payments/Commands`, `Payments/Queries`, or `Payments/Services`.
- Interfaces for external capabilities under `Common/Interfaces` or the relevant feature folder.
- Application result types and errors.
- Validation that coordinates a use case but does not require HTTP infrastructure.
- Authorization decisions based on an authenticated merchant identity.

Application services may call interfaces such as `IPaymentRepository` and `IBankClient`, but never concrete EF Core repositories or `HttpClient` directly.

### Infrastructure project

Put implementations and I/O here:

- EF Core `DbContext`, entity configurations, migrations, and repository implementations under `Persistence`.
- Typed or named bank clients under `Bank`.
- JWT/HMAC implementation details under `Security`.
- Timeouts, circuit breakers, and provider-specific resilience under `Resilience`.
- Options classes and service registration under `Configuration`.
- One `DependencyInjection.cs` extension may expose `AddInfrastructure(...)`.

Infrastructure translates provider and database failures into application-safe errors. It must not return provider-specific objects to the API.

### API project

Put HTTP and host concerns here:

- Controllers under `Controllers`.
- Request and response DTOs under `Contracts` or a feature-specific `Payments` folder.
- Middleware under `Middleware`.
- Authentication, authorization, Swagger, health checks, and dependency injection in the host setup.
- Mapping between HTTP contracts and application commands/results at the boundary.

Controllers should be thin: bind and validate the request, call one application use case, and map the result to an HTTP response. Do not put payment rules, SQL, provider calls, or large workflows in controllers.

### Test projects

Keep tests outside `src` under `tests`. Organize tests by the production feature rather than by implementation detail:

```text
tests/PaymentGateway.Domain.Tests/Payments/PaymentTests.cs
tests/PaymentGateway.Application.Tests/Payments/AuthorizePaymentTests.cs
tests/PaymentGateway.Infrastructure.Tests/Bank/BankClientTests.cs
tests/PaymentGateway.Api.IntegrationTests/Payments/PaymentEndpointsTests.cs
```

Tests should not be placed beside production files in `src` unless the team has a documented exception.

## 4. Testing rules and tools

Use the following default stack:

- **xUnit** for test execution and test organization.
- **Microsoft.NET.Test.Sdk** and the Visual Studio xUnit adapter for discovery.
- **Microsoft.AspNetCore.Mvc.Testing** with `WebApplicationFactory<Program>` for API integration tests.
- Fakes for simple application ports; use a mocking library only where a fake would obscure the behavior.
- A controlled HTTP stub such as WireMock.Net for bank-client integration behavior.
- Testcontainers or a disposable test database for database integration tests when EF Core is introduced.
- Coverlet for coverage measurement, with coverage used as a signal rather than a target by itself.

Do not use a real bank, production database, real merchant secret, or internet dependency in automated tests.

Test categories:

| Category | What to test | Normal speed |
|---|---|---|
| Unit | Domain invariants, validators, application decisions, error paths | Very fast |
| Adapter/infrastructure | EF mappings, repository queries, provider response translation | Fast to moderate |
| Integration | HTTP routing, middleware order, auth, serialization, DI composition | Moderate |
| Architecture | Forbidden project references and dependency direction | Fast |
| Contract | Stable API schemas and status/error payloads | Moderate |

Every bug fix should add a regression test at the lowest layer that reproduces the bug. Every payment state transition, idempotency rule, merchant-isolation rule, and provider failure mode must have tests.

Use behavior-oriented names:

```text
AuthorizePayment_WhenRequestIsDuplicate_ReturnsTheOriginalPayment
GetPayment_WhenPaymentBelongsToAnotherMerchant_ReturnsNotFound
BankClient_WhenProviderTimesOut_MapsTheFailureWithoutRetryingACharge
```

Tests must be independent, deterministic, and safe to run in parallel unless a test explicitly owns an isolated resource.

## 5. Adding a payment feature

Follow this sequence for a new feature:

1. Define the business rule and affected payment/merchant states.
2. Add or update domain types and invariants in `PaymentGateway.Domain`.
3. Add an application use case and its input/output types.
4. Add or update application interfaces for required external behavior.
5. Implement external behavior in Infrastructure.
6. Add API request/response contracts and a thin controller endpoint.
7. Register dependencies in the composition root.
8. Add unit tests for domain and application behavior.
9. Add adapter tests for database/provider behavior.
10. Add integration tests for the HTTP contract and middleware pipeline.
11. Update OpenAPI documentation, README usage, and an ADR if the feature changes an architectural decision.

Do not start by adding code to a controller and move it later. Decide the boundary first.

## 6. API and validation conventions

- Use resource-oriented routes and explicit API versioning, such as `/api/v1/payments`.
- Use DTOs for all request and response bodies; never serialize domain entities directly.
- Use `ProblemDetails` for errors with a stable error code and correlation ID.
- Validate required fields, ranges, currency, idempotency keys, and request signatures at the appropriate boundary.
- Return `201 Created` only when a resource is created according to the API contract; use `200`, `202`, `400`, `401`, `403`, `404`, `409`, `422`, `502`, and `503` consistently with documented semantics.
- Preserve backwards compatibility for published contracts. Add a new version for breaking changes.
- Add cancellation tokens to asynchronous application, database, and HTTP operations.
- Do not expose internal exception names, stack traces, SQL, or raw provider payloads.

## 7. Security requirements

- Never commit secrets, signing keys, merchant secrets, database passwords, or real payment data.
- Use User Secrets for local development and environment/managed secret providers in deployed environments.
- Never log CVV, full card numbers, bearer tokens, HMAC values, or secret headers. Store only a provider token or last four digits when permitted by the business and compliance requirements.
- Use constant-time comparison for HMAC/signature verification.
- Derive merchant identity from the authenticated token; do not trust a merchant ID from the request body or route for authorization.
- Ensure all repository queries are scoped to the authenticated merchant.
- Use HTTPS and secure headers in deployed environments.
- Add rate limiting, key rotation, audit events, and abuse monitoring before handling real transactions.
- Treat ambiguous bank outcomes as a reconciliation problem, not as a reason to blindly retry a non-idempotent charge.

## 8. Dependency injection and configuration

- Register dependencies in the composition root or a project-specific registration extension; avoid service location.
- Prefer constructor injection.
- Use typed options with startup validation for JWT, bank, database, and resilience settings.
- Use `IHttpClientFactory` for outbound HTTP and configure an explicit timeout.
- Do not create `HttpClient`, `DbContext`, or service implementations with `new` in application code.
- Choose service lifetimes deliberately: stateless services are usually scoped or singleton only when thread-safe and immutable; DbContexts are normally scoped.
- Keep environment-specific values in configuration providers rather than conditional code.

## 9. Async, error handling, and logging

- Use asynchronous APIs end-to-end for I/O.
- Propagate `CancellationToken` and do not use `.Result`, `.Wait()`, or fire-and-forget tasks for request work.
- Map expected domain/application failures centrally to safe API responses.
- Log unexpected failures once at the boundary with a correlation ID; avoid duplicate logs at every layer.
- Use structured logging templates rather than interpolated sensitive strings.
- Include safe identifiers, operation name, outcome, duration, and provider status where useful.
- Add metrics for authorization outcomes, provider latency, timeouts, idempotency hits, and payments stuck in `Processing`.

## 10. Local development commands

Run these from the repository root in PowerShell:

```powershell
dotnet restore .\PaymentGatewayCheckout.slnx
dotnet build .\PaymentGatewayCheckout.slnx
dotnet test .\PaymentGatewayCheckout.slnx

dotnet run --project .\src\PaymentGateway.Api\PaymentGateway.Api.csproj
```

Use Visual Studio Test Explorer for focused tests while developing. Run the complete test suite before opening a pull request. If a bank simulator or database is required, start an isolated local dependency documented in the repository README and keep credentials local.

## 11. Code quality rules

The repository should keep these settings enabled for every project:

- Nullable reference types.
- Implicit usings where they improve consistency.
- Warnings treated as errors in CI after the baseline is clean.
- A shared `.editorconfig` and consistent formatting.
- Centralized package versions where the solution has enough packages to benefit from it.
- No unused template files or dead code.

Prefer clear names, small cohesive types, and explicit behavior. Avoid speculative abstractions, generic repositories, giant service classes, and methods that mix validation, persistence, HTTP, and mapping.

## 12. Pull request definition of done

Before merging a change:

- The correct project and folder boundaries are used.
- Production code has no dependency on test projects.
- Domain and application rules have unit tests.
- External I/O has isolated adapter tests.
- API changes have integration or contract tests.
- `dotnet build` and `dotnet test` pass with no new warnings.
- Secrets and sensitive payment data are absent from source, logs, test fixtures, and screenshots.
- Configuration and migrations are reviewed.
- Swagger/API documentation and README instructions are updated.
- Security, idempotency, concurrency, and failure behavior are considered.
- A new ADR is added when the change affects a documented architectural decision.
