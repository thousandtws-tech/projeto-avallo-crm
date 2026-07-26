namespace MudBlazorWebApp1.Domain;

public static class Roles
{
    public const string Admin = nameof(Admin);
    public const string Seller = "Vendedor";
    public const string Accountant = "Contador";

    public static readonly string[] All = [Admin, Seller, Accountant];
    public static readonly string[] Writers = [Admin, Seller];
    public static readonly string[] AccountingManagers = [Admin];
}

public static class Policies
{
    public const string TenantMember = nameof(TenantMember);
    public const string CanWrite = nameof(CanWrite);
    public const string CanManageUsers = nameof(CanManageUsers);
    public const string CanReviewAccounting = nameof(CanReviewAccounting);
}
