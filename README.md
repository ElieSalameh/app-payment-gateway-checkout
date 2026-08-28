# Payment Gateway Checkout

[![Build](https://github.com/ElieSalameh/app-payment-gateway-checkout/actions/workflows/build.yml/badge.svg)](https://github.com/ElieSalameh/app-payment-gateway-checkout/actions/workflows/build.yml)

## Overview

This project is a .NET 8 Web API for a payment gateway assessment. It will allow a merchant to submit a card payment for authorization and retrieve the details of a previously created payment.

The gateway validates payment requests, calls a simulated acquiring bank, stores the payment result in an in-memory or test-double repository, and returns safe payment details.

> The repository is currently being implemented incrementally. The API and roadmap below describe the target behavior from the project requirements.

## Goals

- Process a card payment through the bank simulator.
- Return one of `Authorized`, `Declined`, or `Rejected`.
- Retrieve a payment by its identifier.
- Keep full card numbers and CVV values out of responses, logs, and stored payment records.
- Keep the implementation simple, readable, maintainable, and covered by automated tests.

## Payment requirements

### Processing a payment

A payment request must contain the following fields:

| Field | Validation |
| --- | --- |
| Card number | Required, numeric, and 14–19 characters |
| Expiry month | Required and between 1 and 12 |
| Expiry year | Required; the month/year combination must be in the future |
| Currency | Required, three characters, and one of no more than three supported ISO currency codes |
| Amount | Required integer in minor currency units, such as `1050` for USD 10.50 |
| CVV | Required, numeric, and 3–4 characters |

Invalid input must be rejected without calling the bank simulator.

Valid requests should return:

- A payment ID.
- A status of `Authorized` or `Declined`.
- The last four card digits only.
- Expiry month and year.
- Currency.
- Amount in minor currency units.

### Retrieving a payment

The API contract is:

- `POST /payments` — validate and process a payment.
- `GET /payments/{id}` — retrieve a payment by ID.

The retrieval response includes the payment ID, status, masked card details, expiry month and year, currency, and amount.

## API contract

### Supported currencies

`GBP`, `USD`, and `EUR`. Exactly three, as the brief requires. Any other currency code is rejected.

### Status codes

| Status | When | Body |
| --- | --- | --- |
| `201 Created` | The payment reached the acquiring bank and was `Authorized` or `Declined` | `PaymentResponse` |
| `200 OK` | An existing payment was retrieved | `PaymentResponse` |
| `400 Bad Request` | The request failed gateway validation and was **rejected** | `ValidationProblemDetails` |
| `404 Not Found` | No payment exists for the given ID | `ProblemDetails` |
| `502 Bad Gateway` | The bank simulator was unavailable | `ProblemDetails` |
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

A rejected request produces **no** stored payment, **no** payment id, and **no** call to the bank simulator.

### Current status

This commit publishes the contract and its OpenAPI document only. Both endpoints return `501 Not Implemented` until the domain, application, and infrastructure layers land in the following steps. The routes, request and response shapes, and documented status codes above are final.

## Bank simulator

The acquiring bank simulator accepts a `POST` request at:

`http://localhost:8080/payments`

It expects a request similar to:

```json
{
  "card_number": "2222405343248877",
  "expiry_date": "04/2025",
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

The normal response is similar to:

```json
{
  "authorized": true,
  "authorization_code": "0bb07405-6d44-4b50-a14f-7ae0beff13ad"
}
```

Simulator behavior:

- Missing fields return `400 Bad Request`.
- Card numbers ending in `1`, `3`, `5`, `7`, or `9` return an authorized response. The gateway maps this to `Authorized`.
- Card numbers ending in `2`, `4`, `6`, or `8` return an unauthorized response. The gateway maps this to `Declined`.
- Card numbers ending in `0` return `503 Service Unavailable`. The gateway must report a dependency failure and must not falsely report an authorization or decline.

The simulator URL must be configurable. Do not call a live acquiring bank.

## Architecture

The implementation should use the smallest architecture that keeps responsibilities and testing clear:

```text
src/
  PaymentGateway.Api/             # HTTP endpoints, models, pipeline, and composition root
  PaymentGateway.Application/     # payment use cases, validation, ports, and results
  PaymentGateway.Domain/          # payment state, value objects, and business rules
  PaymentGateway.Infrastructure/  # bank HTTP client and in-memory repository

tests/
  PaymentGateway.Domain.Tests/
  PaymentGateway.Application.Tests/
  PaymentGateway.Api.IntegrationTests/
```

The current single `PaymentGateway.Api` project is acceptable while the project is small. New projects should be introduced only when they create a useful dependency or testing boundary.

Dependencies should point inward:

- The API calls application use cases.
- The application depends on domain rules and interfaces.
- Infrastructure implements the application interfaces.
- The domain does not depend on ASP.NET Core, HTTP, persistence, or provider-specific code.

## Testing strategy

Use xUnit as the default test framework unless the repository adopts another standard.

- **Unit tests:** payment validation, domain rules, card masking, and application result mapping.
- **Integration tests:** ASP.NET Core routing, serialization, dependency injection, the bank client boundary, and payment retrieval.
- **End-to-end tests:** a small number of critical journeys using the local API and simulator, if needed.

Important scenarios include:

- Each invalid payment field produces rejected behavior.
- Rejected input does not call the simulator.
- Authorized, declined, and simulator-failure responses are mapped correctly.
- Payments can be stored and retrieved by ID.
- Unknown payment IDs return the documented response.
- Full card numbers and CVV values are not returned or logged.

## Running locally

### Prerequisites

- .NET 8 SDK, to build and run the projects — every project targets `net8.0`.
- .NET SDK **9.0.200 or newer**, additionally, because the solution file is `PaymentGatewayCheckout.slnx`. The `.slnx` format is only understood by that SDK version onwards, so an older CLI fails at `dotnet restore` before anything is compiled. Installing both side by side is supported and is what CI does. To build without it, pass the project files directly instead of the solution.
- Docker, if running the bank simulator locally.

### Start the bank simulator

Run the simulator from its directory with:

```powershell
docker-compose up
```

The simulator should be available at `http://localhost:8080`.

### Run the API

```powershell
dotnet restore .\PaymentGatewayCheckout.slnx
dotnet run --project .\src\PaymentGateway.Api\PaymentGateway.Api.csproj
```

Swagger/OpenAPI is available in the Development environment only, at `/swagger`.

### Run tests

From the repository root:

```powershell
dotnet test .\PaymentGatewayCheckout.slnx
```

The build workflow runs the same command on Linux for every pull request, so the badge above reflects the suite independently of any one machine.

## Security and data handling

- Use test card values only; never use real card data.
- Never return, log, persist, or include the full card number or CVV in an exception.
- Store only the last four card digits and the payment details required for retrieval.
- Do not commit credentials, tokens, or secrets.
- Treat a simulator timeout or `503` as a dependency failure, not as a decline.

## Scope boundaries

The following are not required for this assessment:

- A real database or durable distributed storage.
- A live card-network or acquiring-bank integration.
- Capture, refund, recurring billing, or other payment operations.
- Webhooks, reconciliation jobs, message brokers, or an outbox.
- A complete authentication or merchant identity platform.
- Speculative abstractions and unrelated infrastructure.

## Implementation roadmap

- [x] Define the public request, response, and error contracts.
- [ ] Implement payment validation and rejected behavior.
- [ ] Implement the configurable bank simulator client.
- [ ] Map authorized, declined, and dependency-failure outcomes.
- [ ] Add the in-memory payment repository and retrieval endpoint.
- [ ] Add unit and integration tests.
- [ ] Document supported currencies, assumptions, and HTTP status mappings.

## Related documentation

- [Project scope](.github/project-context/project-scope.md)
- [Architecture instructions](.github/instructions/architecture.instructions.md)
- [Testing instructions](.github/instructions/testing.instructions.md)
- [API and security instructions](.github/instructions/api-security.instructions.md)
