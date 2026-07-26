using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace BraSeller.Connectors.Abstractions;

public interface IMarketplaceConnector
{
    ConnectorDescriptor Descriptor { get; }
    Task<ConnectorAuthentication> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default);
    Task<ConnectorAuthentication> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<ConnectorPage<StandardOrder>> GetOrdersAsync(ConnectorContext context, OrderFilter filter, CancellationToken cancellationToken = default);
    Task<StandardOrder> GetOrderDetailAsync(ConnectorContext context, string orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<StandardPayment>> GetPaymentsAsync(ConnectorContext context, string orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<StandardFee>> GetFeesAsync(ConnectorContext context, string orderId, CancellationToken cancellationToken = default);
    Task<ConnectorPage<StandardInvoice>> GetInvoicesAsync(ConnectorContext context, InvoiceFilter filter, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ConnectorPage<StandardInvoice>([], null, false));
    IAsyncEnumerable<StandardOrder> SyncAllAsync(ConnectorContext context, DateTimeOffset since, CancellationToken cancellationToken = default);
    Task<ConnectorStatus> GetStatusAsync(ConnectorContext context, CancellationToken cancellationToken = default);
}

public interface IConnectorModule
{
    void Register(IServiceCollection services, IConfiguration configuration);
}

public sealed record ConnectorDescriptor(
    string Name,
    string DisplayName,
    string Version,
    bool SupportsInvoices = false,
    IReadOnlyCollection<ConnectorCredentialField>? CredentialFields = null,
    bool UsesOAuth = false);

public sealed record ConnectorCredentialField(
    string Name,
    string Label,
    bool Secret = false,
    bool Required = true);

public interface IOAuthMarketplaceConnector
{
    Task<OAuthAuthorization> BeginAuthenticationAsync(
        OAuthStartRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OAuthStartRequest(Guid TenantId, string State, string CallbackUrl, string CodeChallenge);
public sealed record OAuthAuthorization(Uri AuthorizationUri);

public sealed record AuthenticationRequest(
    Guid TenantId,
    IReadOnlyDictionary<string, string> Credentials,
    string? CallbackUrl = null);

public sealed record RefreshTokenRequest(Guid TenantId, string RefreshToken);

public sealed record ConnectorAuthentication(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string ExternalAccountId,
    string? AccountDisplayName = null);

public sealed record ConnectorContext(
    Guid TenantId,
    Guid ConnectionId,
    string AccessToken,
    string ExternalAccountId);

public sealed record OrderFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    StandardOrderStatus? Status = null,
    string? Cursor = null,
    int PageSize = 50);

public sealed record InvoiceFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Cursor = null,
    int PageSize = 50);

public sealed record ConnectorPage<T>(IReadOnlyCollection<T> Items, string? NextCursor, bool HasMore);

public sealed record StandardOrder(
    [property: JsonPropertyName("order_id")] string OrderId,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("date")] DateTimeOffset Date,
    [property: JsonPropertyName("gross_value")] decimal GrossValue,
    [property: JsonPropertyName("platform_fee")] decimal PlatformFee,
    [property: JsonPropertyName("net_value")] decimal NetValue,
    [property: JsonPropertyName("payment_method")] string PaymentMethod,
    [property: JsonPropertyName("payment_date")] DateTimeOffset? PaymentDate,
    [property: JsonPropertyName("release_date")] DateTimeOffset? ReleaseDate,
    [property: JsonPropertyName("status")] StandardOrderStatus Status,
    [property: JsonPropertyName("buyer_name")] string BuyerName,
    [property: JsonPropertyName("items")] IReadOnlyCollection<StandardOrderItem> Items,
    [property: JsonPropertyName("invoice_number")] string? InvoiceNumber,
    [property: JsonPropertyName("fulfillment_status")] StandardFulfillmentStatus FulfillmentStatus = StandardFulfillmentStatus.Unknown,
    [property: JsonPropertyName("delivered_at")] DateTimeOffset? DeliveredAt = null,
    [property: JsonPropertyName("currency")] string Currency = "BRL");

public sealed record StandardOrderItem(
    string? Sku,
    string Title,
    int Quantity,
    decimal UnitValue);

public enum StandardOrderStatus
{
    Paid,
    Pending,
    Cancelled
}

public enum StandardFulfillmentStatus
{
    Unknown,
    Pending,
    Shipped,
    Delivered,
    Returned
}

public sealed record StandardPayment(
    string PaymentId,
    string OrderId,
    decimal GrossValue,
    decimal NetValue,
    string Method,
    StandardPaymentStatus Status,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ReleaseAt,
    decimal PaymentFee = 0,
    string Currency = "BRL");

public enum StandardPaymentStatus
{
    Paid,
    Pending,
    Cancelled,
    Refunded
}

public sealed record StandardFee(
    string Type,
    string Description,
    decimal Amount,
    string Currency = "BRL",
    StandardFeeCategory Category = StandardFeeCategory.Other,
    string? ExternalId = null);

public enum StandardFeeCategory
{
    MarketplaceCommission,
    PaymentProcessing,
    SellerShipping,
    Refund,
    Chargeback,
    TaxWithholding,
    Other
}

public sealed record StandardInvoice(
    string InvoiceId,
    string? OrderId,
    string Number,
    DateTimeOffset IssuedAt,
    decimal TotalValue,
    string? AccessKey,
    Uri? DocumentUrl);

public sealed record ConnectorStatus(
    ConnectorConnectionState State,
    string? Message = null,
    DateTimeOffset? CheckedAt = null);

public enum ConnectorConnectionState
{
    Active,
    Expired,
    Revoked,
    Error
}

public class ConnectorException(
    string connectorName,
    string code,
    string message,
    bool isTransient = false,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string ConnectorName { get; } = connectorName;
    public string Code { get; } = code;
    public bool IsTransient { get; } = isTransient;
}

