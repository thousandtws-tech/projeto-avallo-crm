using MudBlazorWebApp1.Domain;
using Xunit;

namespace MudBlazorWebApp1.Tests.Features;

public sealed class RolePermissionTests
{
    [Fact]
    public void Accountant_is_read_only_and_admin_manages_accounting()
    {
        Assert.Contains(Roles.Accountant, Roles.All);
        Assert.DoesNotContain(Roles.Accountant, Roles.Writers);
        Assert.DoesNotContain(Roles.Accountant, Roles.AccountingManagers);
        Assert.Contains(Roles.Admin, Roles.Writers);
        Assert.Contains(Roles.Admin, Roles.AccountingManagers);
    }
}
