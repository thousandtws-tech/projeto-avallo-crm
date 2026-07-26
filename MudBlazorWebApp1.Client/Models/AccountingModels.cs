namespace MudBlazorWebApp1.Client.Models;

public sealed record PreliminaryDreModel(
    DateOnly? From,
    DateOnly? To,
    string? Platform,
    decimal GrossRevenue,
    decimal SalesDeductions,
    decimal TaxesOnSales,
    decimal NetRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    decimal MarketplaceCommission,
    decimal PaymentFees,
    decimal ShippingExpenses,
    decimal OtherSellingExpenses,
    decimal SellingExpenses,
    decimal OperatingExpenses,
    decimal PreliminaryProfit,
    DreAccountBalanceModel[] Accounts);

public sealed record DreAccountBalanceModel(
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit);
