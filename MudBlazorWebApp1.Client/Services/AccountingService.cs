using System.Globalization;
using MudBlazorWebApp1.Client.Models;

namespace MudBlazorWebApp1.Client.Services;

public sealed class AccountingService(AuthService authService)
{
    public Task<ApiResult<PreliminaryDreModel>> GetPreliminaryDreAsync(
        DateOnly from,
        DateOnly to,
        string? platform,
        CancellationToken cancellationToken = default)
    {
        var query = $"from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        if (!string.IsNullOrWhiteSpace(platform))
            query += $"&platform={Uri.EscapeDataString(platform)}";
        return authService.GetAsync<PreliminaryDreModel>($"api/accounting/dre?{query}", cancellationToken);
    }
}
