using Microsoft.Data.SqlClient;

namespace PCTP.QC.SqlIntegration;

/// <summary>
/// Development SQL target for the QC integration runner.
/// The test server/database are fixed here so the runner cannot be pointed at production by accident.
/// Credentials are supplied by the developer at run time and are not stored in the repository.
/// </summary>
public static class SqlTestConfiguration
{
    public const string Server = "192.168.200.57";
    public const string Database = "B7R2_FCC";
    public const string ProductionServer = "192.168.200.14\\BRAVO";

    public static string DescribeTarget(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return $"{builder.DataSource}/{builder.InitialCatalog}";
    }

    public static string BuildConnectionString(string userId, string password)
    {
        // CI may opt into an ephemeral SQL Server container. The target is still
        // validated by TestEnvironmentPolicy before any query is executed.
        var ciConnection = Environment.GetEnvironmentVariable("PCTP_QC_EPHEMERAL_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(ciConnection))
            return ciConnection;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("SQL Test requires SQL authentication credentials.");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            UserID = userId,
            Password = password,
            TrustServerCertificate = true,
            ConnectTimeout = 5,
            ApplicationName = "PCTP-QC-SQL-Integration"
        };
        return builder.ConnectionString;
    }
    /// <summary>
    /// Builds a connection string compatible with the legacy System.Data.SqlClient
    /// used by the production repositories. Microsoft.Data.SqlClient 6 emits the
    /// newer "Trust Server Certificate" keyword, which the legacy provider rejects.
    /// The target remains the same fixed test server/database; encryption options
    /// are intentionally not copied across providers.
    /// </summary>
    public static string BuildLegacySqlClientConnectionString(string modernConnectionString)
    {
        var modern = new SqlConnectionStringBuilder(modernConnectionString);
        var legacy = new System.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = modern.DataSource,
            InitialCatalog = modern.InitialCatalog,
            UserID = modern.UserID,
            Password = modern.Password,
            ConnectTimeout = modern.ConnectTimeout,
            ApplicationName = modern.ApplicationName,
            PersistSecurityInfo = modern.PersistSecurityInfo
        };

        var ephemeralAllowed = string.Equals(
            Environment.GetEnvironmentVariable("PCTP_QC_ALLOW_EPHEMERAL_SQL"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var isFixedTestTarget =
            string.Equals(legacy.DataSource, Server, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(legacy.InitialCatalog, Database, StringComparison.OrdinalIgnoreCase);
        var isLocalEphemeralHost =
            legacy.DataSource.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            legacy.DataSource.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            legacy.DataSource.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase) ||
            legacy.DataSource.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase);
        var isEphemeralTarget = ephemeralAllowed && isLocalEphemeralHost &&
            string.Equals(legacy.InitialCatalog, "PCTP_QC_CI", StringComparison.OrdinalIgnoreCase);
        var requireEphemeral = string.Equals(
            Environment.GetEnvironmentVariable("PCTP_QC_REQUIRE_EPHEMERAL_SQL"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var isReworkFixtureTarget = ephemeralAllowed && requireEphemeral && isLocalEphemeralHost &&
            string.Equals(legacy.InitialCatalog, "PCTP_QC_REWORK_CI", StringComparison.OrdinalIgnoreCase);
        if (!isFixedTestTarget && !isEphemeralTarget && !isReworkFixtureTarget)
            throw new InvalidOperationException("Legacy SQL test connection target does not match an explicitly allowed isolated SQL test environment.");

        return legacy.ConnectionString;
    }

}
