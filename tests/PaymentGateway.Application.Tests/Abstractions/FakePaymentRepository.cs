namespace PaymentGateway.Application.Tests.Abstractions;

internal sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly Dictionary<PaymentId, Payment> _payments = [];

    public IReadOnlyCollection<Payment> StoredPayments => _payments.Values;

    public Task Add(Payment payment, CancellationToken cancellationToken)
    {
        _payments.Add(payment.Id, payment);

        return Task.CompletedTask;
    }

    public Task<Payment?> GetById(PaymentId paymentId, CancellationToken cancellationToken) =>
        Task.FromResult(_payments.GetValueOrDefault(paymentId));
}
