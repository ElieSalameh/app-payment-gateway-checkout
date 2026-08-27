---
applyTo: "**/*Tests/**/*.cs,**/*.Tests/**/*.cs,**/*IntegrationTests/**/*.cs"
---
# Testing standards for the payment gateway

Use `.github/project-context/project-scope.md` as the test scope. Prioritize the two required use cases—processing a payment and retrieving a payment—and do not spend test effort on out-of-scope features.

## Test strategy

Use the test pyramid: many fast unit tests, a useful number of integration tests, and a small number of end-to-end tests. Test behavior and business outcomes rather than implementation details.

| Test type | Use it for | Typical tools |
| --- | --- | --- |
| Unit | Domain invariants, value objects, pure mapping, and application decisions with ports replaced by deterministic test doubles | xUnit, built-in assertions, focused fakes |
| Integration | Serialization, dependency injection, middleware, HTTP behavior, and application-to-bank/repository wiring | xUnit, `WebApplicationFactory<TEntryPoint>`, a fake `HttpMessageHandler` or the supplied simulator |
| Contract | The API shape and provider/client agreements that must remain compatible | ASP.NET Core integration tests, OpenAPI validation, provider sandbox where available |
| End-to-end | A few critical journeys through the API and supplied simulator, such as payment authorization and retrieval | Local API, simulator, isolated test data |
| Performance/load | Throughput, latency, concurrency, and provider failure behavior under an agreed workload | A dedicated load-test tool and representative environment |

Use xUnit as the default test framework for new .NET test projects unless the repository already standardizes on another framework. Do not add a second test framework to solve a problem that the existing framework can handle.

## Test project layout

Keep tests separate from production projects and mirror the production boundaries:

```text
tests/
  PaymentGateway.Domain.Tests/
  PaymentGateway.Application.Tests/
  PaymentGateway.Api.IntegrationTests/
```

- Domain tests should reference only the domain project.
- Application tests may use fakes for ports and should not require a web server or real database.
- Integration tests may reference the API and infrastructure projects and should exercise the real composition path with the in-memory repository and a controlled bank client.
- End-to-end tests should be isolated from the normal unit-test run and require explicit environment configuration.

## What to test

- Test every payment validation rule: card number length and digits, expiry month/year, supported currency, integer amount, and CVV length and digits.
- Test that invalid input returns `Rejected` behavior and does not call the bank simulator.
- Test simulator mapping: odd-ending cards become `Authorized`, non-zero even-ending cards become `Declined`, and a zero-ending card or `503` becomes a dependency failure rather than a payment result.
- Test payment persistence, retrieval by ID, and the chosen unknown-ID response.
- Test mapping to stable HTTP status codes and `ProblemDetails` responses.
- Test card masking and verify that full card numbers and CVV values do not appear in response models or captured logs.
- Test the bank adapter against a deterministic fake HTTP handler. Add a test using the supplied simulator when validating its actual HTTP contract.
- Do not add database, queue, or deployment tests when the implementation uses the assessment-approved in-memory repository.
- Test observability behavior only when it is part of an operational contract, such as a required audit event or correlation identifier.
- Do not test framework behavior, private methods, trivial property accessors, or the exact internal call sequence unless that sequence is a business requirement.

## Unit test design

- Follow Arrange/Act/Assert and keep each test focused on one observable outcome.
- Name tests as `MethodOrScenario_Condition_ExpectedOutcome`, for example `ProcessPayment_WhenCardNumberIsEven_ReturnsDeclined`.
- Use deterministic clocks, IDs, random values, and provider fakes. Never depend on wall-clock timing or `Random.Shared` in a unit test.
- Prefer hand-written fakes for important ports. Use mocks sparingly and verify externally visible outcomes rather than implementation call counts.
- Keep unit tests independent, order-independent, and safe to run in parallel.
- Include boundary values and failure paths, not only the successful path.

## Integration and end-to-end safety

- Use `WebApplicationFactory<Program>` to exercise the ASP.NET Core pipeline without starting an external process.
- Reset the in-memory repository between tests and never share mutable test data accidentally.
- Use the documented simulator and test card values only. Never use real card numbers, production tokens, customer data, or live provider endpoints.
- Do not assert on unstable values such as timestamps, generated IDs, or provider messages unless the test controls or normalizes them.
- Make external calls explicit and opt-in. The default test run must be safe offline and must not charge a real account.
- Keep end-to-end tests small, tagged, and separately runnable so a provider outage does not hide failures in fast feedback tests.

## Test maintenance

- Run the smallest relevant test set while developing, then run all tests before merging.
- Treat flaky tests as defects. Fix the cause; do not add arbitrary sleeps or retries around assertions.
- Keep test fixtures reusable but local enough that setup explains the scenario.
- Update tests when behavior changes; do not weaken assertions just to make a failing test pass.
- Prefer coverage of important decisions and failure modes over pursuing a percentage target.
