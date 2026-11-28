using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 081 (chuỗi dữ liệu gốc chặt: mọi mã LOT chỉ chọn từ danh sách đã khai báo, không nhập tay / không tự sinh ngầm).</summary>
public sealed class StrictMasterDataChainContractTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
    private static string Migration() => Read("081_MES_StrictMasterDataChain.sql");

    [Fact]
    public void Migration_follows_080_and_runs_without_create_or_alter()
    {
        var sql = Migration();
        Assert.Contains("MigrationVersion = '080'", sql);
        Assert.Contains("VALUES ('081'", sql);
        Assert.DoesNotContain("CREATE OR ALTER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Status_views_list_every_ifs_work_center_and_line_even_without_a_code()
    {
        var sql = Migration();
        Assert.Contains("ALTER VIEW MES.vw_FVN_WorkCenterCodeStatus", sql);
        Assert.Contains("FROM IFS.FVN_WorkCenter AS w", sql);
        Assert.Contains("LEFT JOIN MES.FVN_DepartmentCodeCrossRef AS x", sql);
        Assert.Contains("'NO_CODE'", sql);
        Assert.Contains("ALTER VIEW MES.vw_FVN_IfsLineCodeStatus", sql);
        Assert.Contains("FROM IFS.FVN_ProductionLine AS l", sql);
        Assert.Contains("LEFT JOIN MES.FVN_LineCodeCrossRef AS x", sql);
    }

    [Fact]
    public void Map_save_only_chooses_existing_master_rows_and_no_longer_creates_them()
    {
        var sql = Migration();
        var save = sql.Substring(sql.IndexOf("ALTER PROCEDURE MES.usp_FVN_ProductionLotCodeMap_Save", StringComparison.Ordinal));
        save = save.Substring(0, save.IndexOf("\nGO", StringComparison.Ordinal));
        Assert.Contains("THROW 57820", save);
        Assert.Contains("THROW 57821", save);
        Assert.Contains("IfsProductionLineCode = @PlanProductionLineCode", save);
        Assert.Contains("WorkCenterNo IS NOT NULL", save);
        Assert.DoesNotContain("INSERT dbo.FVN_ProductionLine", save);
        Assert.DoesNotContain("INSERT dbo.FVN_ProductionDepartment", save);
        Assert.Contains("THROW 57511", save); // IFS site hardening of 075 is kept
    }

    [Fact]
    public void Link_takes_codes_only_from_the_cross_references()
    {
        var sql = Migration();
        var link = sql.Substring(sql.IndexOf("ALTER PROCEDURE MES.usp_FVN_SiteDepartmentLine_Link", StringComparison.Ordinal));
        Assert.Contains("THROW 57822", link); // no department code declared for the work center
        Assert.Contains("THROW 57823", link); // no LOT line code declared for the IFS line
        Assert.Contains("THROW 57824", link); // caller code differs from the declared one
        Assert.Contains("FROM MES.FVN_LineCodeCrossRef", link);
        Assert.DoesNotContain("usp_FVN_LotLineCode_Resolve", link);
        Assert.DoesNotContain("fn_FVN_DepartmentCodeFromWorkCenter", link);
        Assert.DoesNotContain("INSERT MES.FVN_DepartmentCodeCrossRef", link);
    }

    [Fact]
    public void Line_setup_dialog_has_no_free_text_code_fields()
    {
        var src = Read("FormProductionLineSetup.source.cs");
        var dialog = src.Substring(src.IndexOf("class EditDialog", StringComparison.Ordinal));
        Assert.DoesNotContain("TextEdit _lot", dialog);
        Assert.DoesNotContain("TextEdit _dept", dialog);
        Assert.Contains("ComboBoxEdit _lot", dialog);
        Assert.Contains("ComboBoxEdit _dept", dialog);
        Assert.Contains("ListLotLineChoices", dialog);
        Assert.Contains("ListDepartmentChoices", dialog);
        Assert.Contains("FormWorkCenterDepartmentLink", dialog); // missing master data opens the right screen
        Assert.Contains("FormLineCodeCrossRef", dialog);
        Assert.Contains("FormDepartmentCodeCrossRef", dialog);
    }

    [Fact]
    public void Work_center_link_dialog_reads_declared_codes_and_has_no_typed_department()
    {
        var src = Read("FormWorkCenterDepartmentLink.source.cs");
        var dialog = src.Substring(src.IndexOf("class LinkDialog", StringComparison.Ordinal));
        Assert.Contains("GetDeclaredDepartmentCode", dialog);
        Assert.Contains("GetDeclaredLotLineCode", dialog);
        Assert.DoesNotContain("GenerateDepartmentCode", dialog);
        Assert.DoesNotContain("SuggestLotLineCode", dialog);
        Assert.Contains("_dept.Properties.ReadOnly = true", dialog);
    }

    [Fact]
    public void Cross_reference_screens_list_ifs_records_not_only_the_cross_reference_tables()
    {
        Assert.Contains("GetWorkCenterCodeStatus", Read("FormDepartmentCodeCrossRef.source.cs"));
        Assert.Contains("GetIfsLineCodeStatus", Read("FormLineCodeCrossRef.source.cs"));
        var repo = Read("SiteDepartmentLineRepository.source.cs");
        Assert.Contains("MES.vw_FVN_WorkCenterCodeStatus", repo);
        Assert.Contains("MES.vw_FVN_IfsLineCodeStatus", repo);
    }
}
