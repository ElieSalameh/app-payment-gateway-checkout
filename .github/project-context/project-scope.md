# Payment gateway assessment: project scope

## Source of truth

This document summarizes `.github/README.md` from **Assessment Overview** onward. Use it to decide what to build and what not to build. The goal is a focused, maintainable .NET 8 Web API—not a production payment platform.

## Project goal

Build a payment gateway API that allows a merchant to:

1. Submit a card payment for authorization.
2. Receive a result of `Authorized`, `Declined`, or `Rejected`.
3. Retrieve the details of a previously created payment by its identifier.

The gateway validates payment input, calls a simulated acquiring bank, stores the payment result using an in-memory or test-double repository, and returns safe payment details.

## Required behavior

### Create a payment

A payment request must contain:

| Field | Required validation |
| --- | --- |
| Card number | 14–19 characters and numeric only |
| Expiry month | Required and between 1 and 12 |
| Expiry year | Required; the month/year combination must be in the future |
| Currency | Required, three characters, and a supported ISO currency code; support no more than three configured currency codes |
| Amount | Required integer in minor currency units, such as `1050` for USD 10.50 |
| CVV | 3–4 characters and numeric only |

Invalid input must produce `Rejected` behavior and must not call the acquiring-bank simulator.

For a valid request, call the bank simulator and create a payment record. A successful gateway response must include:

- Payment ID.
- `Authorized` or `Declined` status.
- Last four card digits only.
- Expiry month and year.
- Currency.
- Amount in minor units.

The full card number and CVV must never be returned or logged. CVV must not be stored after the authorization attempt.

### Retrieve a payment

Provide retrieval by payment ID for reconciliation and reporting. The response must contain the payment's masked card details, status, expiry month and year, currency, and amount. Choose and document the HTTP response for an unknown ID; `404 Not Found` is the natural default.

A recommended gateway contract is:

- `POST /payments` — validate and process a payment.
- `GET /payments/{id}` — retrieve a payment.

If a different route or response envelope is chosen, document it in the API's OpenAPI output and project documentation.

## Bank simulator contract

The acquiring bank simulator is available at `http://localhost:8080/payments` and accepts `POST` requests with this shape:

```json
{
  "card_number": "2222405343248877",
  "expiry_date": "04/2025",
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

It responds with this shape for normal processing:

```json
{
  "authorized": true,
  "authorization_code": "0bb07405-6d44-4b50-a14f-7ae0beff13ad"
}
```

Simulator rules:

- Missing fields return `400 Bad Request`.
- A card number ending in `1`, `3`, `5`, `7`, or `9` returns `200` with `authorized: true` and a random authorization code. Map this to gateway status `Authorized`.
- A card number ending in `2`, `4`, `6`, or `8` returns `200` with `authorized: false`. Map this to gateway status `Declined`.
- A card number ending in `0` returns `503 Service Unavailable`. Handle this as a bank/dependency failure without falsely reporting an authorization or decline.

Keep the simulator URL configurable. Do not call a live acquiring bank.

## Architecture for this assessment

Use the smallest architecture that makes responsibilities and testing clear:

```text
src/
  PaymentGateway.Api/             # HTTP models, controllers, HTTP pipeline, composition root
  PaymentGateway.Application/     # payment use cases, validation, ports, result mapping
  PaymentGateway.Domain/          # payment state, value objects, business invariants
  PaymentGateway.Infrastructure/  # bank HTTP client and in-memory repository

tests/
  PaymentGateway.Domain.Tests/
  PaymentGateway.Application.Tests/
  PaymentGateway.Api.IntegrationTests/
```

The current repository contains only `PaymentGateway.Api`. It is acceptable to begin there and extract projects only when the boundary provides a real benefit. Do not add a database, message broker, outbox, webhook system, authentication platform, or deployment infrastructure unless a new requirement calls for it.

The bank client and payment repository should be behind small interfaces so the application can be tested without HTTP or shared mutable state. Keep controllers thin and keep payment rules out of HTTP and provider-specific code.

## Testing priorities

At minimum, automated tests should cover:

- Every payment validation rule and the `Rejected` path.
- The guarantee that rejected input does not call the simulator.
- Authorized, declined, and simulator-failure mappings.
- Payment persistence and retrieval by ID.
- Unknown payment ID behavior.
- Last-four masking and the absence of full card data/CVV in responses or logs.
- Boundary values for card length, expiry, amount, currency, and CVV.

Use fast unit tests for domain and application behavior, an integration test for the HTTP pipeline and bank adapter contract, and only a small number of end-to-end tests if they add value. Use the provided simulator or a deterministic fake; never use real payment credentials.

## Expected deliverables

- A compiling .NET 8 API.
- Automated tests covering the important behavior and failure paths.
- OpenAPI/API documentation showing request and response contracts.
- A short design document describing key decisions, assumptions, supported currencies, storage choice, error mapping, and how to run the API and tests.
- Simple, readable, maintainable code focused on the requirements above.

## Explicit non-goals

- Real card-network or acquiring-bank integration.
- A production database or durable distributed storage.
- Returning or persisting full card numbers or CVV values.
- Implementing unrelated payment features such as capture, refund, recurring billing, webhooks, or reconciliation jobs.
- Over-engineering the solution with speculative abstractions or infrastructure.

## Work checklist

- [ ] Define the public payment request, response, and error contracts.
- [ ] Implement validation and `Rejected` behavior before the bank call.
- [ ] Implement the configurable simulator client.
- [ ] Implement `Authorized`, `Declined`, and dependency-failure mapping.
- [ ] Store and retrieve payment records using a test-double/in-memory repository.
- [ ] Return only masked card information.
- [ ] Add focused unit and integration tests.
- [ ] Enable and verify OpenAPI documentation.
- [ ] Document supported currencies, assumptions, setup, and test execution.
