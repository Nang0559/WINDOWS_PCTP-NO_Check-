using PCTP.Shared.Common;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class SqlConnectionRetryPolicyTests
{
    [Theory]
    [InlineData(-2)]
    [InlineData(2)]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(233)]
    [InlineData(10053)]
    [InlineData(10054)]
    [InlineData(10060)]
    [InlineData(11001)]
    public void TransientNetworkErrorsAreEligibleForConnectionOpenRetry(int number)
    {
        Assert.True(SqlConnectionRetryPolicy.IsTransientConnectionErrorNumber(number));
    }

    [Theory]
    [InlineData(18456)] // login failed
    [InlineData(2627)]  // duplicate key
    [InlineData(547)]   // constraint violation
    [InlineData(1205)]  // deadlock: transaction retry requires whole-operation policy
    [InlineData(208)]   // missing table
    public void BusinessAndCommandErrorsAreNotConnectionOpenRetries(int number)
    {
        Assert.False(SqlConnectionRetryPolicy.IsTransientConnectionErrorNumber(number));
    }
}
