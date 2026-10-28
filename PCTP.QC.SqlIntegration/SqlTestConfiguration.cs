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
    public const string Database = "B7R2_FCC_QC";
    public const string ReadOnlyPreflightDatabase = "B7R2_FCC";
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
    /// Read-only schema preflight for G3/G4 targets the isolated QC catalog.
    /// The shared catalog is available only through the explicit shared-preflight method.
    /// </summary>
    public static string BuildReadOnlyPreflightConnectionString(string userId, string password)
    {
        var ciConnection = Environment.GetEnvironmentVariable("PCTP_QC_EPHEMERAL_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(ciConnection))
            return ciConnection;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("SQL preflight requires SQL authentication credentials.");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            UserID = userId,
            Password = password,
            TrustServerCertificate = true,
            ConnectTimeout = 5,
            ApplicationName = "PCTP-QC-Isolated-ReadOnly-Preflight",
            ApplicationIntent = ApplicationIntent.ReadOnly
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Explicit metadata-only connection to shared B7R2_FCC. UI uses this only for
    /// the named read-only check; integration gates must use the isolated QC catalog.
    /// </summary>
    public static string BuildSharedReadOnlyPreflightConnectionString(string userId, string password)
    {
        var ciConnection = Environment.GetEnvironmentVariable("PCTP_QC_EPHEMERAL_SQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(ciConnection))
            return ciConnection;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Shared SQL57 preflight requires SQL authentication credentials.");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = ReadOnlyPreflightDatabase,
            UserID = userId,
            Password = password,
            TrustServerCertificate = true,
            ConnectTimeout = 5,
            ApplicationName = "PCTP-QC-Shared-ReadOnly-Preflight",
            ApplicationIntent = ApplicationIntent.ReadOnly
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
        var isEphemeralTarget = ephemeralAllowed &&
            (legacy.DataSource.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase) ||
             legacy.DataSource.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(legacy.InitialCatalog, "PCTP_QC_CI", StringComparison.OrdinalIgnoreCase);
        if (!isFixedTestTarget && !isEphemeralTarget)
            throw new InvalidOperationException("Legacy SQL test connection target does not match an explicitly allowed isolated SQL test environment.");

        return legacy.ConnectionString;
    }

}
