using Microsoft.Extensions.DependencyInjection;
using PaymentGateway.Application.Payments.ProcessPayment;

namespace PaymentGateway.Application.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentGatewayApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IValidator<ProcessPaymentCommand>, ProcessPaymentValidator>();

        return services;
    }
}
