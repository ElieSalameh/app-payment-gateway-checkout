using PaymentGateway.Infrastructure.Persistence;

namespace PaymentGateway.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentGatewayInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();

        return services;
    }
}
