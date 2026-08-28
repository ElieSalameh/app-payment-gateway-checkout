namespace PaymentGateway.Infrastructure.Bank;

public sealed class BankSimulatorOptions
{
    public const string SectionName = "BankSimulator";

    private const int _MinimumTimeoutInSeconds = 1;
    private const int _MaximumTimeoutInSeconds = 60;
    private const int _DefaultTimeoutInSeconds = 10;

    [Required]
    public Uri? BaseAddress { get; set; }

    [Range(_MinimumTimeoutInSeconds, _MaximumTimeoutInSeconds)]
    public int TimeoutInSeconds { get; set; } = _DefaultTimeoutInSeconds;
}
