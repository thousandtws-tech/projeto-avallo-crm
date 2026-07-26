namespace MudBlazorWebApp1.Client.Models;

public sealed record InventoryOverviewModel(
    InventoryItemModel[] Items,
    InventoryIssueModel[] Issues,
    SupplierInvoiceModel[] Invoices);
public sealed record InventoryItemModel(
    Guid Id, string Sku, string Name, decimal QuantityOnHand, decimal AverageUnitCost,
    decimal InventoryValue, SkuMappingModel[] Mappings);
public sealed record SkuMappingModel(Guid Id, string Platform, string ExternalSku);
public sealed record InventoryIssueModel(
    Guid Id, string Type, string Details, string OrderId, string Platform,
    string? ExternalSku, string ItemName, DateTimeOffset CreatedAt);
public sealed record SupplierInvoiceModel(
    Guid Id, string AccessKey, string InvoiceNumber, string Series, DateTimeOffset IssuedAt,
    string SupplierTaxId, string SupplierName, decimal Total, int ItemCount, DateTimeOffset ImportedAt);
public sealed record ReprocessInventoryModel(int ProcessedItems);
