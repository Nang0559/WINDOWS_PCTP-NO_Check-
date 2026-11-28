using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 078 (mã bộ phận 4 ký tự / mã chuyền LOT 3 ký tự lấy từ IFS).</summary>
public sealed class WorkCenterLinkCodesFromIfsContractTests
{
    private static string Sql() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "078_MES_WorkCenterLinkCodesFromIfs.sql"));
    private static string Repo() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SiteDepartmentLineRepository.source.cs"));

    [Fact]
    public void Department_code_is_the_work_center_no_padded_to_four_characters()
    {
        var sql = Sql();
        Assert.Contains("RIGHT('0000' + CONVERT(varchar(4), @n), 4)", sql);
        Assert.Contains("LEN(@n) > 4", sql);
        Assert.Contains("n.PadLeft(4, '0')", Repo());
    }

    [Fact]
    public void Lot_line_code_is_generated_from_the_ifs_line_and_remembered()
    {
        var sql = Sql();
        Assert.Contains("IfsProductionLineCode", sql);
        Assert.Contains("UX_FVN_ProductionLine_IfsLine", sql);
        Assert.Contains("MES.usp_FVN_LotLineCode_Resolve", sql);
        Assert.Contains("THROW 57742", sql);
    }

    [Fact]
    public void Link_no_longer_requires_typed_codes_or_names_but_keeps_ifs_checks()
    {
        var sql = Sql();
        Assert.Contains("@DepartmentCode varchar(4) = NULL, @LineCode varchar(3) = NULL", sql);
        Assert.DoesNotContain("LineName is required to create it", sql);
        Assert.Contains("THROW 57700", sql);
        Assert.Contains("THROW 57702", sql);
        Assert.Contains("MigrationVersion = '077'", sql);
        Assert.Contains("MigrationVersion = '078'", sql);
    }
}
