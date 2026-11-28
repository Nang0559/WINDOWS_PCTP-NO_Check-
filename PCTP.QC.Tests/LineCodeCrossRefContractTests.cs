using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 079 / 079b (mã chuyền 5 ký tự tham chiếu ↔ 3 ký tự dùng trong LOT).</summary>
public sealed class LineCodeCrossRefContractTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
    private static string Migration() => Read("079_MES_LineCodeCrossRef5To3.sql");
    private static string Seed() => Read("079b_MES_LineCodeCrossRefSeed.sql");

    [Fact]
    public void Both_codes_are_unique_per_site_and_lot_keeps_three_characters()
    {
        var sql = Migration();
        Assert.Contains("CONSTRAINT PK_FVN_LineCodeCrossRef PRIMARY KEY (SiteCode, LineCode5)", sql);
        Assert.Contains("CONSTRAINT UQ_FVN_LineCodeCrossRef_3 UNIQUE (SiteCode, LineCode3)", sql);
        Assert.Contains("CHECK (LEN(LineCode5) = 5)", sql);
        Assert.Contains("CHECK (LEN(LineCode3) = 3)", sql);
        Assert.DoesNotContain("ALTER TABLE dbo.FVN_ProductionLine ALTER COLUMN", sql);
    }

    [Fact]
    public void Generator_follows_the_five_character_standard_and_previews_without_writing()
    {
        var sql = Migration();
        Assert.Contains("SET @Width = 4 - LEN(@FamilyCode)", sql);
        Assert.Contains("SET @Code5 = @Prefix + @Body", sql);
        Assert.Contains("@Seq > 35", sql);
        Assert.Contains("IF @Preview = 0", sql);
        Assert.Contains("THROW 57760", sql);
        Assert.Contains("THROW 57762", sql);
    }

    [Fact]
    public void Work_center_link_uses_the_cross_reference_first()
    {
        var sql = Migration();
        var resolve = sql.Substring(sql.IndexOf("CREATE OR ALTER PROCEDURE MES.usp_FVN_LotLineCode_Resolve", StringComparison.Ordinal));
        var xref = resolve.IndexOf("FROM MES.FVN_LineCodeCrossRef", StringComparison.Ordinal);
        var tied = resolve.IndexOf("FROM dbo.FVN_ProductionLine", StringComparison.Ordinal);
        Assert.True(xref > 0 && tied > xref, "Cross reference must be consulted before dbo.FVN_ProductionLine.");
        Assert.Contains("MigrationVersion = '078'", sql);
        Assert.Contains("MigrationVersion = '079'", sql);
    }

    [Fact]
    public void Seed_holds_every_existing_code_once_with_no_three_character_clash()
    {
        var rows = Regex.Matches(Seed(), @"\(N'(?<site>[^']+)','(?<c5>[^']{5})','(?<c3>[^']{3})'")
            .Select(m => (Site: m.Groups["site"].Value, C5: m.Groups["c5"].Value, C3: m.Groups["c3"].Value)).ToList();
        Assert.Equal(140, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => (r.Site, r.C5)).Distinct().Count());
        Assert.Equal(rows.Count, rows.Select(r => (r.Site, r.C3)).Distinct().Count());
        Assert.Equal(84, rows.Count(r => r.Site == "VN2W"));
        Assert.Equal(56, rows.Count(r => r.Site == "VN4W"));
    }
}
