using Microsoft.EntityFrameworkCore;
using Avallo.Web.Domain;
using Avallo.Web.Infrastructure;

namespace Avallo.Web.Features.Fiscal;

public sealed class TaxEngine(AppDbContext db, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public async Task<TaxProcessingResult> ProcessOrderAsync(
        Guid marketplaceOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await db.MarketplaceOrders.SingleAsync(x => x.Id == marketplaceOrderId, cancellationToken);
        if (IsCancelledOrReturned(order))
            return await ReverseAsync(order, cancellationToken);
        if (!string.Equals(order.FulfillmentStatus, "Delivered", StringComparison.OrdinalIgnoreCase) ||
            order.DeliveredAt is not { } deliveredAt)
            return new TaxProcessingResult(0, 0, null);

        var profile = await db.TaxProfiles
            .Where(x => x.EffectiveFrom <= deliveredAt && (x.EffectiveTo == null || x.EffectiveTo > deliveredAt))
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            var issue = await AddIssueAsync(order, TaxReconciliationIssueTypes.MissingProfile,
                "No fiscal profile is effective at the order delivery date.", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return new TaxProcessingResult(0, 0, issue);
        }

        var rules = await db.TaxRules
            .Where(x => x.TaxProfileId == profile.Id && x.Status == TaxRuleStatuses.Approved &&
                        x.EffectiveFrom <= deliveredAt && (x.EffectiveTo == null || x.EffectiveTo > deliveredAt))
            .OrderBy(x => x.TaxCode).ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
        rules = SelectEffectiveRules(profile, rules, deliveredAt).ToList();
        if (rules.Count == 0)
        {
            var issue = await AddIssueAsync(order, TaxReconciliationIssueTypes.NoApprovedRule,
                "No approved tax rule is effective at the order delivery date.", cancellationToken);
            await ResolveIssueAsync(order.Id, TaxReconciliationIssueTypes.MissingProfile, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return new TaxProcessingResult(0, 0, issue);
        }

        var simulations = Simulate(order.GrossValue, deliveredAt, profile, rules);
        var created = 0;
        foreach (var simulation in simulations)
        {
            if (await db.TaxAssessments.AnyAsync(x => x.MarketplaceOrderId == order.Id &&
                    x.TaxRuleId == simulation.TaxRuleId && x.Type == TaxAssessmentTypes.Assessment, cancellationToken))
                continue;

            var assessment = new TaxAssessment
            {
                TenantId = RequiredTenantId(),
                MarketplaceOrderId = order.Id,
                TaxRuleId = simulation.TaxRuleId,
                TaxableBase = simulation.TaxableBase,
                Rate = simulation.Rate,
                TaxAmount = simulation.TaxAmount,
                AssessedAt = timeProvider.GetUtcNow()
            };
            db.TaxAssessments.Add(assessment);
            db.AccountingEntries.Add(CreateLedgerEntry(order, assessment, simulation.TaxName));
            created++;
        }
        await ResolveIssueAsync(order.Id, TaxReconciliationIssueTypes.MissingProfile, cancellationToken);
        await ResolveIssueAsync(order.Id, TaxReconciliationIssueTypes.NoApprovedRule, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new TaxProcessingResult(created, 0, null);
    }

    public async Task<int> ReprocessOpenIssuesAsync(CancellationToken cancellationToken = default)
    {
        var orderIds = await db.TaxReconciliationIssues.Where(x => x.ResolvedAt == null)
            .Select(x => x.MarketplaceOrderId).Distinct().ToListAsync(cancellationToken);
        foreach (var orderId in orderIds)
            await ProcessOrderAsync(orderId, cancellationToken);
        return orderIds.Count;
    }

    public static IReadOnlyList<TaxSimulationLine> Simulate(
        decimal amount,
        DateTimeOffset occurredAt,
        TaxProfile profile,
        IEnumerable<TaxRule> rules)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Taxable amount cannot be negative.");
        if (profile.EffectiveFrom > occurredAt || profile.EffectiveTo is { } profileEnd && profileEnd <= occurredAt)
            return [];
        return SelectEffectiveRules(profile, rules, occurredAt)
            .Select(rule => new TaxSimulationLine(rule.Id, rule.TaxCode, rule.TaxName, amount, rule.Rate,
                Money(amount * rule.Rate / 100m)))
            .ToList();
    }

    public static IEnumerable<TaxRule> SelectEffectiveRules(
        TaxProfile profile,
        IEnumerable<TaxRule> rules,
        DateTimeOffset occurredAt) => rules
        .Where(x => x.TaxProfileId == profile.Id && x.Status == TaxRuleStatuses.Approved &&
                    x.EffectiveFrom <= occurredAt && (x.EffectiveTo == null || x.EffectiveTo > occurredAt))
        .GroupBy(x => x.TaxCode, StringComparer.OrdinalIgnoreCase)
        .Select(x => x.OrderByDescending(rule => rule.EffectiveFrom).ThenByDescending(rule => rule.Version).First());

    private async Task<TaxProcessingResult> ReverseAsync(
        MarketplaceOrder order,
        CancellationToken cancellationToken)
    {
        var originals = await db.TaxAssessments.Where(x => x.MarketplaceOrderId == order.Id &&
                x.Type == TaxAssessmentTypes.Assessment)
            .ToListAsync(cancellationToken);
        var reversed = 0;
        foreach (var original in originals)
        {
            if (await db.TaxAssessments.AnyAsync(x => x.ReversesAssessmentId == original.Id, cancellationToken))
                continue;
            var reversal = new TaxAssessment
            {
                TenantId = RequiredTenantId(),
                MarketplaceOrderId = order.Id,
                TaxRuleId = original.TaxRuleId,
                Type = TaxAssessmentTypes.Reversal,
                TaxableBase = -original.TaxableBase,
                Rate = original.Rate,
                TaxAmount = -original.TaxAmount,
                AssessedAt = timeProvider.GetUtcNow(),
                ReversesAssessmentId = original.Id
            };
            db.TaxAssessments.Add(reversal);
            db.AccountingEntries.Add(CreateReversalLedgerEntry(order, original, reversal));
            reversed++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return new TaxProcessingResult(0, reversed, null);
    }

    private async Task<TaxReconciliationIssue> AddIssueAsync(
        MarketplaceOrder order,
        string type,
        string details,
        CancellationToken cancellationToken)
    {
        var eventKey = $"order:{order.Id}:tax:{type}";
        var existing = await db.TaxReconciliationIssues
            .SingleOrDefaultAsync(x => x.EventKey == eventKey, cancellationToken);
        if (existing is not null)
            return existing;
        var issue = new TaxReconciliationIssue
        {
            TenantId = RequiredTenantId(),
            MarketplaceOrderId = order.Id,
            EventKey = eventKey,
            Type = type,
            Details = details,
            CreatedAt = timeProvider.GetUtcNow()
        };
        db.TaxReconciliationIssues.Add(issue);
        return issue;
    }

    private async Task ResolveIssueAsync(Guid orderId, string type, CancellationToken cancellationToken)
    {
        var issue = await db.TaxReconciliationIssues
            .SingleOrDefaultAsync(x => x.MarketplaceOrderId == orderId && x.Type == type && x.ResolvedAt == null,
                cancellationToken);
        if (issue is not null)
            issue.ResolvedAt = timeProvider.GetUtcNow();
    }

    private AccountingEntry CreateLedgerEntry(MarketplaceOrder order, TaxAssessment assessment, string taxName)
    {
        var id = Guid.NewGuid();
        var entry = new AccountingEntry
        {
            Id = id,
            TenantId = RequiredTenantId(),
            EventKey = $"tax-assessment:{assessment.Id}",
            Type = AccountingEntryTypes.TaxAssessment,
            SourceType = nameof(TaxAssessment),
            SourceId = assessment.Id.ToString(),
            Description = $"{taxName} do pedido {order.OrderId}",
            OccurredAt = order.DeliveredAt!.Value
        };
        entry.Postings.Add(Posting(id, order, AccountingAccounts.TaxOnSales, "Impostos sobre vendas", assessment.TaxAmount, 0));
        entry.Postings.Add(Posting(id, order, AccountingAccounts.TaxesPayable, "Impostos a recolher", 0, assessment.TaxAmount));
        return entry;
    }

    private AccountingEntry CreateReversalLedgerEntry(
        MarketplaceOrder order,
        TaxAssessment original,
        TaxAssessment reversal)
    {
        var id = Guid.NewGuid();
        var entry = new AccountingEntry
        {
            Id = id,
            TenantId = RequiredTenantId(),
            EventKey = $"tax-reversal:{original.Id}",
            Type = AccountingEntryTypes.TaxReversal,
            SourceType = nameof(TaxAssessment),
            SourceId = reversal.Id.ToString(),
            Description = $"Estorno de impostos do pedido {order.OrderId}",
            OccurredAt = timeProvider.GetUtcNow()
        };
        entry.Postings.Add(Posting(id, order, AccountingAccounts.TaxesPayable, "Impostos a recolher", original.TaxAmount, 0));
        entry.Postings.Add(Posting(id, order, AccountingAccounts.TaxOnSales, "Deducao de impostos sobre vendas", 0, original.TaxAmount));
        return entry;
    }

    private AccountingPosting Posting(
        Guid entryId,
        MarketplaceOrder order,
        string accountCode,
        string accountName,
        decimal debit,
        decimal credit) => new()
    {
        TenantId = RequiredTenantId(),
        AccountingEntryId = entryId,
        AccountCode = accountCode,
        AccountName = accountName,
        Marketplace = order.Platform,
        Currency = order.Currency,
        Debit = debit,
        Credit = credit
    };

    private Guid RequiredTenantId() => tenantContext.TenantId
        ?? throw new UnauthorizedAccessException("A tenant is required to process taxes.");

    private static bool IsCancelledOrReturned(MarketplaceOrder order) =>
        string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(order.FulfillmentStatus, "Returned", StringComparison.OrdinalIgnoreCase);

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record TaxSimulationLine(
    Guid TaxRuleId,
    string TaxCode,
    string TaxName,
    decimal TaxableBase,
    decimal Rate,
    decimal TaxAmount);

public sealed record TaxProcessingResult(
    int AssessmentsCreated,
    int ReversalsCreated,
    TaxReconciliationIssue? Issue);
