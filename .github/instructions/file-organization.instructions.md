---
applyTo: "**/*.cs,**/*.csproj"
---
# File organization and comment-free code

`CLAUDE.md` sections 4 and 5 are canonical. Keep the implementation easy to review and organized around the two required capabilities: processing a payment and retrieving a payment.

## No comments in production code

**This project contains no code comments.** Not `//`, not `/* */`, not XML documentation. If a comment feels necessary, the code is not clear enough — fix the code. This applies to test code too.

How that is earned:

- Methods are verb phrases stating what they do: `ProcessPayment`, `MaskCardNumber`, `IsSupportedCurrency`.
- Booleans read as predicates: `IsAuthorized`, `HasExpired`, `RequiresRetry`.
- No abbreviations beyond the universally understood ones (`Id`, `Api`, `Http`, `Cvv`). Write `cardNumber`, not `cn`.
- No magic values. `const int MinimumCardNumberLength = 14;` — the constant name **is** the comment.
- Extract any conditional you would have explained into a named method or a named local: `var isDeclinedByBank = ...`.
- Methods stay short enough to hold in your head. A method needing section comments to be navigable needs splitting.
- Parameters are named for their role, not their type.
- Named arguments where a bare literal would be opaque: `MaskCardNumber(cardNumber, visibleDigits: 4)`.
- Enums over booleans in signatures. `PaymentStatus.Declined` beats `success: false`.
- Do not use a comment to explain confusing code. Improve the code or rename the symbol.

Rationale, design decisions, and assumptions belong in `README.md` and `CLAUDE.md`. Generated code and tool-required markers are exempt.

## File responsibilities

- One primary public type per file, named after that type: `Payment.cs`, `Money.cs`, `ProcessPaymentHandler.cs`.
- Small private records, enums, and tightly coupled implementation details may share a file when separating them would hurt readability.
- Separate request models, handlers, validators, mappings, the repository, and the bank adapter — they have different reasons to change.
- No business logic in `Program.cs`. Composition and pipeline configuration only.
- Name extension files after the feature they configure: `ServiceCollectionExtensions.cs`. Do not create a catch-all extensions file.
- Avoid `Helpers`, `Utils`, and `Common` as destinations. Use a domain or feature name that explains ownership.
- `partial` only for generated code, source-generator integration (including `[LoggerMessage]` classes), or a genuinely large cohesive type.
- Delete unused files, types, and project references rather than leaving dead alternatives in the repository.

## Directory layout

```text
src/PaymentGateway.Api/
  Controllers/
    PaymentsController.cs
  Contracts/
    Requests/
      ProcessPaymentRequest.cs
    Responses/
      PaymentResponse.cs
      PaymentStatus.cs
  Middleware/
    GlobalExceptionHandler.cs
  Configuration/
    ServiceCollectionExtensions.cs
  GlobalUsings.cs
  Program.cs

src/PaymentGateway.Application/
  Payments/
    ProcessPayment/
    GetPayment/
  Abstractions/
  Exceptions/

src/PaymentGateway.Domain/
  Payments/
    Payment.cs
    PaymentId.cs
    PaymentStatus.cs
    CardDetails.cs
  Shared/
    Money.cs
    Currency.cs

src/PaymentGateway.Infrastructure/
  Bank/
    BankSimulatorClient.cs
  Persistence/
    InMemoryPaymentRepository.cs
  Configuration/

tests/PaymentGateway.Api.IntegrationTests/
  Controllers/
    PaymentsContractTests.cs
  GlobalUsings.cs
```

`Api` is organised by **transport role**; `Application`, `Domain`, and `Infrastructure` are organised by **business capability**. See "Feature organization" below for why, and `CLAUDE.md` section 4 for the full tree.

- Tests live in a top-level `tests/` directory mirroring the production capability names.
- Deployment, scripts, and documentation stay outside `src/`.
- Create a project or folder when it makes a boundary or a test easier, not to have more of them.
- A project must not reference a package merely because a sibling project uses it.

## Namespaces and naming

- Align namespaces with project and folder structure exactly: `PaymentGateway.Api.Contracts.Requests`, `PaymentGateway.Api.Controllers`, `PaymentGateway.Application.Payments.ProcessPayment`. Moving a file between folders means renaming its namespace in the same change.
- A test project mirrors the folder structure of the project under test, so `Controllers/PaymentsController.cs` is covered by `Controllers/PaymentsContractTests.cs`.
- File-scoped namespaces for all new files.
- Name interfaces for capabilities (`IAcquiringBankClient`, `IPaymentRepository`), not implementations (`IPaymentProviderService`).
- Name commands and queries after intent (`ProcessPaymentCommand`, `GetPaymentQuery`); name handlers after what they handle.
- `Request`/`Response` for transport models; `Command`, `Query`, `Result` for application models.
- Avoid `Data`, `Info`, `Manager`, and `Service` unless the word communicates a specific role.

## Class size and dependencies

- A constructor's dependency list should reveal the class's responsibility. Many unrelated dependencies is a signal to split the type.
- Controllers hold transport concerns only. Validation, orchestration, and business rules move inward.
- Mapping at boundaries is explicit. Serialization attributes must not leak into domain types.
- Composition over inheritance. Inherit only where substitutability and shared invariants are clear.
