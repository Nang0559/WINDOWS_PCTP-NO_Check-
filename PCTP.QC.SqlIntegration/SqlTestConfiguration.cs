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

    public static string BuildConnectionString(string userId, string password)
    {
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

        if (!string.Equals(legacy.DataSource, Server, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(legacy.InitialCatalog, Database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Legacy SQL test connection target does not match the fixed test environment.");

        return legacy.ConnectionString;
    }

}
