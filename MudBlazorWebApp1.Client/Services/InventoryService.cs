using Microsoft.AspNetCore.Components.Forms;
using MudBlazorWebApp1.Client.Models;

namespace MudBlazorWebApp1.Client.Services;

public sealed class InventoryService(AuthService authService)
{
    private const long MaximumXmlSize = 10 * 1024 * 1024;

    public Task<ApiResult<InventoryOverviewModel>> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        authService.GetAsync<InventoryOverviewModel>("api/inventory/overview", cancellationToken);

    public async Task<ApiResult<SupplierInvoiceModel>> ImportInvoiceAsync(
        IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (file.Size is <= 0 or > MaximumXmlSize)
            return new ApiResult<SupplierInvoiceModel>(false, Error: "O XML deve ter no maximo 10 MB.");
        await using var source = file.OpenReadStream(MaximumXmlSize, cancellationToken);
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        return await authService.PostFileAsync<SupplierInvoiceModel>(
            "api/inventory/invoices/import", memory.ToArray(), file.Name, "application/xml", cancellationToken);
    }

    public Task<ApiResult<SkuMappingModel>> CreateMappingAsync(
        string platform, string externalSku, Guid inventoryItemId,
        CancellationToken cancellationToken = default) =>
        authService.PostAsync<object, SkuMappingModel>("api/inventory/mappings",
            new { platform, externalSku, inventoryItemId }, cancellationToken);

    public Task<ApiResult<ReprocessInventoryModel>> ReprocessAsync(CancellationToken cancellationToken = default) =>
        authService.PostAsync<ReprocessInventoryModel>("api/inventory/reprocess", cancellationToken);
}
