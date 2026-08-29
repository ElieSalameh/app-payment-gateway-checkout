---
applyTo: "**/*.cs,**/appsettings*.json"
---
# Logging standards for the payment gateway

`CLAUDE.md` section 7 is canonical. Use `ILogger<T>` from `Microsoft.Extensions.Logging`. Structured, sparing, and never leaking card data.

## Never log

- Full card numbers. The last four digits only, and only where they add diagnostic value.
- **CVV — under any circumstance, at any level, in any environment, including inside exception messages.**
- Full request or response bodies from the simulator.
- Secrets, tokens, credentials, or authorization headers.

Add a structural guard rather than relying on discipline: override `ToString()` on the request and command models so they expose only masked values. Then an accidental `logger.LogInformation("{Request}", request)` cannot leak, because the type itself refuses to render the PAN.

Define an **allowlist** of fields that may appear in a payment log: payment id, status, masked last four, currency, amount, correlation id, and the simulator outcome. Anything not on that list does not get logged.

## Level guide

| Level | Use for |
| --- | --- |
| `Trace` / `Debug` | Local diagnostics only. Disabled in Production. |
| `Information` | Payment lifecycle events: request received, sent to simulator, outcome recorded. |
| `Warning` | Validation rejections, simulator `503`, circuit-breaker transitions, not-found lookups. Expected but notable. |
| `Error` | Unhandled exceptions and unexpected simulator responses. |
| `Critical` | Startup and configuration failures. |

## Rules

- **Do not over-log.** This settles at one `Information` line per payment — the recorded outcome. "Received" and "sent to the simulator" are intentions, and the framework already logs the request line. A failed payment produces two lines by design: the inner layer logs the cause, the global handler logs the mapped status.
- Silence framework logs that duplicate ours. `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` is `None` because it logs every handled exception at `Error` with a stack trace before `IExceptionHandler` runs; `System.Net.Http.HttpClient` and `Polly` are `Warning` because they narrate a call the gateway already reports on.
- Use message templates, never string interpolation: `logger.LogInformation("Payment {PaymentId} authorized", paymentId)`. Interpolation destroys structured logging and defeats redaction.
- Use `[LoggerMessage]` source-generated partial methods for the payment-flow logs. They are allocation-free and give every event a stable id, which is what makes logs queryable later.
- Open a logging scope carrying the payment id so every line for one payment correlates, and open it with a **message template** — `logger.BeginScope("PaymentId:{PaymentId}", paymentId)` — never a `Dictionary`. The console formatter renders a scope through `ToString()`, so a dictionary prints as its type name and the id is lost; a template prints `PaymentId:9b5821a9-…` and still exposes the named property to a structured sink.
- Set `Console.IncludeScopes` to `true` in `appsettings.json`. Without it the scopes exist but are never printed, so the `traceId` in a `ProblemDetails` body has no counterpart in the log and a merchant's error report cannot be traced.
- Log **outcomes, not intentions**. "Payment authorized" is useful; "About to call the simulator" is not.
- Never log inside a tight loop.
- ASP.NET Core already logs the request and response line. Do not duplicate it with custom middleware.
- Configure levels in `appsettings.json` per environment rather than branching on the environment in code.

## Testing

Assert in the integration tests that no captured log line contains the full card number or the CVV. Capture logs with a test `ILoggerProvider` and search the rendered output for the test card numbers used. This is a required test, not an optional one — it is the check that catches a leak introduced by an unrelated change six commits later.
