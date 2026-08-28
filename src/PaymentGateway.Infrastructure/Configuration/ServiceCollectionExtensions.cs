using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PaymentGateway.Infrastructure.Bank;
using PaymentGateway.Infrastructure.Persistence;

namespace PaymentGateway.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    private const int _CircuitBreakerSamplingMultiplier = 2;

    public static IServiceCollection AddPaymentGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();

        services
            .AddOptions<BankSimulatorOptions>()
            .Bind(configuration.GetSection(BankSimulatorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient<IAcquiringBankClient, BankSimulatorClient>(ConfigureBankSimulatorClient)
            .AddStandardResilienceHandler()
            .Configure(ConfigureBankSimulatorResilience);

        return services;
    }

    private static void ConfigureBankSimulatorClient(IServiceProvider serviceProvider, HttpClient httpClient)
    {
        httpClient.BaseAddress = ResolveBankSimulatorOptions(serviceProvider).BaseAddress;
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    private static void ConfigureBankSimulatorResilience(
        HttpStandardResilienceOptions options,
        IServiceProvider serviceProvider)
    {
        var timeout = TimeSpan.FromSeconds(ResolveBankSimulatorOptions(serviceProvider).TimeoutInSeconds);

        options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
        options.AttemptTimeout.Timeout = timeout;
        options.TotalRequestTimeout.Timeout = timeout;
        options.CircuitBreaker.SamplingDuration = timeout * _CircuitBreakerSamplingMultiplier;
    }

    private static BankSimulatorOptions ResolveBankSimulatorOptions(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<BankSimulatorOptions>>().Value;
}
