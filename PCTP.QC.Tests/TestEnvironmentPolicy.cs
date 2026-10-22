using System;
using System.Text.RegularExpressions;

namespace PCTP.QC.Tests;

/// <summary>
/// Fail-closed guard for any future SQL integration/performance fixture.
/// Tests are not permitted to connect to production or ambiguous databases.
/// </summary>
public static class TestEnvironmentPolicy
{
    public static bool IsSafeSqlTestTarget(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        var dataSource = ReadValue(connectionString, "Data Source")
            ?? ReadValue(connectionString, "Server");
        var database = ReadValue(connectionString, "Initial Catalog")
            ?? ReadValue(connectionString, "Database");

        if (string.IsNullOrWhiteSpace(dataSource) || string.IsNullOrWhiteSpace(database))
            return false;

        var server = dataSource.Trim().ToLowerInvariant();
        var isLocal = server == "." || server == "(local)" || server == "localhost"
            || server == "127.0.0.1" || server.StartsWith(".\\", StringComparison.Ordinal)
            || server.StartsWith("localhost\\", StringComparison.Ordinal)
            || server.StartsWith("(local)\\", StringComparison.Ordinal);
        var db = database.Trim();
        var isTestDatabase = db.StartsWith("TEST_", StringComparison.OrdinalIgnoreCase)
            || db.EndsWith("_TEST", StringComparison.OrdinalIgnoreCase)
            || db.EndsWith("_TESTING", StringComparison.OrdinalIgnoreCase);

        return isLocal && isTestDatabase;
    }

    public static void EnsureSafeSqlTestTarget(string connectionString)
    {
        if (!IsSafeSqlTestTarget(connectionString))
            throw new InvalidOperationException(
                "SQL integration tests are blocked. Only a local database named TEST_* / *_TEST / *_TESTING is allowed. Production and remote servers are never accepted.");
    }

    private static string ReadValue(string connectionString, string key)
    {
        var match = Regex.Match(connectionString,
            @"(?:^|;)\s*" + Regex.Escape(key) + @"\s*=\s*(?:""(?<quoted>[^""]*)""|(?<plain>[^;]*))",
            RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        return match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value.Trim()
            : match.Groups["plain"].Value.Trim();
    }
}