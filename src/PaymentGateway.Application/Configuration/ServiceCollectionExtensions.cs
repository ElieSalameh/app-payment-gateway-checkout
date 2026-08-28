using Microsoft.Extensions.DependencyInjection;
using PaymentGateway.Application.Payments.GetPayment;
using PaymentGateway.Application.Payments.ProcessPayment;

namespace PaymentGateway.Application.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentGatewayApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IValidator<ProcessPaymentCommand>, ProcessPaymentValidator>();
        services.AddScoped<ProcessPaymentHandler>();
        services.AddScoped<GetPaymentHandler>();

        return services;
    }
}
