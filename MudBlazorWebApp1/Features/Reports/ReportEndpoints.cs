using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Infrastructure;

namespace MudBlazorWebApp1.Features.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reports = endpoints.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization(Policies.TenantMember);

        reports.MapGet("/dashboard", GetDashboardAsync)
            .WithName("GetDashboardReport")
            .WithSummary("Retorna totais e graficos consolidados")
            .Produces<DashboardReport>();
        reports.MapGet("/entries", GetEntriesAsync)
            .WithName("GetFinancialEntries")
            .WithSummary("Lista lancamentos com busca, ordenacao e paginacao")
            .Produces<PagedEntries>()
            .ProducesValidationProblem();
        reports.MapGet("/options", GetOptionsAsync)
            .WithName("GetReportOptions")
            .WithSummary("Retorna as opcoes reais disponiveis para os filtros")
            .Produces<ReportOptions>();
        reports.MapGet("/export", ExportAsync)
            .WithName("ExportFinancialReport")
            .WithSummary("Exporta relatorio consolidado ou por plataforma")
            .WithDescription("Formatos aceitos: pdf, xlsx e csv. Modos aceitos: consolidated e platform.")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }

    private static async Task<IResult> GetDashboardAsync(
        [AsParameters] ReportFilter filter,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (ValidatePeriod(filter) is { } problem)
            return problem;

        var query = FinancialEntryQuery.Apply(db.FinancialEntries.AsNoTracking(), filter);
        var summary = await query.GroupBy(_ => 1).Select(group => new ReportSummary(
            group.Sum(x => x.GrossAmount),
            group.Sum(x => x.ReceivedAmount),
            group.Sum(x => x.FeeAmount),
            group.Sum(x => x.GrossAmount - x.FeeAmount - x.ReceivedAmount))).SingleOrDefaultAsync(cancellationToken)
            ?? new ReportSummary(0, 0, 0, 0);

        var monthlyData = await query
            .GroupBy(x => new { x.OccurredAt.Year, x.OccurredAt.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Billed = group.Sum(x => x.GrossAmount),
                Received = group.Sum(x => x.ReceivedAmount)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);
        var monthly = monthlyData.Select(x => new MonthlyEvolution(
            $"{x.Year:D4}-{x.Month:D2}", x.Billed, x.Received)).ToArray();

        var platformData = await query.GroupBy(x => x.Marketplace)
            .Select(group => new
            {
                Platform = group.Key,
                Billed = group.Sum(x => x.GrossAmount),
                Received = group.Sum(x => x.ReceivedAmount),
                Fees = group.Sum(x => x.FeeAmount)
            })
            .OrderByDescending(x => x.Billed)
            .ToListAsync(cancellationToken);
        var platforms = platformData.Select(x => new PlatformComparison(
            x.Platform, x.Billed, x.Received, x.Fees)).ToArray();

        return Results.Ok(new DashboardReport(summary, monthly, platforms));
    }

    private static async Task<IResult> GetEntriesAsync(
        [AsParameters] EntriesQuery filter,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (ValidatePeriod(filter) is { } periodProblem)
            return periodProblem;
        if (filter.Page < 1 || filter.PageSize is < 5 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Page must be positive and pageSize must be between 5 and 100."]
            });

        var query = FinancialEntryQuery.Apply(db.FinancialEntries.AsNoTracking(), filter);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Description, $"%{search}%") ||
                                     EF.Functions.ILike(x.ExternalId, $"%{search}%"));
        }

        query = (filter.SortBy.ToLowerInvariant(), filter.Descending) switch
        {
            ("description", false) => query.OrderBy(x => x.Description),
            ("description", true) => query.OrderByDescending(x => x.Description),
            ("platform", false) => query.OrderBy(x => x.Marketplace),
            ("platform", true) => query.OrderByDescending(x => x.Marketplace),
            ("amount", false) => query.OrderBy(x => x.GrossAmount),
            ("amount", true) => query.OrderByDescending(x => x.GrossAmount),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            (_, false) => query.OrderBy(x => x.OccurredAt),
            _ => query.OrderByDescending(x => x.OccurredAt)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new EntryRow(
                x.Id, x.ExternalId, x.Description, x.Marketplace, x.PaymentMethod, x.Status,
                x.OccurredAt, x.ExpectedAt, x.GrossAmount, x.ReceivedAmount, x.FeeAmount,
                x.GrossAmount - x.FeeAmount - x.ReceivedAmount))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedEntries(items, total, filter.Page, filter.PageSize));
    }

    private static async Task<ReportOptions> GetOptionsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var optionsData = await db.FinancialEntries.AsNoTracking()
            .Select(x => new { x.Marketplace, x.PaymentMethod, x.Status })
            .Distinct()
            .ToListAsync(cancellationToken);

        var platforms = optionsData.Select(x => x.Marketplace).Distinct().OrderBy(x => x).ToArray();
        var payments = optionsData.Select(x => x.PaymentMethod).Distinct().OrderBy(x => x).ToArray();
        var statuses = optionsData.Select(x => x.Status).Distinct().OrderBy(x => x).ToArray();
        return new ReportOptions(platforms, payments, statuses);
    }

    private static async Task<IResult> ExportAsync(
        [AsParameters] ExportReportRequest request,
        ReportExportService exportService,
        CancellationToken cancellationToken)
    {
        if (ValidatePeriod(request) is { } periodProblem)
            return periodProblem;
        if (request.Format.ToLowerInvariant() is not ("pdf" or "xlsx" or "csv"))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["format"] = ["Format must be pdf, xlsx or csv."]
            });
        if (request.Mode.ToLowerInvariant() is not ("consolidated" or "platform"))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["mode"] = ["Mode must be consolidated or platform."]
            });

        try
        {
            var report = await exportService.ExportAsync(request, cancellationToken);
            return Results.File(report.Content, report.ContentType, report.FileName);
        }
        catch (ExportLimitExceededException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static IResult? ValidatePeriod(ReportFilter filter)
    {
        if (filter.From is null || filter.To is null || filter.From <= filter.To)
            return null;
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["period"] = ["The start date must be earlier than the end date."]
        });
    }
}
