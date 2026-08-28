---
applyTo: "**/*.cs,**/*.json,**/*.http"
---
# API and payment security for this assessment

`CLAUDE.md` section 9 is canonical. `.github/project-context/project-scope.md` is the functional source of truth. Apply strong handling of payment data without building an authentication, webhook, or compliance platform the assessment does not require.

## Scope: built versus documented

The brief lists two functional requirements and explicitly discourages over-engineering. Authentication, merchant identity, and rate limiting are not stated requirements.

**Implement.** Card-data hygiene, boundary validation, bounded timeouts, safe error responses, log redaction, HTTPS configuration outside local development. These are what a payments reviewer actually checks, and they are all in scope.

**Document, do not build.** A `## Production considerations` section in `README.md` covering: API key or mTLS merchant authentication with `CryptographicOperations.FixedTimeEquals` comparison rather than `==`; per-merchant payment scoping so one merchant cannot read another's payments; rate limiting via the built-in .NET 8 limiter returning `429` with `Retry-After`; idempotency keys on the process endpoint; secret storage and rotation with separate credentials per environment; and provider-hosted fields or tokenization so raw PAN never reaches the API.

Keep the boundary structured so authorization can be added later — threading a merchant identifier through the command and storing it on the payment costs nothing and makes the follow-up small. Do not add the authentication scheme itself. Half an identity system reads worse in review than a clearly documented absent one.

## Sensitive payment data

- Never log, persist in the payment record, return in a response, or include in an exception message: full card numbers (PAN), CVV/CVC, PINs, authentication secrets, access tokens, or provider credentials.
- The full PAN exists in exactly two places: the inbound request model and the outbound simulator call. Nowhere else, at no point.
- Retain only the last four digits and the expiry details required for retrieval. Discard the CVV after the authorization call — storing it is a hard PCI DSS violation regardless of environment.
- Mask once, in `CardDetails.FromCardNumber` (`PaymentGateway.Domain`). One implementation, one place to audit. Do not add a second masking helper in any other layer.
- Treat payment and merchant identifiers as sensitive even though they are not card data.
- Never use real card data. The simulator's documented test cards only.
- Keep only non-secret configuration defaults in source control. Use `dotnet user-secrets` locally and environment-based injection in deployed environments.

## Request safety

- Validate at the boundary and treat every request as hostile: content type, request size, required fields, card digits and length, expiry month and combined expiry date, supported currency, integer amount, CVV length and digits, and payment identifier format.
- Route parameters typed as `Guid` so malformed ids are rejected before any code runs.
- Set a low `MaxRequestBodySize` — a payment request is tiny.
- Reject unknown JSON properties rather than silently ignoring them.
- Do not trust client-supplied calculated values or ownership fields; resolve authoritative values server-side.
- Use HTTPS in every non-local environment, with HSTS. The simulator is plain HTTP locally — say so in `README.md` rather than pretending otherwise.
- CORS stays closed. A payment API is not called from a browser.
- Remove the `Server` header.

## Payment commands

- Use integer minor units with an explicit currency. Never binary floating point for money.
- Store only the safe fields required for retrieval, in the approved in-memory repository.
- Configure a bounded timeout for the simulator and **do not blindly retry its non-idempotent payment request** — a retry risks double-charging.
- A timeout or `503` after submission is a **dependency failure**, not proof of decline. Never record it as `Declined` and never tell the merchant a payment failed when it may have succeeded.
- Validate allowed amount, currency, and status server-side.

## Out of scope

Webhooks, callbacks, capture, refunds, voids, recurring billing, and reconciliation jobs are outside this assessment. Do not create endpoints or security flows for them without a new requirement.

## Errors and observability

- Return stable, client-safe error codes and `ProblemDetails`. Never return provider credentials, raw provider payloads, stack traces, SQL, or internal hostnames. See `error-handling.instructions.md`.
- Use correlation identifiers and structured logs with an **allowlist** of fields for payment logs. Redact sensitive headers, bodies, authorization values, and query strings. See `logging.instructions.md`.
- Keep audit and diagnostic data limited to safe payment identifiers, status, result, and correlation data. Do not expose dependency diagnostics publicly.
- Swagger in Development only.

## Verification checklist

Before merging an API or payment change, verify: input validation, simulator boundary handling, secret handling, log redaction, card masking, timeout behavior, safe error responses, and the specific payment-data risk the change touches. Add a regression test for every security defect or business-risk scenario fixed.
