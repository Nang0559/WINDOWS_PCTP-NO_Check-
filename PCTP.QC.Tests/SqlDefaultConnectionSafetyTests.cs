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
        Assert.Contains("GetConfiguredConnection(\"" + field + "\")", line);
    }

    [Fact]
    public void ConfiguredConnections_GoThroughQcSqlOverride()
    {
        var lines = CodeLines();
        Assert.Contains(lines, l => l.Contains("return QcSqlOverride.Resolve(configured);", StringComparison.Ordinal));
    }

    [Fact]
    public void ExternalDatabaseConfig_ContainsBothLegacyKeys()
    {
        var root = FindRepositoryRoot();
        var config = Path.Combine(root, "PCTP", "PCTP.Database.config");
        Assert.True(File.Exists(config), "PCTP.Database.config template is missing.");
        var content = File.ReadAllText(config);
        Assert.Contains("B7R2_FCCdb", content);
        Assert.Contains("B7R2_FCCdbb", content);
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
