namespace PaymentGateway.Domain.Shared;

public sealed record Currency
{
    private const int _CurrencyCodeLength = 3;

    public static readonly Currency Gbp = new("GBP");
    public static readonly Currency Usd = new("USD");
    public static readonly Currency Eur = new("EUR");

    private static readonly Currency[] _SupportedCurrencies = [Gbp, Usd, Eur];

    private Currency(string code) => Code = code;

    public string Code { get; }

    public static IReadOnlyList<Currency> Supported => _SupportedCurrencies;

    public static bool IsSupported(string? code) => TryParse(code, out _);

    public static bool TryParse(string? code, out Currency? currency)
    {
        currency = null;

        if (code is null || code.Length != _CurrencyCodeLength)
        {
            return false;
        }

        foreach (var supported in _SupportedCurrencies)
        {
            if (string.Equals(supported.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                currency = supported;
                return true;
            }
        }

        return false;
    }

    public static Currency Parse(string? code) => TryParse(code, out var currency) && currency is not null
        ? currency
        : throw new ArgumentException($"Currency must be one of {string.Join(", ", _SupportedCurrencies.Select(supported => supported.Code))}.", nameof(code));

    public override string ToString() => Code;
}
