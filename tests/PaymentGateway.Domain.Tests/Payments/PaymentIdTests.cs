namespace PaymentGateway.Domain.Tests.Payments;

public sealed class PaymentIdTests
{
    [Fact]
    public void New_WhenCalledRepeatedly_ProducesDistinctIdentifiers()
    {
        var first = PaymentId.New();
        var second = PaymentId.New();

        first.Should().NotBe(second);
        first.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void From_WhenGivenAnExistingGuid_PreservesIt()
    {
        var value = Guid.NewGuid();

        PaymentId.From(value).Value.Should().Be(value);
    }

    [Fact]
    public void From_WhenGuidIsEmpty_Throws()
    {
        var create = () => PaymentId.From(Guid.Empty);

        create.Should().Throw<ArgumentException>().WithMessage("A payment id cannot be empty.*");
    }

    [Fact]
    public void Equality_WhenTwoIdentifiersShareAGuid_TreatsThemAsEqual()
    {
        var value = Guid.NewGuid();

        PaymentId.From(value).Should().Be(PaymentId.From(value));
    }
}
