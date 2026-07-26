using Microsoft.AspNetCore.Mvc;

namespace MudBlazorWebApp1.Features.Reports;

public sealed class ExportReportRequest : ReportFilter
{
    [FromQuery] public string Format { get; init; } = "pdf";
    [FromQuery] public string Mode { get; init; } = "consolidated";
}

public sealed record ExportedReport(byte[] Content, string ContentType, string FileName);

internal sealed record ExportRow(
    string ExternalId,
    string Description,
    string Marketplace,
    string PaymentMethod,
    string Status,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ExpectedAt,
    decimal GrossAmount,
    decimal ReceivedAmount,
    decimal FeeAmount)
{
    public decimal ReceivableAmount => GrossAmount - FeeAmount - ReceivedAmount;
}

public sealed class ExportLimitExceededException(int limit)
    : Exception($"The export exceeds the limit of {limit:N0} entries. Reduce the selected period.");
