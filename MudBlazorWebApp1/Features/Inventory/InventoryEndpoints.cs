using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Features.Auth;
using MudBlazorWebApp1.Features.Expenses;
using MudBlazorWebApp1.Infrastructure;

namespace MudBlazorWebApp1.Features.Inventory;

public static class InventoryEndpoints
{
    private const long MaximumXmlSize = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory").WithTags("Inventory")
            .RequireAuthorization(Policies.TenantMember);
        group.MapGet("/overview", GetOverviewAsync);
        group.MapPost("/invoices/import", ImportInvoiceAsync)
            .RequireAuthorization(Policies.CanWrite).DisableAntiforgery();
        group.MapPost("/mappings", CreateMappingAsync).RequireAuthorization(Policies.CanWrite);
        group.MapPost("/reprocess", ReprocessAsync).RequireAuthorization(Policies.CanWrite);
        return endpoints;
    }

    private static async Task<InventoryOverviewResponse> GetOverviewAsync(
        AppDbContext db, CancellationToken cancellationToken)
    {
        var itemData = await db.InventoryItems.AsNoTracking()
            .OrderBy(x => x.Sku)
            .Select(x => new { x.Id, x.Sku, x.Name, x.QuantityOnHand, x.AverageUnitCost })
            .ToArrayAsync(cancellationToken);
        var mappingData = await db.MarketplaceSkuMappings.AsNoTracking()
            .OrderBy(x => x.Platform).ThenBy(x => x.ExternalSku)
            .Select(x => new { x.Id, x.InventoryItemId, x.Platform, x.ExternalSku })
            .ToArrayAsync(cancellationToken);
        var items = itemData.Select(x => new InventoryItemResponse(
            x.Id, x.Sku, x.Name, x.QuantityOnHand, x.AverageUnitCost,
            x.QuantityOnHand * x.AverageUnitCost,
            mappingData.Where(m => m.InventoryItemId == x.Id)
                .Select(m => new SkuMappingResponse(m.Id, m.Platform, m.ExternalSku)).ToArray())).ToArray();
        var issuesData = await (
            from issue in db.InventoryReconciliationIssues.AsNoTracking()
            join order in db.MarketplaceOrders.AsNoTracking() on issue.MarketplaceOrderId equals order.Id
            join item in db.MarketplaceOrderItems.AsNoTracking() on issue.MarketplaceOrderItemId equals item.Id
            where issue.ResolvedAt == null
            orderby issue.CreatedAt descending
            select new
            {
                issue.Id, issue.Type, issue.Details, issue.CreatedAt,
                order.OrderId, order.Platform, ExternalSku = item.Sku, ItemName = item.Title
            }).ToArrayAsync(cancellationToken);
        var issues = issuesData.Select(x => new InventoryIssueResponse(
            x.Id, x.Type, x.Details, x.OrderId, x.Platform, x.ExternalSku,
            x.ItemName, x.CreatedAt)).ToArray();
        var invoices = await db.SupplierInvoices.AsNoTracking().OrderByDescending(x => x.IssuedAt)
            .Select(x => new SupplierInvoiceResponse(
                x.Id, x.AccessKey, x.InvoiceNumber, x.Series, x.IssuedAt,
                x.SupplierTaxId, x.SupplierName, x.Total, x.Items.Count, x.ImportedAt))
            .ToArrayAsync(cancellationToken);
        return new InventoryOverviewResponse(items, issues, invoices);
    }

    private static async Task<IResult> ImportInvoiceAsync(
        IFormFile file,
        INfeXmlParser parser,
        InventoryCostService inventory,
        IExpenseStorage storage,
        ITenantContext tenantContext,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaximumXmlSize)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["NF-e XML must be between 1 byte and 10 MB."] });
        await using var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        ParsedNfeInvoice parsed;
        try
        {
            memory.Position = 0;
            parsed = parser.Parse(memory);
        }
        catch (InvalidDataException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [exception.Message] });
        }
        if (await db.SupplierInvoices.AnyAsync(x => x.AccessKey == parsed.AccessKey, cancellationToken))
            return Results.Conflict(new { message = "This NF-e has already been imported." });
        var tenantId = tenantContext.TenantId!.Value;
        var objectKey = $"tenants/{tenantId:N}/inventory/invoices/{parsed.AccessKey}.xml";
        memory.Position = 0;
        await storage.PutAsync(objectKey, memory, "application/xml", cancellationToken);
        try
        {
            var invoice = await inventory.ImportSupplierInvoiceAsync(
                parsed, cancellationToken, objectKey,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            await inventory.ReprocessOpenIssuesAsync(cancellationToken);
            return Results.Ok(new SupplierInvoiceResponse(
                invoice.Id, invoice.AccessKey, invoice.InvoiceNumber, invoice.Series, invoice.IssuedAt,
                invoice.SupplierTaxId, invoice.SupplierName, invoice.Total, invoice.Items.Count, invoice.ImportedAt));
        }
        catch
        {
            await storage.DeleteAsync(objectKey, cancellationToken);
            throw;
        }
    }

    private static async Task<IResult> CreateMappingAsync(
        CreateSkuMappingRequest request,
        AppDbContext db,
        InventoryCostService inventory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Platform) || string.IsNullOrWhiteSpace(request.ExternalSku))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["mapping"] = ["Platform and external SKU are required."] });
        if (!await db.InventoryItems.AnyAsync(x => x.Id == request.InventoryItemId, cancellationToken))
            return Results.NotFound(new { message = "Inventory item was not found." });
        var platform = request.Platform.Trim().ToLowerInvariant();
        var externalSku = request.ExternalSku.Trim();
        var existing = await db.MarketplaceSkuMappings
            .SingleOrDefaultAsync(x => x.Platform == platform && x.ExternalSku == externalSku, cancellationToken);
        if (existing is not null)
            return existing.InventoryItemId == request.InventoryItemId
                ? Results.Ok(new SkuMappingResponse(existing.Id, existing.Platform, existing.ExternalSku))
                : Results.Conflict(new { message = "This marketplace SKU is already mapped to another inventory item." });
        var mapping = new MarketplaceSkuMapping
        {
            Platform = platform,
            ExternalSku = externalSku,
            InventoryItemId = request.InventoryItemId
        };
        db.MarketplaceSkuMappings.Add(mapping);
        await db.SaveChangesAsync(cancellationToken);
        await inventory.ReprocessOpenIssuesAsync(cancellationToken);
        return Results.Ok(new SkuMappingResponse(mapping.Id, mapping.Platform, mapping.ExternalSku));
    }

    private static async Task<ReprocessInventoryResponse> ReprocessAsync(
        InventoryCostService inventory, CancellationToken cancellationToken) =>
        new(await inventory.ReprocessOpenIssuesAsync(cancellationToken));
}

public sealed record InventoryOverviewResponse(
    InventoryItemResponse[] Items,
    InventoryIssueResponse[] Issues,
    SupplierInvoiceResponse[] Invoices);
public sealed record InventoryItemResponse(
    Guid Id, string Sku, string Name, decimal QuantityOnHand, decimal AverageUnitCost,
    decimal InventoryValue, SkuMappingResponse[] Mappings);
public sealed record SkuMappingResponse(Guid Id, string Platform, string ExternalSku);
public sealed record InventoryIssueResponse(
    Guid Id, string Type, string Details, string OrderId, string Platform,
    string? ExternalSku, string ItemName, DateTimeOffset CreatedAt);
public sealed record SupplierInvoiceResponse(
    Guid Id, string AccessKey, string InvoiceNumber, string Series, DateTimeOffset IssuedAt,
    string SupplierTaxId, string SupplierName, decimal Total, int ItemCount, DateTimeOffset ImportedAt);
public sealed record CreateSkuMappingRequest(string Platform, string ExternalSku, Guid InventoryItemId);
public sealed record ReprocessInventoryResponse(int ProcessedItems);
