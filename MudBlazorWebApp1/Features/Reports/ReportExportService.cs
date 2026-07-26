using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MudBlazorWebApp1.Features.Reports;

public sealed class ReportExportService(AppDbContext db, ITenantContext tenantContext, TimeProvider timeProvider)
{
    private const int ExportLimit = 50_000;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<ExportedReport> ExportAsync(
        ExportReportRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant is required.");
        var query = FinancialEntryQuery.Apply(db.FinancialEntries.AsNoTracking(), request)
            .OrderBy(x => x.OccurredAt);
        if (await query.CountAsync(cancellationToken) > ExportLimit)
            throw new ExportLimitExceededException(ExportLimit);

        var rows = await query.Select(x => new ExportRow(
                x.ExternalId, x.Description, x.Marketplace, x.PaymentMethod, x.Status,
                x.OccurredAt, x.ExpectedAt, x.GrossAmount, x.ReceivedAmount, x.FeeAmount))
            .ToListAsync(cancellationToken);
        var tenantName = await db.Tenants.AsNoTracking().Where(x => x.Id == tenantId)
            .Select(x => x.Name).SingleAsync(cancellationToken);
        var period = FormatPeriod(request);
        var baseName = $"relatorio-{Slug(tenantName)}-{timeProvider.GetUtcNow():yyyyMMdd-HHmm}";

        return request.Format.ToLowerInvariant() switch
        {
            "pdf" => new ExportedReport(CreatePdf(tenantName, period, request.Mode, rows), "application/pdf", $"{baseName}.pdf"),
            "xlsx" => new ExportedReport(CreateExcel(tenantName, period, rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{baseName}.xlsx"),
            "csv" => new ExportedReport(CreateCsv(rows), "text/csv; charset=utf-8", $"{baseName}.csv"),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Format), "Unsupported export format.")
        };
    }

    private static byte[] CreatePdf(string tenantName, string period, string mode, IReadOnlyCollection<ExportRow> rows)
    {
        var totals = Totals(rows);
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(8).FontFamily("Arial"));
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("NUCLEO | RELATORIO FINANCEIRO").FontSize(9).FontColor("#252525").SemiBold();
                        column.Item().Text(tenantName).FontSize(18).Bold().FontColor("#181818");
                        column.Item().Text($"Periodo: {period} | Visao: {(mode == "platform" ? "por plataforma" : "consolidada")}").FontColor("#666666");
                    });
                    row.ConstantItem(145).AlignRight().Column(column =>
                    {
                        column.Item().Text("Documento para conferencia contabil").FontSize(8).SemiBold();
                        column.Item().Text($"Gerado em {DateTimeOffset.Now:dd/MM/yyyy HH:mm}").FontColor("#666666");
                    });
                });
                page.Content().PaddingTop(18).Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Row(row =>
                    {
                        SummaryBox(row.RelativeItem(), "FATURADO", totals.Billed);
                        row.Spacing(8);
                        SummaryBox(row.RelativeItem(), "RECEBIDO", totals.Received);
                        row.Spacing(8);
                        SummaryBox(row.RelativeItem(), "TAXAS", totals.Fees);
                        row.Spacing(8);
                        SummaryBox(row.RelativeItem(), "A RECEBER", totals.Receivable);
                    });

                    column.Item().Text("Resumo mensal consolidado").FontSize(11).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f); columns.RelativeColumn(); columns.RelativeColumn();
                            columns.RelativeColumn(); columns.RelativeColumn();
                        });
                        PdfHeader(table, ["Mes", "Faturado", "Recebido", "Taxas", "A receber"]);
                        foreach (var month in rows.GroupBy(x => new { x.OccurredAt.Year, x.OccurredAt.Month }).OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month))
                        {
                            var monthTotals = Totals(month);
                            PdfCell(table, $"{month.Key.Month:D2}/{month.Key.Year}");
                            PdfMoney(table, monthTotals.Billed); PdfMoney(table, monthTotals.Received);
                            PdfMoney(table, monthTotals.Fees); PdfMoney(table, monthTotals.Receivable);
                        }
                    });

                    foreach (var group in GroupRows(rows, mode))
                    {
                        column.Item().Text(group.Name).FontSize(11).Bold().FontColor("#252525");
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(55); columns.RelativeColumn(1.8f); columns.RelativeColumn();
                                columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                                columns.RelativeColumn(); columns.RelativeColumn();
                            });
                            PdfHeader(table, ["Data", "Lancamento", "Plataforma", "Pagamento", "Status", "Faturado", "Taxas", "A receber"]);
                            foreach (var item in group.Rows)
                            {
                                PdfCell(table, item.OccurredAt.ToLocalTime().ToString("dd/MM/yy"));
                                PdfCell(table, item.Description); PdfCell(table, item.Marketplace);
                                PdfCell(table, item.PaymentMethod); PdfCell(table, item.Status);
                                PdfMoney(table, item.GrossAmount); PdfMoney(table, item.FeeAmount);
                                PdfMoney(table, item.ReceivableAmount);
                            }
                        });
                    }
                    if (rows.Count == 0)
                        column.Item().PaddingVertical(45).AlignCenter().Text("Nenhum lancamento encontrado para os filtros selecionados.").FontColor("#666666");
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Nucleo | ");
                    text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static byte[] CreateExcel(string tenantName, string period, IReadOnlyCollection<ExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Consolidado");
        summary.Cell("A1").Value = "NUCLEO | RELATORIO FINANCEIRO";
        summary.Cell("A2").Value = tenantName;
        summary.Cell("A3").Value = $"Periodo: {period}";
        summary.Range("A1:E1").Merge().Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#252525")).Font.SetFontSize(14);
        summary.Range("A2:E2").Merge().Style.Font.SetBold().Font.SetFontSize(18);
        summary.Range("A3:E3").Merge().Style.Font.SetFontColor(XLColor.Gray);
        WriteExcelHeader(summary, 5, ["Mes", "Faturado", "Recebido", "Taxas", "A receber"]);
        var rowNumber = 6;
        foreach (var month in rows.GroupBy(x => new { x.OccurredAt.Year, x.OccurredAt.Month }).OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month))
        {
            var total = Totals(month);
            summary.Cell(rowNumber, 1).Value = $"{month.Key.Month:D2}/{month.Key.Year}";
            WriteMoney(summary, rowNumber, 2, total.Billed); WriteMoney(summary, rowNumber, 3, total.Received);
            WriteMoney(summary, rowNumber, 4, total.Fees); WriteMoney(summary, rowNumber, 5, total.Receivable);
            rowNumber++;
        }
        var grandTotal = Totals(rows);
        summary.Cell(rowNumber, 1).Value = "TOTAL";
        summary.Row(rowNumber).Style.Font.SetBold();
        WriteMoney(summary, rowNumber, 2, grandTotal.Billed); WriteMoney(summary, rowNumber, 3, grandTotal.Received);
        WriteMoney(summary, rowNumber, 4, grandTotal.Fees); WriteMoney(summary, rowNumber, 5, grandTotal.Receivable);
        summary.Columns().AdjustToContents();
        summary.SheetView.FreezeRows(5);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Consolidado" };
        foreach (var marketplace in rows.GroupBy(x => x.Marketplace).OrderBy(x => x.Key))
        {
            var worksheet = workbook.Worksheets.Add(WorksheetName(marketplace.Key, usedNames));
            WriteExcelHeader(worksheet, 1, ["Data", "ID externo", "Lancamento", "Pagamento", "Status", "Previsto", "Faturado", "Recebido", "Taxas", "A receber"]);
            var currentRow = 2;
            foreach (var item in marketplace)
            {
                worksheet.Cell(currentRow, 1).Value = item.OccurredAt.LocalDateTime;
                worksheet.Cell(currentRow, 1).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                worksheet.Cell(currentRow, 2).Value = item.ExternalId;
                worksheet.Cell(currentRow, 3).Value = item.Description;
                worksheet.Cell(currentRow, 4).Value = item.PaymentMethod;
                worksheet.Cell(currentRow, 5).Value = item.Status;
                if (item.ExpectedAt is { } expected)
                {
                    worksheet.Cell(currentRow, 6).Value = expected.LocalDateTime;
                    worksheet.Cell(currentRow, 6).Style.DateFormat.Format = "dd/mm/yyyy";
                }
                WriteMoney(worksheet, currentRow, 7, item.GrossAmount);
                WriteMoney(worksheet, currentRow, 8, item.ReceivedAmount);
                WriteMoney(worksheet, currentRow, 9, item.FeeAmount);
                WriteMoney(worksheet, currentRow, 10, item.ReceivableAmount);
                currentRow++;
            }
            worksheet.SheetView.FreezeRows(1);
            worksheet.RangeUsed()?.SetAutoFilter();
            worksheet.Columns().AdjustToContents(5, 45);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateCsv(IEnumerable<ExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Data;ID externo;Lancamento;Plataforma;Forma de pagamento;Status;Data prevista;Faturado;Recebido;Taxas;A receber");
        foreach (var item in rows)
        {
            var values = new[]
            {
                item.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), item.ExternalId, item.Description,
                item.Marketplace, item.PaymentMethod, item.Status, item.ExpectedAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? string.Empty,
                Decimal(item.GrossAmount), Decimal(item.ReceivedAmount), Decimal(item.FeeAmount), Decimal(item.ReceivableAmount)
            };
            builder.AppendLine(string.Join(';', values.Select(Csv)));
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static void SummaryBox(IContainer container, string label, decimal value) => container
        .Border(1).BorderColor("#D6D6D6").Background("#F5F5F5").Padding(9).Column(column =>
        {
            column.Item().Text(label).FontSize(7).SemiBold().FontColor("#666666");
            column.Item().Text(Money(value)).FontSize(13).Bold().FontColor("#181818");
        });

    private static void PdfHeader(TableDescriptor table, string[] headers)
    {
        table.Header(header =>
        {
            foreach (var text in headers)
                header.Cell().Background("#252525").Padding(5).Text(text).FontColor(Colors.White).SemiBold();
        });
    }

    private static void PdfCell(TableDescriptor table, string value) =>
        table.Cell().BorderBottom(1).BorderColor("#E1E1E1").Padding(4).Text(value);
    private static void PdfMoney(TableDescriptor table, decimal value) =>
        table.Cell().BorderBottom(1).BorderColor("#E1E1E1").Padding(4).AlignRight().Text(Money(value));

    private static void WriteExcelHeader(IXLWorksheet worksheet, int row, string[] headers)
    {
        for (var column = 1; column <= headers.Length; column++)
            worksheet.Cell(row, column).Value = headers[column - 1];
        worksheet.Range(row, 1, row, headers.Length).Style
            .Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#252525"));
    }

    private static void WriteMoney(IXLWorksheet worksheet, int row, int column, decimal value)
    {
        worksheet.Cell(row, column).Value = value;
        worksheet.Cell(row, column).Style.NumberFormat.Format = "R$ #,##0.00;[Red]-R$ #,##0.00";
    }

    private static (decimal Billed, decimal Received, decimal Fees, decimal Receivable) Totals(IEnumerable<ExportRow> rows)
    {
        var billed = 0m; var received = 0m; var fees = 0m;
        foreach (var row in rows) { billed += row.GrossAmount; received += row.ReceivedAmount; fees += row.FeeAmount; }
        return (billed, received, fees, billed - received - fees);
    }

    private static IEnumerable<(string Name, IEnumerable<ExportRow> Rows)> GroupRows(IEnumerable<ExportRow> rows, string mode) =>
        mode == "platform"
            ? rows.GroupBy(x => x.Marketplace).Select(x => (x.Key, x.AsEnumerable()))
            : [("Lancamentos consolidados", rows)];

    private static string WorksheetName(string name, ISet<string> used)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = string.Concat(name.Select(character => invalid.Contains(character) ? '-' : character));
        clean = string.IsNullOrWhiteSpace(clean) ? "Marketplace" : clean[..Math.Min(clean.Length, 31)];
        var candidate = clean;
        var suffix = 2;
        while (!used.Add(candidate))
        {
            var ending = $"-{suffix++}";
            candidate = clean[..Math.Min(clean.Length, 31 - ending.Length)] + ending;
        }
        return candidate;
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string Money(decimal value) => value.ToString("C", PtBr);
    private static string Decimal(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string FormatPeriod(ReportFilter request) =>
        request.From is null && request.To is null ? "Todos os periodos" : $"{request.From?.ToString("dd/MM/yyyy") ?? "Inicio"} a {request.To?.ToString("dd/MM/yyyy") ?? "Hoje"}";
    private static string Slug(string value) => string.Concat(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
}
