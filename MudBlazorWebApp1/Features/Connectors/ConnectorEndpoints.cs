using BraSeller.Connectors.Abstractions;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MudBlazorWebApp1.Domain;

namespace MudBlazorWebApp1.Features.Connectors;

public static class ConnectorEndpoints
{
    public static IEndpointRouteBuilder MapConnectorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/connectors")
            .WithTags("Connectors")
            .RequireAuthorization(Policies.TenantMember)
            .AddEndpointFilter<ConnectorExceptionFilter>();

        group.MapGet("/", (ConnectorGateway gateway) => Results.Ok(gateway.AvailableConnectors))
            .WithName("GetInstalledConnectors").WithSummary("Lista os modulos de marketplace instalados");
        group.MapGet("/connections", ListConnectionsAsync)
            .WithName("GetMarketplaceConnections").WithSummary("Lista as contas conectadas do tenant");
        group.MapPost("/{connectorName}/authenticate", AuthenticateAsync)
            .RequireAuthorization(Policies.CanWrite).WithName("AuthenticateMarketplaceConnector");
        group.MapGet("/{connectorName}/oauth/start", StartOAuthAsync)
            .RequireAuthorization(Policies.CanWrite).WithName("StartConnectorOAuth");
        group.MapPost("/connections/{connectionId:guid}/refresh", RefreshAsync)
            .RequireAuthorization(Policies.CanWrite).WithName("RefreshMarketplaceToken");
        group.MapGet("/connections/{connectionId:guid}/status", StatusAsync)
            .WithName("GetMarketplaceStatus");
        group.MapGet("/connections/{connectionId:guid}/orders", GetOrdersAsync)
            .WithName("GetMarketplaceOrders");
        group.MapGet("/connections/{connectionId:guid}/orders/{orderId}", GetOrderAsync)
            .WithName("GetMarketplaceOrderDetail");
        group.MapGet("/connections/{connectionId:guid}/orders/{orderId}/payments", GetPaymentsAsync)
            .WithName("GetMarketplacePayments");
        group.MapGet("/connections/{connectionId:guid}/orders/{orderId}/fees", GetFeesAsync)
            .WithName("GetMarketplaceFees");
        group.MapGet("/connections/{connectionId:guid}/invoices", GetInvoicesAsync)
            .WithName("GetMarketplaceInvoices");
        group.MapPost("/connections/{connectionId:guid}/sync", SyncAsync)
            .RequireAuthorization(Policies.CanWrite).WithName("SyncMarketplaceConnection");
        endpoints.MapGet("/api/connectors/oauth/callback", CompleteOAuthAsync)
            .AllowAnonymous().WithTags("Connectors").WithName("CompleteConnectorOAuth")
            .ExcludeFromDescription();
        return endpoints;
    }

    private static async Task<IResult> AuthenticateAsync(
        string connectorName,
        AuthenticateConnectorRequest request,
        ConnectorGateway gateway,
        CancellationToken cancellationToken)
    {
        if (request.Credentials is null || request.Credentials.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Connector credentials are required."] });
        var connection = await gateway.AuthenticateAsync(connectorName, request.Credentials, request.CallbackUrl, cancellationToken);
        return Results.Ok(Map(connection));
    }

    private static async Task<IResult> StartOAuthAsync(
        string connectorName,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        ConnectorOAuthService oauth,
        CancellationToken cancellationToken)
    {
        var callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/connectors/oauth/callback";
        var authorizationUri = await oauth.StartAsync(connectorName, principal, callbackUrl, cancellationToken);
        return Results.Ok(new { authorizationUrl = authorizationUri.ToString() });
    }

    private static async Task<IResult> CompleteOAuthAsync(
        string? code,
        string? state,
        string? error,
        HttpContext httpContext,
        ConnectorOAuthService oauth,
        ILogger<ConnectorOAuthService> logger,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return Results.Redirect($"/connectors?oauthError={Uri.EscapeDataString(error)}");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return Results.BadRequest(new { message = "OAuth code and state are required." });
        var callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/connectors/oauth/callback";
        try
        {
            await oauth.CompleteAsync(code, state, callbackUrl, cancellationToken);
            return Results.Redirect("/connectors?connected=true");
        }
        catch (ConnectorException exception)
        {
            logger.LogWarning(exception, "Connector OAuth callback failed with code {Code}.", exception.Code);
            return Results.Redirect($"/connectors?oauthError={Uri.EscapeDataString(exception.Code)}&oauthMessage={Uri.EscapeDataString(exception.Message)}");
        }
    }

    private static async Task<IResult> ListConnectionsAsync(ConnectorGateway gateway, CancellationToken cancellationToken) =>
        Results.Ok((await gateway.ListConnectionsAsync(cancellationToken)).Select(Map));

    private static async Task<IResult> RefreshAsync(Guid connectionId, ConnectorGateway gateway, CancellationToken cancellationToken)
    {
        await gateway.RefreshTokenAsync(connectionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<ConnectorStatus> StatusAsync(Guid connectionId, ConnectorGateway gateway, CancellationToken cancellationToken) =>
        await gateway.RefreshStatusAsync(connectionId, cancellationToken);

    private static async Task<ConnectorPage<StandardOrder>> GetOrdersAsync(
        Guid connectionId, [AsParameters] ConnectorOrderQuery query, ConnectorGateway gateway, CancellationToken cancellationToken)
    {
        var execution = await gateway.GetExecutionAsync(connectionId, cancellationToken);
        return await execution.Connector.GetOrdersAsync(execution.Context,
            new OrderFilter(query.From, query.To, query.Status, query.Cursor, Math.Clamp(query.PageSize, 1, 200)), cancellationToken);
    }

    private static async Task<StandardOrder> GetOrderAsync(
        Guid connectionId, string orderId, ConnectorGateway gateway, CancellationToken cancellationToken)
    {
        var execution = await gateway.GetExecutionAsync(connectionId, cancellationToken);
        return await execution.Connector.GetOrderDetailAsync(execution.Context, orderId, cancellationToken);
    }

    private static async Task<IReadOnlyCollection<StandardPayment>> GetPaymentsAsync(
        Guid connectionId, string orderId, ConnectorGateway gateway, CancellationToken cancellationToken)
    {
        var execution = await gateway.GetExecutionAsync(connectionId, cancellationToken);
        return await execution.Connector.GetPaymentsAsync(execution.Context, orderId, cancellationToken);
    }

    private static async Task<IReadOnlyCollection<StandardFee>> GetFeesAsync(
        Guid connectionId, string orderId, ConnectorGateway gateway, CancellationToken cancellationToken)
    {
        var execution = await gateway.GetExecutionAsync(connectionId, cancellationToken);
        return await execution.Connector.GetFeesAsync(execution.Context, orderId, cancellationToken);
    }

    private static async Task<IResult> GetInvoicesAsync(
        Guid connectionId, [AsParameters] ConnectorInvoiceQuery query, ConnectorGateway gateway, CancellationToken cancellationToken)
    {
        var execution = await gateway.GetExecutionAsync(connectionId, cancellationToken);
        if (!execution.Connector.Descriptor.SupportsInvoices)
            return Results.Problem("This connector does not provide invoices.", statusCode: StatusCodes.Status501NotImplemented);
        var result = await execution.Connector.GetInvoicesAsync(execution.Context,
            new InvoiceFilter(query.From, query.To, query.Cursor, Math.Clamp(query.PageSize, 1, 200)), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<SyncResult> SyncAsync(
        Guid connectionId, SyncConnectorRequest request, ConnectorSyncService sync, CancellationToken cancellationToken) =>
        await sync.SyncAllAsync(connectionId, request.Since, cancellationToken);

    private static ConnectionResponse Map(MarketplaceConnection connection) => new(
        connection.Id, connection.ConnectorName, connection.ExternalAccountId,
        connection.AccountDisplayName, connection.Status, connection.StatusMessage,
        connection.TokenExpiresAt, connection.LastSyncAt);
}

public sealed record AuthenticateConnectorRequest(IReadOnlyDictionary<string, string> Credentials, string? CallbackUrl);
public sealed record SyncConnectorRequest(DateTimeOffset Since);
public sealed record ConnectionResponse(
    Guid Id, string ConnectorName, string ExternalAccountId, string? AccountDisplayName,
    string Status, string? StatusMessage, DateTimeOffset TokenExpiresAt, DateTimeOffset? LastSyncAt);

public sealed class ConnectorOrderQuery
{
    [FromQuery] public DateTimeOffset? From { get; init; }
    [FromQuery] public DateTimeOffset? To { get; init; }
    [FromQuery] public StandardOrderStatus? Status { get; init; }
    [FromQuery] public string? Cursor { get; init; }
    [FromQuery] public int PageSize { get; init; } = 50;
}

public sealed class ConnectorInvoiceQuery
{
    [FromQuery] public DateTimeOffset? From { get; init; }
    [FromQuery] public DateTimeOffset? To { get; init; }
    [FromQuery] public string? Cursor { get; init; }
    [FromQuery] public int PageSize { get; init; } = 50;
}

public sealed class ConnectorExceptionFilter(ILogger<ConnectorExceptionFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ConnectorNotFoundException exception)
        {
            return Results.NotFound(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { message = exception.Message });
        }
        catch (ConnectorException exception)
        {
            logger.LogWarning(exception, "Connector {Connector} failed with code {Code}.", exception.ConnectorName, exception.Code);
            return Results.Problem(exception.Message,
                statusCode: exception.IsTransient ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code, ["connector"] = exception.ConnectorName });
        }
    }
}
