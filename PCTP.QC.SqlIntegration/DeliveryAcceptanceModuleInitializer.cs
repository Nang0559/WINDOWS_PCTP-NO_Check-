using System.Runtime.CompilerServices;
using PCTP.QC.Tests;

namespace PCTP.QC.SqlIntegration;

internal static class DeliveryAcceptanceModuleInitializer
{
    [ModuleInitializer]
    public static void Run()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PCTP_QC_RUN_DEL_ACCEPTANCE"), "1", StringComparison.OrdinalIgnoreCase))
            return;

        var user = Environment.GetEnvironmentVariable("PCTP_QC_SQL_USER");
        var password = Environment.GetEnvironmentVariable("PCTP_QC_SQL_PASSWORD");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("PCTP_QC_RUN_DEL_ACCEPTANCE=1 requires PCTP_QC_SQL_USER and PCTP_QC_SQL_PASSWORD.");

        var connectionString = SqlTestConfiguration.BuildConnectionString(user, password);
        TestEnvironmentPolicy.EnsureSafeSqlTestTarget(connectionString);
        DeliveryProductionAcceptanceScenario.RunAsync(connectionString).GetAwaiter().GetResult();
    }
}
