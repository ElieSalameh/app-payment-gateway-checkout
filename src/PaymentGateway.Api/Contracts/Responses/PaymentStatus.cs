using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Contracts.Responses;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Authorized,
    Declined
}
