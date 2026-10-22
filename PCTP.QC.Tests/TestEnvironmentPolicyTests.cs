using Xunit;

namespace PCTP.QC.Tests;

public sealed class TestEnvironmentPolicyTests
{
    [Theory]
    [InlineData("Server=localhost;Database=TEST_PCTP;Trusted_Connection=True", true)]
    [InlineData("Data Source=.;Initial Catalog=PCTP_TEST;Integrated Security=True", true)]
    [InlineData("Server=127.0.0.1;Database=PCTP_TESTING;Trusted_Connection=True", true)]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Initial Catalog=TEST_PCTP;Trusted_Connection=True", true)]
    [InlineData("Server=localhost;Data Source=prod-sql;Database=TEST_PCTP", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;Initial Catalog=PCTP_PROD", false)]
    [InlineData("Network Address=localhost;Database=PCTP_TEST", true)]
    [InlineData("Server=192.168.1.10;Database=TEST_PCTP;User Id=x;Password=y", false)]
    [InlineData("Server=localhost;Database=PCTP;Trusted_Connection=True", false)]
    [InlineData("Server=prod-sql;Database=PCTP_PROD;User Id=x;Password=y", false)]
    [InlineData("", false)]
    public void SqlTargetMustBeLocalAndExplicitlyTestNamed(string connectionString, bool expected)
    {
        Assert.Equal(expected, TestEnvironmentPolicy.IsSafeSqlTestTarget(connectionString));
    }

    [Fact]
    public void ProductionTargetIsRejectedWithActionableMessage()
    {
        var error = Assert.Throws<System.InvalidOperationException>(
            () => TestEnvironmentPolicy.EnsureSafeSqlTestTarget(
                "Server=prod-sql;Database=PCTP_PROD;Trusted_Connection=True"));
        Assert.Contains("blocked", error.Message);
    }
}