---
applyTo: "**/*.cs,**/*.csproj,**/*.props,**/*.targets"
---
# .NET 8 development standards for this assessment

Use `.github/project-context/project-scope.md` as the functional source of truth. Favor simple, readable code that fulfills payment processing and retrieval; do not add production infrastructure that is not required.

## General rules

- Target the repository's configured .NET version (`net8.0`) unless a task explicitly requires otherwise.
- Keep nullable reference types enabled. Fix nullability warnings instead of suppressing them with `!` or disabling nullable analysis.
- Prefer clear, intention-revealing code over clever abstractions. Do not add an abstraction until it protects a boundary or is reused.
- Follow the existing naming and formatting conventions. Use PascalCase for types and public members, camelCase for parameters and locals, and `_camelCase` for private fields.
- Keep production code free of developer comments. Make intent clear through descriptive names for variables, methods, types, and parameters, plus small focused methods and straightforward control flow.
- Keep public APIs small. Make types and members `internal` unless they are part of a deliberate application or library contract.
- Prefer immutable data. Use `record` types for value-like request, response, and message models when appropriate.
- Use guard clauses and validate inputs at the boundary where they enter the application.

## Async and resource management

- Use `async`/`await` for I/O-bound work and pass `CancellationToken` through application and infrastructure boundaries.
- Do not use `.Result`, `.Wait()`, or synchronous blocking around asynchronous operations.
- Do not use `Task.Run` to make naturally asynchronous I/O asynchronous.
- Use `IAsyncEnumerable<T>` only when streaming is part of the contract; otherwise return a materialized result with an explicit shape.
- Dispose `IDisposable` and `IAsyncDisposable` resources deterministically. Let dependency injection own the lifetime of registered services.
- Use `HttpClient` through `IHttpClientFactory`; never create a new `HttpClient` per request.

## Dependency injection and configuration

- Register services in one clear composition root, normally `Program.cs` or extension methods called by it.
- Use constructor injection. Avoid service locator patterns and direct calls to `IServiceProvider` in application code.
- Choose service lifetimes deliberately: singleton services must be thread-safe, scoped services must not be injected into singletons, and transient services should be lightweight.
- Bind configuration such as the bank simulator URL and supported currencies to typed options with `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>` as appropriate. Validate required options at startup.
- Never store credentials, API keys, tokens, or card data in source control, logs, configuration committed to the repository, or exception messages.

## ASP.NET Core behavior

- Keep controllers or endpoint handlers thin: validate, call a payment use case, and map the result to HTTP. Authentication is not part of the stated assessment requirements.
- Use the framework's built-in dependency injection, routing, model validation, logging, and problem-details support before adding custom equivalents.
- Return consistent `ProblemDetails` responses for errors. Do not expose stack traces or infrastructure details to clients.
- Use structured logging with stable event names and properties. Do not interpolate sensitive values into log messages.
- Make HTTP status codes part of the API contract. Document the chosen responses for rejected input, unknown payment IDs, and simulator failures; do not invent endpoint behavior that is not needed by the assessment.
- Document public HTTP contracts with OpenAPI and keep examples synchronized with the implementation.

## Quality gates

- Build with warnings visible and treat new warnings as defects.
- Add or update automated tests for behavior changes.
- Keep methods focused and avoid classes that mix transport, business, persistence, and external-provider concerns.
- Prefer the BCL and existing ASP.NET Core packages. Do not add a database, messaging library, payment SDK, or authentication package unless a requirement justifies it.
- Do not catch exceptions unless the code can add useful context, translate the failure, recover, or enforce a boundary policy. Preserve the original exception as the inner exception when rethrowing.
