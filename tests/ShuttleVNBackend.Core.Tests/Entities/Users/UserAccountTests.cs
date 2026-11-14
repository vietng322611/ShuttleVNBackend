using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Core.Tests.Entities.Users;

public class UserAccountTests
{
    [Fact]
    public void GetRole_CustomerAccount_ReturnsCustomer()
    {
        var account = new UserAccount { AccountType = AccountType.Customer };

        Assert.Equal("Customer", account.GetRole());
    }

    [Fact]
    public void GetRole_EmployeeAccount_ReturnsEmployee()
    {
        var account = new UserAccount
        {
            AccountType = AccountType.Employee,
            Employee = new Employee { IsAdmin = false }
        };

        Assert.Equal("Employee", account.GetRole());
    }

    [Fact]
    public void GetRole_AdminEmployeeAccount_ReturnsAdmin()
    {
        var account = new UserAccount
        {
            AccountType = AccountType.Employee,
            Employee = new Employee { IsAdmin = true }
        };

        Assert.Equal("Admin", account.GetRole());
    }

    [Fact]
    public void GetRole_EmployeeAccountWithoutEmployeeRecord_ReturnsEmployee()
    {
        var account = new UserAccount { AccountType = AccountType.Employee, Employee = null };

        Assert.Equal("Employee", account.GetRole());
    }
}
