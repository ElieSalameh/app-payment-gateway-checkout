---
applyTo: "**/*.cs,**/*.csproj"
---
# File and project organization for this assessment

Use `.github/project-context/project-scope.md` as the functional source of truth. Keep the implementation easy to review and organize code around the two required capabilities: processing a payment and retrieving a payment.

## File responsibilities

- Keep one primary public type per file. Name the file after that type, for example `Payment.cs`, `Money.cs`, and `CreatePaymentHandler.cs`.
- Small private records, enums, and closely coupled implementation details may share a file when separating them would reduce readability.
- Keep a file focused on one responsibility. Separate payment request models, handlers, validators, mappings, the repository, and the bank adapter when they have different reasons to change.
- Do not put business logic in `Program.cs`; use it only for application composition and HTTP pipeline configuration.
- Do not put unrelated extension methods into one catch-all file. Name extension files after the feature they configure, such as `ServiceCollectionExtensions.cs` or `PaymentProviderExtensions.cs`.
- Avoid `Helpers`, `Utils`, and `Common` as default destinations. Use a domain or feature name that explains ownership.
- Use partial classes only for generated code, source-generator integration, or a genuinely large type whose parts have one cohesive responsibility.

## Directory layout

Organize the API project by feature at the edge:

```text
PaymentGateway.Api/
  Payments/
	PaymentController.cs
	ProcessPaymentRequest.cs
	PaymentResponse.cs
  Middleware/
  Configuration/
  Program.cs
```

Organize application and domain projects by business capability rather than technical type alone:

```text
PaymentGateway.Application/
  Payments/
	ProcessPayment/
	GetPayment/
  Abstractions/

PaymentGateway.Domain/
  Payments/
	Payment.cs
	PaymentStatus.cs
	PaymentId.cs
  Shared/

PaymentGateway.Infrastructure/
	Persistence/
	InMemoryPaymentRepository.cs
  Bank/
	BankSimulatorClient.cs
```

- Keep tests in a top-level `tests/` directory and mirror the production capability names.
- Keep deployment, scripts, and documentation outside `src/`.
- The current single `PaymentGateway.Api` project is acceptable. Do not create extra projects or folders until they make a boundary or test easier.
- Keep each project focused. A project should not reference a package just because another project uses it.
- Prefer project references that make dependency direction obvious; avoid circular project references.

## Namespaces and naming

- Align namespaces with the project and folder structure, for example `PaymentGateway.Application.Payments.CreatePayment`.
- Prefer file-scoped namespaces for new C# files unless the surrounding project consistently uses block-scoped namespaces.
- Name interfaces for capabilities (`IBankClient`, `IPaymentRepository`), not implementation details (`IPaymentProviderService`).
- Name commands and queries after intent (`ProcessPaymentCommand`, `GetPaymentQuery`). Name handlers after the message they handle.
- Use `Request` and `Response` suffixes for transport models and `Command`, `Query`, `Result`, or `Dto` for application models when those distinctions matter.
- Avoid ambiguous names such as `Data`, `Info`, `Manager`, and `Service` unless they communicate a specific role.

## Class size and dependencies

- Keep constructors short enough that dependencies reveal the class's responsibility. A constructor with many unrelated dependencies is a design signal to split the type or use a cohesive façade.
- Keep controllers limited to transport concerns. Move validation, orchestration, and business rules into the appropriate inner layer.
- Keep mapping code explicit at boundaries. Do not allow serialization attributes or database annotations to leak into domain types without a deliberate reason.
- Prefer composition over inheritance. Use inheritance only when substitutability and shared invariants are clear.
- Delete unused files, types, and project references rather than leaving dead alternatives in the repository.
