using System;
using Xunit;

namespace PCTP.QC.Tests;

[CollectionDefinition("TestEnvironmentPolicy", DisableParallelization = true)]
public sealed class TestEnvironmentPolicyCollection { }

/// <summary>
/// The policy reads process-wide environment variables, and the CI workflow sets
/// PCTP_QC_REQUIRE_EPHEMERAL_SQL=true for every job (including the DB-less unit job). These tests therefore
/// pin the mode they assert instead of inheriting it: the constructor selects development mode and
/// Dispose restores the caller's values. CI mode has its own explicit cases below.
/// </summary>
[Collection("TestEnvironmentPolicy")]
public sealed class TestEnvironmentPolicyTests : IDisposable
{
    private const string Require = "PCTP_QC_REQUIRE_EPHEMERAL_SQL";
    private const string Allow = "PCTP_QC_ALLOW_EPHEMERAL_SQL";
    private readonly string? _savedRequire = Environment.GetEnvironmentVariable(Require);
    private readonly string? _savedAllow = Environment.GetEnvironmentVariable(Allow);

    public TestEnvironmentPolicyTests()
    {
        Environment.SetEnvironmentVariable(Require, null); // development mode
        Environment.SetEnvironmentVariable(Allow, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Require, _savedRequire);
        Environment.SetEnvironmentVariable(Allow, _savedAllow);
    }

    [Theory]
    [InlineData("Server=127.0.0.1,1433;Initial Catalog=PCTP_QC_CI;User ID=sa;Password=x;Encrypt=True;TrustServerCertificate=True", true)]
    [InlineData("Server=localhost;Database=PCTP_QC_CI;Trusted_Connection=True", true)]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC;Trusted_Connection=True", false)]   // DB57 rejected in CI
    [InlineData("Server=127.0.0.1;Database=B7R2_FCC;User Id=x;Password=y", false)]          // local but wrong database
    [InlineData("Server=192.168.200.14\\BRAVO;Database=B7R2_FCC;Trusted_Connection=True", false)] // DB14 always rejected
    [InlineData("Server=192.168.1.10;Database=PCTP_QC_CI;User Id=x;Password=y", false)]     // right name, remote host
    public void CiRequiredMode_PermitsOnlyLocalEphemeralDatabase(string connectionString, bool expected)
    {
        Environment.SetEnvironmentVariable(Require, "true");
        Assert.Equal(expected, TestEnvironmentPolicy.IsSafeSqlTestTarget(connectionString));
    }

    [Fact]
    public void AllowEphemeralMode_AcceptsLocalCiDatabaseButStillRejectsDb14()
    {
        Environment.SetEnvironmentVariable(Allow, "true");
        Assert.True(TestEnvironmentPolicy.IsSafeSqlTestTarget("Server=127.0.0.1,1433;Initial Catalog=PCTP_QC_CI;User ID=sa;Password=x"));
        Assert.False(TestEnvironmentPolicy.IsSafeSqlTestTarget("Server=192.168.200.14\\BRAVO;Database=PCTP_QC_CI;Trusted_Connection=True"));
    }

    // The QC runner is intentionally restricted to one explicit test target.
    // "Local" or a database name containing TEST is not sufficient authorization.
    [Theory]
    [InlineData("Server=192.168.200.57;Database=B7R2_FCC;Trusted_Connection=True", true)]
    [InlineData("Data Source=192.168.200.57;Initial Catalog=B7R2_FCC;Integrated Security=True", true)]
    [InlineData("Network Address=192.168.200.57;Database=B7R2_FCC;Integrated Security=True", true)]
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

    [Fact]
    public void ProductionTargetIsRejectedWithActionableMessage()
    {
        var error = Assert.Throws<System.InvalidOperationException>(
            () => TestEnvironmentPolicy.EnsureSafeSqlTestTarget(
                "Server=192.168.200.14\\BRAVO;Database=B7R2_FCC;Trusted_Connection=True"));
        Assert.Contains("blocked", error.Message);
    }
}
