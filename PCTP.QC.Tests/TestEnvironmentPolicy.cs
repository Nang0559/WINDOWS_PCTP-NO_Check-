using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.Tests;

/// <summary>
/// Fail-closed guard for SQL integration tests.
/// Development may use the dedicated DB57 test target when explicitly requested.
/// CI sets PCTP_QC_REQUIRE_EPHEMERAL_SQL=true, which rejects DB57 as well as production DB14.
/// </summary>
public static class TestEnvironmentPolicy
{
    private static readonly string[] ServerAliases =
        { "Data Source", "Server", "Address", "Addr", "Network Address" };
    private static readonly string[] DatabaseAliases =
        { "Initial Catalog", "Database" };

    private const string AllowedTestServer = "192.168.200.57";
    private const string AllowedTestDatabase = "B7R2_FCC";
    private const string BlockedProductionServer = "192.168.200.14\\BRAVO";

    public static bool IsSafeSqlTestTarget(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
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

        var server = builder.DataSource.Trim();
        var db = builder.InitialCatalog.Trim();

        if (server.Equals(BlockedProductionServer, StringComparison.OrdinalIgnoreCase))
            return false;

        var requireEphemeral = string.Equals(
            Environment.GetEnvironmentVariable("PCTP_QC_REQUIRE_EPHEMERAL_SQL"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (requireEphemeral)
        {
            var isLocal = server.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || server.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || server.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase)
                || server.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase);
            return isLocal && db.Equals("PCTP_QC_CI", StringComparison.OrdinalIgnoreCase);
        }

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

    public static void EnsureSafeSqlTestTarget(string connectionString)
    {
        if (!IsSafeSqlTestTarget(connectionString))
            throw new InvalidOperationException(
                $"SQL integration tests are blocked. CI-required mode permits only 127.0.0.1/localhost PCTP_QC_CI; development mode permits only {AllowedTestServer}/{AllowedTestDatabase}; production {BlockedProductionServer} is always rejected.");
    }

    /// <summary>
    /// Allows the rework repository fixture to use its own empty database only in strict CI mode.
    /// This keeps hand-built dbo fixtures isolated from the DB57-derived schema under test.
    /// </summary>
    public static bool IsSafeSqlFixtureTarget(string connectionString)
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
        if (!string.Equals(Environment.GetEnvironmentVariable("PCTP_QC_REQUIRE_EPHEMERAL_SQL"),
                "true", StringComparison.OrdinalIgnoreCase))
            return false;

        var server = builder.DataSource.Trim();
        var isLocal = server.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || server.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || server.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase)
            || server.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase);
        return isLocal
            && builder.InitialCatalog.Equals("PCTP_QC_REWORK_CI", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureSafeSqlFixtureTarget(string connectionString)
    {
        if (!IsSafeSqlFixtureTarget(connectionString))
            throw new InvalidOperationException(
                "SQL repository fixtures are blocked. Only localhost/127.0.0.1 PCTP_QC_REWORK_CI is permitted while PCTP_QC_REQUIRE_EPHEMERAL_SQL=true.");
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
