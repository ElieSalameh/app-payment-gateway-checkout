---
applyTo: "**/*.cs"
---
# Error handling for the payment gateway

`CLAUDE.md` section 8 is canonical.

## Principles

- **Exceptions are for the exceptional.** Validation failure is an expected outcome the brief names (`Rejected`); it is returned as a result, not thrown.
- Never catch `Exception` except in the global handler.
- Never swallow an exception silently. Handle it, translate it, or let it bubble.
- Do not catch unless the code can add useful context, translate the failure, recover, or enforce a boundary policy. Preserve the original as `InnerException` when rethrowing.
- The merchant sees a useful, stable error shape. Stack traces and internal detail never cross the wire.

## Global handler

Use .NET 8's `IExceptionHandler`, registered with `AddExceptionHandler<GlobalExceptionHandler>()` and `UseExceptionHandler()`. It maps exceptions to RFC 7807 `ProblemDetails`:

| Exception | Status | Merchant sees |
| --- | --- | --- |
| `ValidationException` | `400` | Field-level error list |
| `PaymentNotFoundException` | `404` | "Payment not found" |
| `AcquiringBankUnavailableException` | `502` | "Acquiring bank is unavailable, please retry" |
| `TaskCanceledException` (timeout) | `504` | "Request to acquiring bank timed out" |
| anything else | `500` | "An unexpected error occurred" |

Every `ProblemDetails` carries the `traceId`, so a merchant's report can be traced in the logs without exposing anything internal.

## Validation errors

Return `400` with the field name and reason for each failure, so the merchant can fix the request:

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

The `paymentStatus` extension member keeps the brief's three-outcome vocabulary on the wire without inventing a second error shape. The payment envelope itself never carries `Rejected`, because no payment was created — which is why the wire `PaymentStatus` enum is exactly `Authorized` and `Declined`, matching the brief's response table.

Document explicitly in `README.md` that `Rejected` maps to `400`, produces **no** stored payment, **no** payment id, and **no** call to the simulator.

## Simulator failures

A `503` or a timeout from the simulator means **the outcome is unknown**, not "declined". Never record it as `Declined` — that would tell a merchant their payment failed when it may have succeeded, which is the most damaging bug this codebase can ship. Surface it as `502` or `504`, persist nothing, and log it at `Warning`.

The simulator returns `503` for card numbers ending in zero. Test that path explicitly.

## Status code contract

`201` on a processed payment (authorized or declined — both are successful processing), `200` on retrieval, `400` on rejection, `404` on an unknown id, `502` when the simulator is unavailable, `504` on timeout, `500` otherwise. These are part of the API contract: annotate them with `[ProducesResponseType]`, document them in `README.md`, and cover each in an integration test.
