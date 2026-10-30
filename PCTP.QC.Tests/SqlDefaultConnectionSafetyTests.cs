using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Source/config guard: SQLPROVIDER reads its connections from the external config file,
/// and every configured connection must pass through QcSqlOverride so isolated CI redirection
/// remains fail-closed.
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
    public void ConnectionField_UsesExternalConfig(string field)
    {
        var line = CodeLines().SingleOrDefault(l => l.StartsWith("public string " + field + " =", StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.Contains("GetConfiguredConnection(\"" + field + "\")", line);
    }

    [Fact]
    public void AutoWhAlias_UsesTheSharedExternalDatabaseSetting()
    {
        var line = CodeLines().SingleOrDefault(l => l.StartsWith("public string AutoWH =", StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.Contains("GetConfiguredConnection(\"B7R2_FCCdb\")", line);
    }

    [Fact]
    public void MainAppConfig_ExternalizesConnectionStrings()
    {
        var root = FindRepositoryRoot();
        var appConfig = File.ReadAllText(Path.Combine(root, "PCTP", "App.config"));
        Assert.Contains("<connectionStrings configSource=\"PCTP.Database.config\" />", appConfig);
        Assert.DoesNotContain("<add name=\"B7R2_FCCEntities", appConfig);
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
        Assert.Contains("<connectionStrings>", content);
        Assert.Contains("B7R2_FCCdb", content);
        Assert.Contains("B7R2_FCCEntities", content);
        Assert.Contains("localhost_B7R2_FCC_Connection", content);
        Assert.Contains("B7R2_FCCdbb", content);
        var lower = content.ToLowerInvariant();
        Assert.DoesNotContain("192.168.200.14", lower);
        Assert.DoesNotContain("bravo", lower);
        Assert.DoesNotContain("fccbrv", lower);
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
