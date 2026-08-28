namespace PaymentGateway.Application.Tests.Payments.ProcessPayment;

public sealed class ProcessPaymentCommandTests
{
    [Fact]
    public void ToString_Always_OmitsTheCardNumberAndTheCvv()
    {
        const string _cardNumber = "2222405343248877";
        const string _cvv = "123";
        var command = new ProcessPaymentCommand
        {
            CardNumber = _cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            Cvv = _cvv
        };

        var description = command.ToString();

        description.Should().NotContain(_cardNumber);
        description.Should().NotContain(_cvv);
        description.Should().Contain("GBP");
    }
}
