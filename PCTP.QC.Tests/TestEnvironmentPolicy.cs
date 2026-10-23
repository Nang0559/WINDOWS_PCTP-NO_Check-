using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.Tests;

/// <summary>
/// Fail-closed guard for SQL integration tests.
/// Development uses the dedicated 192.168.200.57/B7R2_FCC test target.
/// The production 192.168.200.14\\BRAVO target is always rejected.
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

        return server.Equals(AllowedTestServer, StringComparison.OrdinalIgnoreCase)
            && db.Equals(AllowedTestDatabase, StringComparison.OrdinalIgnoreCase);
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
