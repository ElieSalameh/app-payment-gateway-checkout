namespace PaymentGateway.Application.Abstractions;

public interface IPaymentRepository
{
    Task Add(Payment payment, CancellationToken cancellationToken);

    Task<Payment?> GetById(PaymentId paymentId, CancellationToken cancellationToken);
}
