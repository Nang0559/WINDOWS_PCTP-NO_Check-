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
        var line = CodeLines().SingleOrDefault(l => l.StartsWith("public string AutoWH", StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.Contains("SQLPROVIDER.GetConfiguredConnection(\"B7R2_FCCdb\")", line);
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
    public void ExternalDatabaseConfig_DeclaresOneCanonicalConnection()
    {
        var root = FindRepositoryRoot();
        var config = Path.Combine(root, "PCTP", "PCTP.Database.config");
        Assert.True(File.Exists(config), "PCTP.Database.config template is missing.");
        var content = File.ReadAllText(config);
        Assert.Contains("<connectionStrings>", content);
        var document = System.Xml.Linq.XDocument.Parse(content);

        // Exactly two names: the canonical SqlClient string and the XPO alias that the DevExpress
        // report designers bind to (derived from the canonical one at start-up by ReportConnectionAlias).
        var names = document.Root.Elements("add").Select(e => (string)e.Attribute("name")).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "B7R2_FCCdb", "accfast\\bravo.B7R2_FCC.dbo" }.OrderBy(n => n).ToArray(), names);

        var connectionValues = document.Root.Elements("add")
            .Select(e => (string)e.Attribute("connectionString"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
        Assert.Equal(2, connectionValues.Length);
        var values = string.Join("\n", connectionValues).ToLowerInvariant();
        Assert.DoesNotContain("192.168.200.14", values);
        Assert.DoesNotContain("192.168.200.57", values);
        Assert.DoesNotContain("fccbrv", values);
        Assert.DoesNotContain("password=sa", values);
        Assert.Contains("your_sql_server", values);
        Assert.Contains("your_sql_user", values);
        Assert.Contains("your_sql_password", values);
    }

    [Fact]
    public void ApplicationSource_HasNoHardCodedSqlServerHosts()
    {
        var root = FindRepositoryRoot();
        var offenders = Directory.EnumerateFiles(Path.Combine(root, "PCTP"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            // QcSqlOverride is the fail-closed guard that rejects both hosts; ShellSystemInfoRibbon only
            // labels the server SQL Server reports (display text, it never opens a connection).
            .Where(f => !new[] { "QcSqlOverride.cs", "ShellSystemInfoRibbon.cs" }
                .Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => File.ReadAllLines(f)
                .Select(l => l.Trim())
                .Where(l => !l.StartsWith("//", StringComparison.Ordinal))
                .Any(l => l.Contains("192.168.200.14") || l.Contains("192.168.200.57")))
            .Select(f => f.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar))
            .ToArray();
        Assert.True(offenders.Length == 0,
            "SQL server hosts must only come from PCTP.Database.config (B7R2_FCCdb). Found in: " + string.Join(", ", offenders));
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
