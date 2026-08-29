namespace PaymentGateway.Infrastructure.Persistence;

public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<PaymentId, Payment> _payments = new();

    public Task Add(Payment payment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_payments.TryAdd(payment.Id, payment))
        {
            throw new InvalidOperationException($"A payment with id {payment.Id} has already been stored.");
        }

        return Task.CompletedTask;
    }

    public Task<Payment?> GetById(PaymentId paymentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_payments.TryGetValue(paymentId, out var payment) ? payment : null);
    }
}
