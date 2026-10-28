using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.Tests;

/// <summary>
/// Fail-closed guard for SQL integration tests.
/// Integration uses isolated 192.168.200.57/B7R2_FCC_QC; shared B7R2_FCC is read-only-preflight only.
/// The production 192.168.200.14\\BRAVO target is always rejected.
/// </summary>
public static class TestEnvironmentPolicy
{
    private static readonly string[] ServerAliases =
        { "Data Source", "Server", "Address", "Addr", "Network Address" };
    private static readonly string[] DatabaseAliases =
        { "Initial Catalog", "Database" };

    private const string AllowedTestServer = "192.168.200.57";
    private const string AllowedTestDatabase = "B7R2_FCC_QC";
    private const string ReadOnlyPreflightDatabase = "B7R2_FCC";
    private const string BlockedProductionServer = "192.168.200.14\\BRAVO";

    public static bool IsSafeSqlTestTarget(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename)
            || !string.IsNullOrWhiteSpace(builder.FailoverPartner)
            || builder.UserInstance
            || ReadValues(connectionString, new[] { "Context Connection" }).Count > 0)
            return false;

        var servers = ReadValues(connectionString, ServerAliases);
        var databases = ReadValues(connectionString, DatabaseAliases);
        if (servers.Count != 1 || databases.Count != 1) return false;

        var server = builder.DataSource.Trim();
        var db = builder.InitialCatalog.Trim();

        if (server.Equals(BlockedProductionServer, StringComparison.OrdinalIgnoreCase))
            return false;

        var allowEphemeral = string.Equals(
            Environment.GetEnvironmentVariable("PCTP_QC_ALLOW_EPHEMERAL_SQL"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (allowEphemeral &&
            (server.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             server.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
             server.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase) ||
             server.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)))
            return db.Equals("PCTP_QC_CI", StringComparison.OrdinalIgnoreCase);

        return server.Equals(AllowedTestServer, StringComparison.OrdinalIgnoreCase)
            && db.Equals(AllowedTestDatabase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Allows only metadata/schema preflight against the shared SQL57 catalog.
    /// Full integration and all mutation-capable acceptance modes must use B7R2_FCC_QC*.
    /// </summary>
    public static bool IsSafeSqlReadOnlyPreflightTarget(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        SqlConnectionStringBuilder builder;
        try { builder = new SqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }

        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename)
            || !string.IsNullOrWhiteSpace(builder.FailoverPartner)
            || builder.UserInstance
            || ReadValues(connectionString, new[] { "Context Connection" }).Count > 0)
            return false;

        var servers = ReadValues(connectionString, ServerAliases);
        var databases = ReadValues(connectionString, DatabaseAliases);
        if (servers.Count != 1 || databases.Count != 1) return false;
        if (!builder.DataSource.Trim().Equals(AllowedTestServer, StringComparison.OrdinalIgnoreCase))
            return false;

        var db = builder.InitialCatalog.Trim();
        if (db.Equals(ReadOnlyPreflightDatabase, StringComparison.OrdinalIgnoreCase))
            return builder.ApplicationIntent == ApplicationIntent.ReadOnly;
        return IsSafeSqlTestTarget(connectionString);
    }

    public static void EnsureSafeSqlReadOnlyPreflightTarget(string connectionString)
    {
        if (!IsSafeSqlReadOnlyPreflightTarget(connectionString))
            throw new InvalidOperationException(
                $"SQL read-only preflight is blocked. Shared {AllowedTestServer}/{ReadOnlyPreflightDatabase} requires ApplicationIntent=ReadOnly; integration tests require {AllowedTestServer}/{AllowedTestDatabase}.");
    }

    public static void EnsureSafeSqlTestTarget(string connectionString)
    {
        if (!IsSafeSqlTestTarget(connectionString))
            throw new InvalidOperationException(
                $"SQL integration tests are blocked. Only {AllowedTestServer}/{AllowedTestDatabase} is allowed in the development QC runner; production {BlockedProductionServer} is rejected.");
    }

    public static string DescribeTarget(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return $"{builder.DataSource}/{builder.InitialCatalog}";
    }

    private static List<string> ReadValues(string connectionString, string[] aliases)
    {
        var values = new List<string>();
        foreach (var alias in aliases)
        {
            var pattern = @"(?:^|;)\s*" + Regex.Escape(alias) +
                @"\s*=\s*(?:""(?<quoted>[^""]*)""|(?<plain>[^;]*))";
            foreach (Match match in Regex.Matches(connectionString, pattern, RegexOptions.IgnoreCase))
            {
                values.Add(match.Groups["quoted"].Success
                    ? match.Groups["quoted"].Value.Trim()
                    : match.Groups["plain"].Value.Trim());
            }
        }
        return values;
    }
}
