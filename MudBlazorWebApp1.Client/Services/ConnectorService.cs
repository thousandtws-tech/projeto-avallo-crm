using MudBlazorWebApp1.Client.Models;

namespace MudBlazorWebApp1.Client.Services;

public sealed class ConnectorService(AuthService authService)
{
    public Task<ApiResult<ConnectorDescriptorModel[]>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
        authService.GetAsync<ConnectorDescriptorModel[]>("api/connectors", cancellationToken);

    public Task<ApiResult<MarketplaceConnectionModel[]>> GetConnectionsAsync(CancellationToken cancellationToken = default) =>
        authService.GetAsync<MarketplaceConnectionModel[]>("api/connectors/connections", cancellationToken);

    public Task<ApiResult<MarketplaceConnectionModel>> AuthenticateAsync(
        string connectorName,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken = default) =>
        authService.PostAsync<object, MarketplaceConnectionModel>(
            $"api/connectors/{Uri.EscapeDataString(connectorName)}/authenticate",
            new { credentials, callbackUrl = (string?)null }, cancellationToken);

    public Task<ApiResult<OAuthStartModel>> StartOAuthAsync(
        string connectorName,
        CancellationToken cancellationToken = default) =>
        authService.GetAsync<OAuthStartModel>(
            $"api/connectors/{Uri.EscapeDataString(connectorName)}/oauth/start", cancellationToken);

    public Task<ApiResult<SyncResultModel>> SyncAsync(
        Guid connectionId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        authService.PostAsync<object, SyncResultModel>(
            $"api/connectors/connections/{connectionId}/sync", new { since }, cancellationToken);
}
