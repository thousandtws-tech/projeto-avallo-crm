namespace MudBlazorWebApp1.Client.Models;

public sealed record ConnectorCredentialFieldModel(string Name, string Label, bool Secret, bool Required);
public sealed record ConnectorDescriptorModel(
    string Name, string DisplayName, string Version, bool SupportsInvoices,
    ConnectorCredentialFieldModel[]? CredentialFields, bool UsesOAuth);
public sealed record MarketplaceConnectionModel(
    Guid Id, string ConnectorName, string ExternalAccountId, string? AccountDisplayName,
    string Status, string? StatusMessage, DateTimeOffset TokenExpiresAt, DateTimeOffset? LastSyncAt);
public sealed record SyncResultModel(int ProcessedOrders, DateTimeOffset CompletedAt);
public sealed record OAuthStartModel(string AuthorizationUrl);
