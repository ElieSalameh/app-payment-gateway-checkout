---
applyTo: "**/*.cs,**/*.json,**/*.http"
---
# API and payment security for this assessment

Use `.github/project-context/project-scope.md` as the functional source of truth. Apply strong handling of payment data without adding an authentication, webhook, or production compliance platform that the assessment does not require.

## Sensitive payment data

- Never log, persist in the payment record, return, or include in exceptions full card numbers (PAN), CVV/CVC, PINs, authentication secrets, access tokens, or provider credentials.
- This assessment's simulator accepts card test data, so pass the data only for the authorization request, retain only the last four digits and required expiry details, and discard CVV after the call. Never use real card data.
- In a production design, prefer provider-hosted fields or tokenization so raw card data does not enter the API. Keep the assessment implementation consistent with the simulator contract without pretending it is production PCI compliance.
- Mask identifiers when they must be displayed, for example only the last four digits of a card token. Treat payment and customer identifiers as sensitive even when they are not card data.
- Use an approved secrets store or environment-based secret injection in deployed environments. Keep only non-secret configuration defaults in source control.
- Rotate credentials and use separate credentials for development, testing, staging, and production.

## Request safety

- Authentication and merchant identity are not part of the stated assessment requirements. Do not invent a full identity system; keep the API boundary structured so authorization can be added later.
- Validate request size, content type, required fields, ranges, card digits, expiry, supported currency codes, amount, CVV, and payment identifiers at the API boundary.
- Do not trust client-provided calculated values or ownership fields if those fields are introduced later; resolve authoritative values from server-side state.
- Use HTTPS in every non-local environment and configure secure cookies, HSTS, and appropriate security headers when cookies or browser clients are involved.
- Rate limiting and abuse controls are production follow-ups, not reasons to add unrelated infrastructure to this assessment.

## Payment commands

- Idempotency is a useful production consideration, but it is not a stated assessment requirement. Do not add an idempotency protocol unless it is implemented completely and documented.
- Store only the safe payment fields required for retrieval in the approved in-memory/test-double repository. Treat a timeout or `503` after submission as a dependency failure, not as proof that the payment was declined.
- Use decimal-safe money types or integer minor units with an explicit currency. Never use binary floating point for monetary calculations.
- Validate allowed amount, currency, and payment status on the server. Do not accept calculated totals from an untrusted client without recomputing or verifying them.
- Configure a bounded timeout for the simulator and do not blindly retry its non-idempotent payment request.

## Out-of-scope integrations

- Webhooks, callbacks, capture, refunds, recurring billing, and reconciliation jobs are outside this assessment. Do not create endpoints or security flows for them without a new requirement.

## Errors and observability

- Return stable, client-safe error codes and `ProblemDetails`; do not return provider credentials, raw provider payloads, stack traces, SQL, or internal hostnames.
- Use correlation IDs and structured logs, but define an allowlist of fields for payment logs. Redact sensitive headers, request bodies, authorization values, and query strings.
- Keep any audit or diagnostic data limited to safe payment identifiers, status, result, and correlation data. Do not expose detailed dependency diagnostics publicly.

## Verification checklist

Before merging an API or payment change, verify input validation, simulator boundary handling, secret handling, log redaction, card masking, timeout behavior, safe error responses, and the relevant payment-data risk. Add a regression test for each security defect or business-risk scenario fixed.
