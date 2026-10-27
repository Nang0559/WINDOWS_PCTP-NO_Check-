using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class LegacyDeliverySaveProcedureRetirementTests
{
    private static string ReadSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Usp_Qrcode_LUU_PHIEU_RetireUnsafeLegacy.sql");
        Assert.True(File.Exists(path), "Linked SQL fixture missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void RetirementScript_IsPreflightOnlyByDefault_AndBlocksProduction()
    {
        var sql = ReadSource();

        Assert.Contains("DECLARE @ConfirmRetire bit = 0", sql);
        Assert.Contains("IF @ConfirmRetire = 0", sql);
        Assert.Contains("SERVERPROPERTY('ServerName')", sql);
        Assert.Contains("LIKE N'%BRAVO%'", sql);
        Assert.Contains("DB_NAME() <> N'B7R2_FCC'", sql);
        Assert.Contains("CONNECTIONPROPERTY('local_net_address')", sql);
        Assert.Contains("@SqlServerIp <> N'192.168.200.57'", sql);
        Assert.Contains("PRECHECK ONLY", sql);
    }

    [Fact]
    public void RetirementScript_RefusesToDisableProcedureWhileSqlCallersRemain()
    {
        var sql = ReadSource();

        Assert.Contains("m.definition LIKE N'%Usp_Qrcode_LUU_PHIEU%'", sql);
        Assert.Contains("st.command LIKE N'%Usp_Qrcode_LUU_PHIEU%'", sql);
        Assert.Contains("Van con module SQL tham chieu", sql);
        Assert.Contains("Van con SQL Agent job step tham chieu", sql);
    }


    [Fact]
    public void ProductionSourceAndSqlDoNotCallTheUnsafeLegacySaveProcedure()
    {
        var root = FindRepositoryRoot();
        var roots = new[] { "PCTP", "WINDOWS_PCTP", "APP LOTNO", "SQL" }
            .Select(name => Path.Combine(root, name))
            .Where(Directory.Exists)
            .ToArray();
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".sql", ".config", ".settings", ".json", ".xml", ".ps1", ".xaml", ".csproj", ".vb"
        };

        var matches = roots
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            .Where(file => extensions.Contains(Path.GetExtension(file)))
            .Where(file => !string.Equals(Path.GetFileName(file), "Usp_Qrcode_LUU_PHIEU_RetireUnsafeLegacy.sql", StringComparison.OrdinalIgnoreCase))
            .Where(file => File.ReadAllText(file).Contains("Usp_Qrcode_LUU_PHIEU", StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(root, file))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(matches.Length == 0,
            "Unsafe legacy procedure is referenced by production source/config/SQL: " + string.Join(", ", matches));
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

        throw new DirectoryNotFoundException("Repository root not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void RetiredProcedure_FailsClosedInsteadOfExecutingLegacyWrites()
    {
        var sql = ReadSource();

        Assert.Contains("ALTER PROCEDURE dbo.Usp_Qrcode_LUU_PHIEU", sql);
        Assert.Contains("THROW 51015", sql);
        Assert.DoesNotContain("UPDATE LUUPHIEUGIAOHANG SET LOT=", sql);
        Assert.DoesNotContain("DELETE FROM DOCQRCODE WHERE KETQUA", sql);
    }
}
