using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Source guard: the compiled-in default connection strings of SQLPROVIDER must never point at Production
/// (192.168.200.14 / BRAVO), and every default that mentions a real server must go through
/// QcSqlOverride.Resolve so the isolated CI fixture can redirect it (and fail closed otherwise).
/// </summary>
public sealed class SqlDefaultConnectionSafetyTests
{
    private static string[] CodeLines()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "PCTP", "Infrastructure", "Sql", "SQL.cs");
        Assert.True(File.Exists(path), "SQL.cs not found: " + path);
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith("//", StringComparison.Ordinal))
            .ToArray();
    }

    [Theory]
    [InlineData("B7R2_FCCdb")]
    [InlineData("B7R2_FCCdbb")]
    public void AppDefaultConnection_IsNotProduction(string field)
    {
        var line = CodeLines().SingleOrDefault(l => l.StartsWith("public string " + field + " =", StringComparison.Ordinal));
        Assert.NotNull(line);
        var lower = line!.ToLowerInvariant();
        Assert.DoesNotContain("192.168.200.14", lower);
        Assert.DoesNotContain("bravo", lower);
        Assert.Contains("QcSqlOverride.Resolve(", line);
    }

    [Fact]
    public void EveryDefaultConnectionString_GoesThroughQcSqlOverride()
    {
        var offenders = CodeLines()
            .Where(l => l.StartsWith("public string ", StringComparison.Ordinal) && l.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            .Where(l => !l.Contains("QcSqlOverride.Resolve(", StringComparison.Ordinal))
            .ToArray();
        Assert.True(offenders.Length == 0,
            "Connection defaults must use QcSqlOverride.Resolve so CI isolation cannot be bypassed:\n" + string.Join("\n", offenders));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "PCTP", "Modules")) &&
                Directory.Exists(Path.Combine(dir.FullName, "PCTP.QC.Tests")))
                return dir.FullName;
            dir = dir.Parent;
        }
        Assert.Fail("Repository root not found above " + AppContext.BaseDirectory);
        return string.Empty;
    }
}
