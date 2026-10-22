using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace PCTP.QC.Tests;

/// <summary>
/// Fail-closed guard for any future SQL integration/performance fixture.
/// Accepts exactly one local server alias and one explicitly test-named database.
/// Ambiguous/duplicate aliases are rejected to prevent connection-string parser differentials.
/// </summary>
public static class TestEnvironmentPolicy
{
    private static readonly string[] ServerAliases =
        { "Data Source", "Server", "Address", "Addr", "Network Address" };
    private static readonly string[] DatabaseAliases =
        { "Initial Catalog", "Database" };

    public static bool IsSafeSqlTestTarget(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        // Provider versions can reject special keywords with either exception type.
        // Both cases must fail closed; they must never escape this safety guard.
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
            // Microsoft.Data.SqlClient does not expose ContextConnection on
            // SqlConnectionStringBuilder. Reject the keyword explicitly.
            || ReadValues(connectionString, new[] { "Context Connection" }).Count > 0)
            return false;

        var servers = ReadValues(connectionString, ServerAliases);
        var databases = ReadValues(connectionString, DatabaseAliases);
        if (servers.Count != 1 || databases.Count != 1) return false;

        var server = builder.DataSource.Trim().ToLowerInvariant();
        var isLocal = server == "." || server == "(local)" || server == "localhost"
            || server == "127.0.0.1" || server == "::1"
            || server.StartsWith(".\\", StringComparison.Ordinal)
            || server.StartsWith("localhost\\", StringComparison.Ordinal)
            || server.StartsWith("(local)\\", StringComparison.Ordinal)
            || server.StartsWith("(localdb)\\", StringComparison.Ordinal);

        var db = builder.InitialCatalog.Trim();
        var isTestDatabase = db.StartsWith("TEST_", StringComparison.OrdinalIgnoreCase)
            || db.EndsWith("_TEST", StringComparison.OrdinalIgnoreCase)
            || db.EndsWith("_TESTING", StringComparison.OrdinalIgnoreCase);

        return isLocal && isTestDatabase;
    }

    public static void EnsureSafeSqlTestTarget(string connectionString)
    {
        if (!IsSafeSqlTestTarget(connectionString))
            throw new InvalidOperationException(
                "SQL integration tests are blocked. Exactly one local server and one TEST_* / *_TEST / *_TESTING database must be specified. Production, remote, and ambiguous targets are rejected.");
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
