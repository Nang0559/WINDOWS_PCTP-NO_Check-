using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 076 (đồng nhất Site / Line / Department giữa schema IFS, dbo, MES).</summary>
public sealed class MasterDataUnificationContractTests
{
    private static string Migration() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "076_MES_MasterDataUnification.sql"));

    [Fact]
    public void Migration_NeverDropsOrDeletesData()
    {
        var sql = Migration();
        Assert.False(Regex.IsMatch(sql, @"DROP\s+TABLE", RegexOptions.IgnoreCase));
        Assert.False(Regex.IsMatch(sql, @"\bDELETE\b", RegexOptions.IgnoreCase));
        Assert.False(Regex.IsMatch(sql, @"TRUNCATE", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Migration_DoesNotNeedCreateOrAlter_AndIsRerunnable()
    {
        var sql = Migration();
        Assert.DoesNotContain("CREATE OR ALTER", sql);
        Assert.Contains("VALUES ('076'", sql);
        Assert.True(Regex.IsMatch(sql, @"MigrationVersion = '076'\)\s+RETURN;"));
    }

    [Fact]
    public void ReconcileProcedure_IsPreviewByDefault_AndRequiresActorToWrite()
    {
        var sql = Migration();
        Assert.Contains("@Apply bit = 0", sql);
        Assert.Contains("@DeactivateStaleMappings bit = 0", sql);
        Assert.Contains("THROW 57610", sql);
    }

    [Fact]
    public void BridgeToIfsLine_IsAnUntrustedForeignKey()
    {
        var sql = Migration();
        Assert.True(Regex.IsMatch(sql,
            @"WITH NOCHECK\s+ADD CONSTRAINT FK_FVN_ProductionLotCodeMap_IfsLine.*?REFERENCES IFS\.FVN_ProductionLine",
            RegexOptions.Singleline));
    }

    [Fact]
    public void SiteNameBackfill_NeverOverwritesAnExistingName()
    {
        Assert.Contains("NULLIF(LTRIM(RTRIM(p.SiteName)), N'') IS NULL", Migration());
    }
}
