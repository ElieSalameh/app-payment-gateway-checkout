namespace PaymentGateway.Domain.Tests.Payments;

public sealed class PaymentTests
{
    private const string _CardNumber = "2222405343248877";

    [Fact]
    public void Authorized_WhenCreated_HasAuthorizedStatusAndKeepsTheSuppliedDetails()
    {
        var id = PaymentId.New();
        var card = ValidCard();
        var amount = Money.FromMinorUnits(1050, Currency.Gbp);

        var payment = Payment.Authorized(id, card, amount);

        payment.Id.Should().Be(id);
        payment.Status.Should().Be(PaymentStatus.Authorized);
        payment.Card.Should().Be(card);
        payment.Amount.Should().Be(amount);
    }

    [Fact]
    public void Declined_WhenCreated_HasDeclinedStatus()
    {
        var payment = Payment.Declined(PaymentId.New(), ValidCard(), Money.FromMinorUnits(100, Currency.Usd));

        payment.Status.Should().Be(PaymentStatus.Declined);
    }

    [Fact]
    public void Payment_WhenCreated_NeverExposesTheFullCardNumber()
    {
        var payment = Payment.Authorized(PaymentId.New(), ValidCard(), Money.FromMinorUnits(100, Currency.Gbp));

        payment.Card.LastFourDigits.Should().Be("8877");
        payment.ToString().Should().NotContain(_CardNumber);
        payment.Card.ToString().Should().NotContain(_CardNumber);
    }

    [Fact]
    public void PaymentStatus_WhenEnumerated_AllowsOnlyAuthorizedAndDeclined()
    {
        Enum.GetNames<PaymentStatus>().Should().BeEquivalentTo(nameof(PaymentStatus.Authorized), nameof(PaymentStatus.Declined));
    }

    private static CardDetails ValidCard() =>
        CardDetails.FromCardNumber(_CardNumber, expiryMonth: 4, expiryYear: 2030);
}
