---
applyTo: "**/*.cs,**/*.csproj,**/*.props,**/*.targets"
---
# .NET 8 development standards for this assessment

`CLAUDE.md` section 2 is canonical. `.github/project-context/project-scope.md` is the functional source of truth. Favor simple, readable code that fulfills payment processing and retrieval; do not add production infrastructure that is not required.

## Project settings

- Target `net8.0` in every project. The solution file is `PaymentGatewayCheckout.slnx`, which needs SDK **9.0.200 or newer** on the CLI; CI installs both 8 and 9.
- `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` everywhere.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Fix nullability warnings; never suppress with `!` or by disabling the analyzer.
- Put shared properties in a root `Directory.Build.props` once there is more than one project, rather than repeating them.

## General rules

- Prefer clear, intention-revealing code over clever abstraction. Do not add an abstraction until it protects a boundary or is reused.
- PascalCase for types and public members, camelCase for parameters and locals, `_camelCase` for private fields.
- **Keep production code free of developer comments.** Express intent through descriptive names for variables, methods, types, and parameters, plus small focused methods and straightforward control flow. See `file-organization.instructions.md`.
- Keep public API surface small. Types and members are `internal` unless deliberately part of a contract.
- Prefer immutable data. `record` for request, response, command, and result models. `sealed` on any class not designed for inheritance.
- `required` properties on contracts so the compiler enforces construction.
- File-scoped namespaces. A single `GlobalUsings.cs` per project rather than scattered `using` blocks.
- Target-typed `new()`, collection expressions, and pattern matching over long `if`/`else` chains.
- Guard clauses at the boundary where input enters.
- No magic values. A named `const` is how a number explains itself in a comment-free codebase.

## Money and time

- Amounts are **integers in minor units**, always paired with an explicit currency. Never `double` or `float` for money.
- Supported currencies are exactly `GBP`, `USD`, `EUR`.
- Inject `TimeProvider` and read the clock through it. Never call `DateTime.UtcNow` or `DateTime.Now` in a code path that has a rule attached — expiry validation above all, which is otherwise untestable and rots on a fixed date.

## Async and resources

- `async`/`await` for I/O. Pass `CancellationToken` through every application and infrastructure boundary and into the `HttpClient` call.
- Never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Do not use `Task.Run` to make naturally asynchronous I/O asynchronous.
- `ValueTask` only on a hot path that has been measured. `Task` otherwise.
- `IAsyncEnumerable<T>` only when streaming is part of the contract.
- Dispose `IDisposable`/`IAsyncDisposable` deterministically; let DI own registered service lifetimes.
- Obtain `HttpClient` through `IHttpClientFactory` as a typed client. Never `new HttpClient()`.

## Dependency injection and configuration

- One clear composition root: `Program.cs`, calling `AddApplication()` and `AddInfrastructure(configuration)` extension methods so it stays readable.
- Constructor injection only. No service locator, no `IServiceProvider` in application code.
- Choose lifetimes deliberately. The in-memory repository is a thread-safe singleton (`ConcurrentDictionary`). Scoped services must never be injected into singletons.
- Bind configuration to typed options and validate at startup:
  `services.AddOptions<BankSimulatorOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart();`
  `ValidateOnStart()` is required — a misconfigured app must fail at boot, not on the first payment.
- Use a `const string SectionName` on the options type instead of magic configuration keys.
- Never put credentials, tokens, or card data in source control, logs, committed configuration, or exception messages.

## HTTP resilience

- Use `Microsoft.Extensions.Http.Resilience` on the bank client for a bounded timeout and a circuit breaker.
- **Do not blindly retry the payment request.** It is not idempotent, and a retry risks double-charging. Retrying a connect-phase failure that provably never reached the simulator is defensible; retrying after the request was sent is not. Whatever is chosen, record the reasoning in `README.md`.
- Set an explicit `HttpClient.Timeout`. A hanging simulator must not hang a merchant.

## JSON

- `System.Text.Json` only. Configure serialization once in `Program.cs`.
- The gateway's public API is **camelCase**. The simulator is **snake_case** with an `MM/yyyy` expiry string.
- That translation lives in `Infrastructure` and nowhere else, via `[JsonPropertyName]` on the simulator DTOs. Never let the simulator's naming reach the public contract or the domain.
- Reject unknown properties rather than silently ignoring them.

## Validation

- **FluentValidation**, on the application command, in `Application`. Do not mix DataAnnotations and FluentValidation on the same type.
- The rules, all of which must be enforced: card number required, 14–19 characters, digits only; expiry month required, 1–12; expiry year required with month and year combined strictly in the future; currency required, exactly 3 characters, one of the three supported; amount required, integer, greater than zero; CVV required, 3–4 characters, digits only.

## ASP.NET Core behavior

- Controllers stay thin: bind, call the use case, map the result to a status code. No business logic. Authentication is not part of this assessment.
- Use the framework's routing, DI, logging, and `ProblemDetails` support before writing a custom equivalent.
- Return `ActionResult<T>` with explicit status codes and annotate with `[ProducesResponseType]` so the OpenAPI document matches reality.
- Route parameters typed as `Guid` so malformed ids fail at model binding.
- Enable Swagger in Development only.
- Structured logging with stable event names. Never interpolate values into a log message. See `logging.instructions.md`.

## Quality gates

- The build must be clean. New warnings are defects.
- Add or update tests for every behavior change.
- Do not catch an exception unless the code can add context, translate the failure, recover, or enforce a boundary policy. Preserve the original as `InnerException` when rethrowing. See `error-handling.instructions.md`.
- Keep methods focused. No type that mixes transport, business, persistence, and provider concerns.
