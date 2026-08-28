---
applyTo: "**/*Tests/**/*.cs,**/*.Tests/**/*.cs,**/*IntegrationTests/**/*.cs"
---
# Testing standards for the payment gateway

`CLAUDE.md` section 6 is canonical. `.github/project-context/project-scope.md` is the test scope. Prioritize the two required use cases — processing a payment and retrieving a payment — and spend no test effort on out-of-scope features.

## Stack

| Purpose | Package |
| --- | --- |
| Test framework | **xUnit** |
| Assertions | **FluentAssertions** |
| Test doubles | **NSubstitute** |
| API integration | **`Microsoft.AspNetCore.Mvc.Testing`** (`WebApplicationFactory<Program>`) |
| Deterministic clock | **`Microsoft.Extensions.TimeProvider.Testing`** (`FakeTimeProvider`) |

Do not add a second test framework. NSubstitute is available, but prefer a **hand-written fake** for the two ports (`IAcquiringBankClient`, `IPaymentRepository`): they are small, reused by nearly every test, and a fake pushes assertions toward outcomes rather than call counts. Reach for NSubstitute when a one-off stub is genuinely cheaper.

## Test strategy

Many fast unit tests, a useful number of integration tests, a small number of end-to-end tests. Test behavior and business outcomes, never implementation details.

| Test type | Use it for |
| --- | --- |
| Unit | Domain invariants, value objects, validation rules, handler decisions with ports faked |
| Integration | Serialization, DI, middleware, HTTP status and `ProblemDetails` behavior, the real composition path |
| End-to-end | A couple of journeys through the API and the running simulator, isolated and opt-in |

## Project layout

```text
tests/
  PaymentGateway.Domain.Tests/
  PaymentGateway.Application.Tests/
  PaymentGateway.Infrastructure.Tests/
  PaymentGateway.Api.IntegrationTests/
```

- `Domain.Tests` references the Domain project only.
- `Application.Tests` uses fakes for ports; no web server, no HTTP.
- `Infrastructure.Tests` covers the simulator client against a stubbed `HttpMessageHandler`, and the in-memory repository.
- `Api.IntegrationTests` exercises the real pipeline with the in-memory repository and a controlled bank client.
- End-to-end tests against the live simulator are isolated from the default run and require explicit environment configuration.

## What to test

**Validation** — every rule, at its boundaries: 13/14/19/20-character card numbers, month 0/1/12/13, expiry in the past, expiry this month, expiry next month, 2/3/4/5-character CVV, unsupported currency, zero and negative amounts, non-numeric card number and CVV. This is the largest suite in the project and should be table-driven with `[Theory]` and `[InlineData]`.

**Outcome mapping** — invalid input returns `Rejected` **and never calls the bank**; an odd-ending card becomes `Authorized`; a non-zero even-ending card becomes `Declined`; a zero-ending card or a `503` becomes a dependency failure, not a payment result.

**Persistence and retrieval** — an authorized or declined payment is stored; a rejected one is not; retrieval by id returns it; an unknown id returns the documented not-found response.

**HTTP contract** — the status code and `ProblemDetails` shape for each outcome: `201` on process, `200` on retrieval, `400` on rejection with field-level errors, `404` on unknown id, `502` when the simulator is unavailable, `504` on timeout.

**Data safety** — card masking works, and no full card number or CVV appears in any response model or captured log. Assert this explicitly; it is the single most important test in a payments codebase.

Do not test framework behavior, private methods, trivial property accessors, or an exact internal call sequence unless that sequence is itself a requirement. Do not add database, queue, or deployment tests.

## Test design

- Arrange / Act / Assert, separated by blank lines, one observable outcome per test.
- Name tests `MethodOrScenario_Condition_ExpectedOutcome`, for example `ProcessPayment_WhenCardNumberEndsInEvenDigit_ReturnsDeclined`. In a comment-free codebase the test names are the specification — write them that way.
- Test files carry no comments either. If a scenario needs explaining, the test name and the fixture setup are where it gets explained.
- Deterministic clocks (`FakeTimeProvider`), ids, and random values. Never depend on wall-clock timing, `DateTime.UtcNow`, or `Random.Shared`.
- Independent, order-independent, parallel-safe. No `Thread.Sleep`.
- Include boundary values and failure paths, not only the happy path.

## Integration safety

- `WebApplicationFactory<Program>` rather than starting an external process.
- Reset the in-memory repository between tests; never share mutable state accidentally.
- Use the documented simulator test cards only. Never real card numbers, production tokens, or live provider endpoints.
- The default test run must be safe offline. External calls are explicit and opt-in.
- Do not assert on unstable values — timestamps, generated ids, provider messages — unless the test controls or normalizes them.

## Maintenance

- Run the narrowest relevant tests while developing; run everything before opening a pull request.
- Flaky tests are defects. Fix the cause; never paper over one with a sleep or a retry.
- Update tests when behavior changes. Never weaken an assertion to make a failing test pass.
- Cover important decisions and failure modes rather than chasing a coverage percentage.

## CI

`.github/workflows/build.yml` currently restores and builds in Release on every pull request into `main`. It is **build-only**. Add the `dotnet test` step to that workflow as soon as the first test project exists, and keep the build green — a red build is a blocked pull request, not a warning.
