using System;
using System.IO;
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
    public void RetiredProcedure_FailsClosedInsteadOfExecutingLegacyWrites()
    {
        var sql = ReadSource();

        Assert.Contains("ALTER PROCEDURE dbo.Usp_Qrcode_LUU_PHIEU", sql);
        Assert.Contains("THROW 51015", sql);
        Assert.DoesNotContain("UPDATE LUUPHIEUGIAOHANG SET LOT=", sql);
        Assert.DoesNotContain("DELETE FROM DOCQRCODE WHERE KETQUA", sql);
    }
}
