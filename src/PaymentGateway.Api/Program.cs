using PaymentGateway.Api.Configuration;
using PaymentGateway.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPaymentGatewayApi();
builder.Services.AddPaymentGatewayInfrastructure();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program;
