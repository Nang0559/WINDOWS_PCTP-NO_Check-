using Xunit;

namespace PCTP.QC.Tests;

public sealed class TestEnvironmentPolicyTests
{
    // The QC runner is intentionally restricted to one explicit test target.
    // "Local" or a database name containing TEST is not sufficient authorization.
    [Theory]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC_QC;Trusted_Connection=True", true)]
    [InlineData("Data Source=192.168.200.57;Initial Catalog=B7R2_FCC_QC;Integrated Security=True", true)]
    [InlineData("Network Address=192.168.200.57;Database=B7R2_FCC_QC;Integrated Security=True", true)]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC;Trusted_Connection=True", false)]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC_QC;Trusted_Connection=True", true)]
    [InlineData("Server=192.168.200.57;Database=OTHER_TEST_DB;Trusted_Connection=True", false)]
    [InlineData("Server=192.168.200.57;Database=PCTP_PROD;Trusted_Connection=True", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;Trusted_Connection=True", false)]
    [InlineData("Data Source=.;Initial Catalog=PCTP_TEST;Integrated Security=True", false)]
    [InlineData("Server=127.0.0.1;Database=PCTP_TESTING;Trusted_Connection=True", false)]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Initial Catalog=TEST_PCTP;Trusted_Connection=True", false)]
    [InlineData("Network Address=localhost;Database=PCTP_TEST", false)]
    [InlineData("Server=192.168.200.14\\BRAVO;Database=B7R2_FCC;Trusted_Connection=True", false)]
    [InlineData("Server=localhost;Data Source=prod-sql;Database=TEST_PCTP", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;Initial Catalog=PCTP_PROD", false)]
    [InlineData("Server=192.168.1.10;Database=TEST_PCTP;User Id=x;Password=y", false)]
    [InlineData("Server=localhost;Database=PCTP;Trusted_Connection=True", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;AttachDbFilename=C:\\\\Data\\\\prod.mdf", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;Failover Partner=prod-sql", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;User Instance=True", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;Context Connection=True", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;Attach DB Filename=C:\\\\Data\\\\prod.mdf", false)]
    [InlineData("Server=localhost;Database=TEST_PCTP;not-a-valid-option=x", false)]
    [InlineData("Server=prod-sql;Database=PCTP_PROD;User Id=x;Password=y", false)]
    [InlineData("", false)]
    public void SqlTargetMustMatchExplicitAllowlist(string connectionString, bool expected)
    {
        Assert.Equal(expected, TestEnvironmentPolicy.IsSafeSqlTestTarget(connectionString));
    }


    [Theory]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC;ApplicationIntent=ReadOnly;Trusted_Connection=True", true)]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC;Trusted_Connection=True", false)]
    [InlineData("Server=192.168.200.14\\\\BRAVO;Database=B7R2_FCC;ApplicationIntent=ReadOnly;Trusted_Connection=True", false)]
    public void SharedCatalogIsAllowedOnlyForExplicitReadOnlyPreflight(string connectionString, bool expected)
    {
        Assert.Equal(expected, TestEnvironmentPolicy.IsSafeSqlReadOnlyPreflightTarget(connectionString));
    }

    [Fact]
    public void ProductionTargetIsRejectedWithActionableMessage()
    {
        var error = Assert.Throws<System.InvalidOperationException>(
            () => TestEnvironmentPolicy.EnsureSafeSqlTestTarget(
                "Server=192.168.200.14\\BRAVO;Database=B7R2_FCC;Trusted_Connection=True"));
        Assert.Contains("blocked", error.Message);
    }
}
