# Payment Gateway Checkout

[![Build](https://github.com/ElieSalameh/app-payment-gateway-checkout/actions/workflows/build.yml/badge.svg)](https://github.com/ElieSalameh/app-payment-gateway-checkout/actions/workflows/build.yml)

A .NET 8 Web API that sits between a merchant and an acquiring bank. A merchant submits a card payment for authorization and later retrieves that payment by its identifier.

The gateway validates the request, calls a simulated acquiring bank, stores the outcome in an in-memory repository, and returns payment details with the card number masked. The full card number and the CVV are never stored, never logged, and never returned.

**Status: complete.** Both endpoints are implemented and covered by 193 automated tests. The sections below describe what the code does, not what it is intended to do.

## Contents

- [Quick start](#quick-start)
- [Trying it by hand](#trying-it-by-hand)
- [Running the tests](#running-the-tests)
- [API contract](#api-contract)
- [Bank simulator](#bank-simulator)
- [Architecture](#architecture)
- [Design decisions and assumptions](#design-decisions-and-assumptions)
- [Security and data handling](#security-and-data-handling)
- [What I would add next](#what-i-would-add-next)
- [Scope boundaries](#scope-boundaries)

## Quick start

### Prerequisites

- **.NET 8 SDK** — every project targets `net8.0`.
- **.NET SDK 9.0.200 or newer**, additionally, because the solution file is `PaymentGatewayCheckout.slnx`. The `.slnx` format is only understood from that SDK version onwards, so an older CLI fails at `dotnet restore` before anything compiles. Installing both side by side is supported and is what CI does. To build without it, pass the project files directly instead of the solution.
- **Docker**, to run the bank simulator.

### 1. Start the bank simulator

From the repository root:

```powershell
docker-compose -f docker/docker-compose.yml up
```

It listens on `http://localhost:8080`, which is the address `BankSimulator:BaseAddress` points at. Confirm it answers before starting the gateway — if it is down, every payment correctly returns `502`:

```powershell
Invoke-RestMethod -Uri http://localhost:8080/payments -Method Post -ContentType application/json -Body '{"card_number":"2222405343248877","expiry_date":"04/2030","currency":"GBP","amount":100,"cvv":"123"}'
```

<details>
<summary>No Docker available?</summary>

The simulator is [mountebank](https://www.mbtest.org/), a Node tool, so it can run directly from the same imposter configuration:

```powershell
npx -y mountebank@2.8.1 --configfile docker/imposters/bank_simulator.ejs --allowInjection
```

This writes an `mb.pid` file, which is git-ignored. Stop it with `npx -y mountebank@2.8.1 stop`.

</details>

### 2. Run the gateway

```powershell
dotnet restore .\PaymentGatewayCheckout.slnx
dotnet run --project .\src\PaymentGateway.Api\PaymentGateway.Api.csproj
```

It listens on `https://localhost:55740` and `http://localhost:55741`. HTTPS redirection is always on, so use the HTTPS address — a plain HTTP `POST` receives a `307` that most clients will not follow with the body attached. Run `dotnet dev-certs https --trust` once if the certificate is not yet trusted.

### 3. Confirm it is up

Open `https://localhost:55740/swagger`. Swagger UI is served in the Development environment only, which the launch profile sets, and the launch profile opens it for you. There is no health endpoint — the brief does not ask for one, and adding unrequested endpoints is the over-engineering it warns against.

## Trying it by hand

`src/PaymentGateway.Api/PaymentGateway.Api.http` contains every request below and runs directly in Visual Studio, or in VS Code with the REST Client extension. The equivalents in PowerShell:

**An authorized payment** — the simulator authorizes card numbers ending in an odd digit:

```powershell
Invoke-RestMethod -Uri https://localhost:55740/payments -Method Post -ContentType application/json -Body '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":100,"cvv":"123"}'
```

`201 Created`, a `Location` header pointing at the new payment, and:

```json
{
  "id": "b9877a5a-e8a7-4a39-b804-90f740a4c7dc",
  "status": "Authorized",
  "lastFourCardDigits": "8877",
  "expiryMonth": 4,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 100
}
```

**A declined payment** — card numbers ending in an even digit, for example `2222405343248112`. Also `201`, with `"status": "Declined"`. The bank answered and the payment was stored, so the resource exists either way.

**A rejected request** — anything that fails validation, for example `"cvv": "1"`. Returns `400` with the field-level errors, `"paymentStatus": "Rejected"`, and no payment id. Check the simulator's console: it received nothing.

**An unavailable bank** — card numbers ending in `0`, for example `2222405343248870`, make the simulator return `503`. The gateway returns `502`, not a decline, and stores nothing.

**Retrieving a payment** — using the `id` from the first call:

```powershell
Invoke-RestMethod -Uri https://localhost:55740/payments/b9877a5a-e8a7-4a39-b804-90f740a4c7dc
```

`200 OK` with the same body. An unknown id returns `404`; an id that is not a GUID is rejected by routing before any code runs.

**Check the log while you do this.** Every line carries `TraceId`, and payment lines carry `PaymentId`, so the `traceId` in any error body can be found in the log. Search the output for `2222405343248877` and `123` — neither the card number nor the CVV appears anywhere, on any path.

## Running the tests

```powershell
dotnet test .\PaymentGatewayCheckout.slnx
```

193 tests, no external dependencies — the suite never calls the real simulator.

| Project | Tests | Covers |
| --- | --- | --- |
| `PaymentGateway.Domain.Tests` | 65 | Value object invariants: `Money` rejecting zero and negatives, `Currency` limited to the supported three, `CardDetails` exposing only the last four digits, expiry arithmetic at month boundaries |
| `PaymentGateway.Application.Tests` | 78 | Every validation rule at its boundaries (13/14/19/20-character card numbers, month 0/1/12/13, expiry last month / this month / next month, 2/3/4/5-character CVV), and both handlers with the ports faked |
| `PaymentGateway.Infrastructure.Tests` | 27 | The bank client against a stubbed `HttpMessageHandler`: snake_case body, `MM/yyyy` expiry, authorized and declined mapping, `503`, unreadable body, timeout, open circuit; and the in-memory repository including concurrent writes |
| `PaymentGateway.Api.IntegrationTests` | 23 | The full pipeline through `WebApplicationFactory` with the bank stubbed: status codes, `ProblemDetails` shapes, retrieval, and log redaction |

The build workflow runs the same command on Linux for every pull request, so the badge reflects the suite independently of any one machine.

## API contract

### Supported currencies

`GBP`, `USD`, and `EUR`. Exactly three, as the brief requires. Any other currency code is rejected.

### Status codes

| Status | When | Body |
| --- | --- | --- |
| `201 Created` | The payment reached the acquiring bank and was `Authorized` or `Declined` | `PaymentResponse` |
| `200 OK` | An existing payment was retrieved | `PaymentResponse` |
| `400 Bad Request` | The request failed gateway validation and was **rejected** | `ValidationProblemDetails` |
| `404 Not Found` | No payment exists for the given id | `ProblemDetails` |
| `502 Bad Gateway` | The bank simulator was unavailable, unreachable, or answered unreadably | `ProblemDetails` |
| `504 Gateway Timeout` | The bank simulator did not respond in time | `ProblemDetails` |
| `500 Internal Server Error` | Anything unexpected | `ProblemDetails` |

Both `Authorized` and `Declined` are successful processing, so both return `201`. A `502` or `504` is a dependency failure with an **unknown** outcome. It is never reported as `Declined`, and nothing is stored.

### Payment response

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Authorized",
  "lastFourCardDigits": "8877",
  "expiryMonth": 4,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 100
}
```

`status` is `Authorized` or `Declined` only. `Rejected` never appears here, because the brief states that a rejected request creates no payment, so there is no payment resource to return.

### Rejected response

A rejected request returns `400` with an RFC 7807 body. The `paymentStatus` member carries the brief's third outcome, and `traceId` lets a merchant's report be traced in the logs without exposing anything internal.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-...",
  "paymentStatus": "Rejected",
  "errors": {
    "cvv": ["CVV must be 3 or 4 digits."],
    "expiryYear": ["Expiry date must be in the future."]
  }
}
```

A rejected request produces **no** stored payment, **no** payment id, and **no** call to the bank simulator. Error keys are camelCase, matching the rest of the public API, and error messages never echo the submitted value — a card number or CVV must not escape through an error body.

### Validation rules

| Field | Rules |
| --- | --- |
| `cardNumber` | Required, 14–19 characters, digits only |
| `expiryMonth` | Required, 1–12 |
| `expiryYear` | Required, four digits, and the month/year combination must not have passed |
| `currency` | Required, exactly 3 characters, one of `GBP`/`USD`/`EUR` |
| `amount` | Required integer in minor units, greater than zero |
| `cvv` | Required, 3–4 characters, digits only |

A card is expired once the calendar has moved past its expiry month, so a card expiring this month is valid until the month ends — which is what the card networks mean by an expiry date.

## Bank simulator

The simulator accepts `POST http://localhost:8080/payments` with a snake_case body and a single `MM/yyyy` expiry string:

```json
{
  "card_number": "2222405343248877",
  "expiry_date": "04/2030",
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

and answers:

```json
{
  "authorized": true,
  "authorization_code": "0bb07405-6d44-4b50-a14f-7ae0beff13ad"
}
```

Its behaviour is driven by the last digit of the card number: odd authorizes, even declines, `0` returns `503 Service Unavailable`, and a missing field returns `400`.

The gateway's own API is camelCase with separate expiry month and year fields. The translation between the two shapes belongs to the infrastructure layer and does not leak inwards.

### Bank client decisions

The `BankSimulator` configuration section binds to `BankSimulatorOptions` and is validated at startup, so a missing or malformed address fails the app at boot rather than on the first payment:

```json
"BankSimulator": {
  "BaseAddress": "http://localhost:8080",
  "TimeoutInSeconds": 10
}
```

**Retry is disabled, deliberately.** Processing a payment is not idempotent and this gateway implements no idempotency key, so a retry of a request that already reached the simulator could charge a cardholder twice. A double charge is far worse than asking a merchant to resubmit, so the gateway retries nothing and reports `502` or `504` instead, leaving the decision with the caller. The circuit breaker and the timeout from `Microsoft.Extensions.Http.Resilience` are kept.

**The resilience pipeline owns the timeout, not `HttpClient`.** `HttpClient.Timeout` is set to infinite on purpose: it is enforced outside the handler chain, so a timeout raised there is invisible to the circuit breaker and could never open it. The pipeline's attempt and total timeouts come from `TimeoutInSeconds`.

**Any unsuccessful response is an unknown outcome.** Only a `200` carrying the documented body produces `Authorized` or `Declined`. A `503`, any other error status, an unreachable host, or a `200` whose body cannot be read all raise a dependency failure and store nothing. The gateway never infers `Declined` from a transport failure, because telling a merchant a payment failed when it may have succeeded is the most damaging error it could make.

**An open circuit is a dependency failure too.** When repeated failures trip the breaker, Polly raises `BrokenCircuitException` instead of attempting the call. The client translates it to the same `AcquiringBankUnavailableException` as any other unreachable-bank case, so a sustained outage keeps returning `502` rather than degrading into `500`. This was found by driving the running API rather than by reading it, which is why the case now has its own test.

**A timeout is distinguished from a cancellation.** The pipeline raises Polly's `TimeoutRejectedException`, which the client translates into `AcquiringBankTimeoutException` at the infrastructure boundary. That keeps Polly types out of the API layer and avoids treating `TaskCanceledException` — which is also what a merchant disconnecting produces — as a gateway timeout.

The full card number and CVV exist only in the request DTO and the outbound call to the simulator. `AuthorizationRequest` and the bank's own request DTO both override `ToString()` to expose neither, so an accidental log of either object cannot leak them.

## Architecture

Four projects. Dependencies point inward, and the split is enforced by the compiler rather than by convention:

```text
src/
  PaymentGateway.Api/             # controllers, contracts, middleware, composition root
  PaymentGateway.Application/     # use cases, validation, ports, results
  PaymentGateway.Domain/          # payment state, value objects, invariants
  PaymentGateway.Infrastructure/  # bank HTTP client, in-memory repository

tests/
  PaymentGateway.Domain.Tests/
  PaymentGateway.Application.Tests/
  PaymentGateway.Infrastructure.Tests/
  PaymentGateway.Api.IntegrationTests/
```

- **`Domain`** has zero project references and no packages beyond the BCL. It is a separate assembly precisely so that "the business rules depend on nothing" is a compiler guarantee rather than a folder convention that quietly erodes.
- **`Application`** references `Domain` only. It declares the two ports it needs — `IAcquiringBankClient` and `IPaymentRepository` — without knowing that one is HTTP and the other a dictionary.
- **`Infrastructure`** references `Application` and `Domain` and implements those ports. Replacing the in-memory store with SQL touches one file.
- **`Api`** references `Application`, and `Infrastructure` only at the composition root. Its job is HTTP: routing, serialization, status codes.

Two organising principles are used deliberately. `Api` is arranged by transport role (`Controllers/`, `Contracts/`, `Middleware/`, `Configuration/`) because that is the conventional ASP.NET Core shape a reviewer can navigate without orientation. The inner layers are arranged by capability (`Payments/ProcessPayment/`, `Payments/GetPayment/`, `Bank/`, `Persistence/`) because there the code that changes together is the code for one use case.

The four-project split is the layout Microsoft's reference applications use, so it reads without explanation. The brief's warning about over-engineering is aimed at speculative *features* — CQRS buses, a mediator, an outbox, event sourcing, idempotency protocols — none of which are here.

## Design decisions and assumptions

**Storage is in memory, behind a port.** `InMemoryPaymentRepository` is a `ConcurrentDictionary` registered as a singleton, as the brief allows. `Add` throws rather than overwriting an existing id: a duplicate means a bug upstream, and silently replacing a stored payment would lose the record of a real charge. Payments do not survive a restart, which is acceptable for an assessment and is the first thing production would change.

**`PaymentId` is a value type, not a `Guid`.** The repository port takes `PaymentId`, so the raw `Guid` from the route is converted once at the edge and travels typed from there. Preventing identifier mix-ups at the repository boundary is the entire reason the type exists, so that boundary is exactly where the primitive must not reappear.

**A payment is `Authorized` or `Declined` — never `Rejected`.** The brief states that a rejected request creates no payment, so there is nothing to give a status to. `Rejected` exists only as the shape of the `400`, which is why both the domain and the wire enums hold exactly two values.

**Validation lives in `Application`, on the command, not on the HTTP request.** `Rejected` is a business outcome the brief names, so the rule producing it belongs with the use case where a second entry point cannot bypass it. The command carries every field as nullable, because "required" is one of the rules the validator must be able to fail — binding a missing field would otherwise crash before validation could report it.

**Validation failures travel as an exception.** `ProcessPaymentHandler` calls `ValidateAndThrowAsync`, and `GlobalExceptionHandler` turns the result into the `400` body. Returning a result union instead would need a second refusal vocabulary in the application layer and would rebuild the rejection body at the controller, while the wire `PaymentStatus` still could not carry `Rejected`. One refusal shape, built in one place, is worth the throw.

**Error mapping is ours, not the framework's.** `GlobalExceptionHandler` implements .NET 8's `IExceptionHandler`, so the whole exception-to-status mapping is readable in one file and a wrong mapping is a one-line fix. Framework defaults had already hidden two real defects here — internal .NET type names leaking into `400` bodies, and a `[Produces]` attribute silently overriding the `problem+json` content type — neither visible in the source, both found by calling the running API.

**Money is a `long` of minor units paired with a currency.** Never binary floating point, and never an amount without its currency attached.

**Time is injected.** Expiry rules take `TimeProvider`, so the expiry tests do not rot on a fixed date, and `CardDetails.IsExpired` is the single definition of expiry in the codebase — two definitions that drift apart would be a live payments defect.

**The code carries no comments.** Names, small methods, and named constants are expected to carry the meaning; explanation lives in this document instead. Test names read as the specification.

**Assumptions made where the brief is silent:** a processed payment is `201` with a `Location` header rather than `200`; retrieval of an unknown id is `404` rather than an empty `200`; currency codes are matched case-insensitively but stored canonically uppercase; the `authorization_code` returned by the bank is not part of the documented response contract, so it is not stored or returned; and there is no pagination or listing endpoint, because the brief asks only for retrieval by id.

## Security and data handling

- Use test card values only; never use real card data.
- Never return, log, persist, or include the full card number or CVV in an exception.
- Store only the last four card digits and the payment details required for retrieval.
- Do not commit credentials, tokens, or secrets.
- Treat a simulator timeout or `503` as a dependency failure, not as a decline.

Implemented at the boundary: request body size capped at 4 KB, unknown JSON properties rejected rather than ignored, GUID route constraints so malformed ids never reach application code, HTTPS redirection with HSTS outside Development, the `Server` header removed, CORS closed by default, and Swagger served in Development only. Card masking happens in exactly one place — `CardDetails.FromCardNumber` — and nothing else in the codebase masks a card number.

### There is no authentication, and no merchant identity

This is the most important assumption in the solution, so it is stated plainly rather than left to be discovered.

**Both endpoints are open to any caller that can reach the port.** There is no API key, no mTLS, no merchant identity anywhere in the pipeline: not on `ProcessPaymentCommand`, not on `Payment`, not in the repository lookup. Two consequences follow, and neither is acceptable in production:

- `POST /payments` is a **card-testing oracle**. An attacker holding stolen card numbers can submit them and learn which are live from the `Authorized` versus `Declined` split, at the acquiring bank's expense. This is a routinely exploited attack against unauthenticated payment endpoints.
- `GET /payments/{id}` performs **no ownership check**. Anyone holding a payment id can read that payment's status, last four digits, expiry, currency, and amount. The only obstacle is that a version 4 GUID is not guessable — obscurity, not authorization.

The reason it is absent is scope, not oversight. The brief defines the merchant as an actor — "the seller of the product" — and never as data: it appears in neither the request table nor either response table, and the stated requirements are only that a merchant can process and retrieve a payment. The brief also asks for an architecture "focused on meeting the functional requirements" and explicitly discourages over-engineering.

A partial version was considered and rejected. Threading an unauthenticated `MerchantId` through the command and onto the payment would enforce nothing while resembling an access control, which is worse than a documented absence — a field that looks like a security boundary and is not one invites exactly the wrong assumption.

## What I would add next

In the order I would do them, given more time:

1. **Merchant authentication and per-merchant scoping.** An API key or mTLS scheme with constant-time comparison (`CryptographicOperations.FixedTimeEquals`), keys mapped to merchant identities, and secret storage with rotation. Then a merchant identifier threaded through the command, stored on the `Payment`, and applied to the retrieval query so one merchant receives `404` for another's payment. This closes both problems described above and is the only item on this list that is genuinely mandatory before production.
2. **Idempotency keys on `POST /payments`.** The reason retry is disabled today is that a resubmission could double-charge. An idempotency key stored with the payment makes a repeat of the same request safe, which in turn makes a retry policy safe to enable.
3. **Rate limiting**, using the built-in .NET 8 limiter, returning `429` with `Retry-After`. Per merchant once merchants exist; per IP before that. It also blunts the card-testing attack.
4. **Durable storage.** The repository is already behind a port, so this is one new implementation plus a connection string, with the existing tests unchanged as the contract.
5. **Tokenisation or provider-hosted fields**, so the raw card number never reaches this API at all. This is the change that removes most of the PCI surface rather than merely handling it carefully.
6. **Structured log shipping and metrics** — authorization rate, decline rate, bank latency percentiles, circuit-breaker state transitions. The scopes and event ids are already in place for this; what is missing is somewhere to send them.
7. **A payments listing endpoint with pagination**, once merchant scoping exists to make it safe.

## Scope boundaries

The following are deliberately not implemented:

- A real database or durable distributed storage.
- A live card-network or acquiring-bank integration.
- Capture, refund, recurring billing, or other payment operations.
- Webhooks, reconciliation jobs, message brokers, or an outbox.
- Authentication, merchant identity, or a rate limiter.
- Speculative abstractions and unrelated infrastructure.

## Related documentation

- [Assessment brief](.github/instructions/README.md)
- [Project scope](.github/project-context/project-scope.md)
- [Architecture instructions](.github/instructions/architecture.instructions.md)
- [Testing instructions](.github/instructions/testing.instructions.md)
- [API and security instructions](.github/instructions/api-security.instructions.md)
