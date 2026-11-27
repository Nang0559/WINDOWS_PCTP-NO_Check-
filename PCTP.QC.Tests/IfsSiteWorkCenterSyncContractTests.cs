using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Source-contract guards cho migration 068 (đồng bộ IFS Site/Work Center + liên kết Site-Line-Department).
/// Đọc text nguồn vì SQL/Oracle không chạy trong project test này.
/// </summary>
public sealed class IfsSiteWorkCenterSyncContractTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
    private static string Migration() => Read("068_IFS_SiteWorkCenterSyncAndProductionLink.sql");

    /// <summary>Nội dung của một procedure được tạo bằng EXEC(N'CREATE PROCEDURE name ...') cho tới END');</summary>
    private static string Proc(string sql, string name)
    {
        var start = sql.IndexOf("CREATE PROCEDURE " + name, StringComparison.Ordinal);
        Assert.True(start >= 0, "Procedure not found: " + name);
        var end = sql.IndexOf("END');", start, StringComparison.Ordinal);
        Assert.True(end > start, "Procedure end not found: " + name);
        return sql.Substring(start, end - start);
    }

    [Fact]
    public void StageEntityConstraint_AllowsSiteAndWorkCenter_AndKeepsExistingEntities()
    {
        var sql = Migration();
        var ck = Regex.Match(sql, @"CK_IFS_Stage_Entity CHECK\s*\((?<body>.*?)\);", RegexOptions.Singleline);
        Assert.True(ck.Success);
        foreach (var e in new[] { "PART_CATALOG", "INVENTORY_PART", "BOM_HEADER", "BOM_ALTERNATIVE", "BOM_COMPONENT", "WAREHOUSE", "SUPPLIER", "SITE", "WORK_CENTER" })
            Assert.Contains("'" + e + "'", ck.Groups["body"].Value);
    }

    [Fact]
    public void Migration_DoesNotModifyTheSharedApplyProcedure()
    {
        var sql = Migration();
        Assert.DoesNotContain("ALTER PROCEDURE IFS.usp_ApplySyncBatch", sql);
        Assert.DoesNotContain("CREATE PROCEDURE IFS.usp_ApplySyncBatch", sql);
        Assert.DoesNotContain("OBJECT_DEFINITION", sql);
    }

    [Fact]
    public void SyncApply_NeverCreatesLinesDepartmentsOrSites()
    {
        // LineCode(3)/DepartmentCode(4) là một phần LOT 20 ký tự: chỉ procedure Link được tạo chúng.
        var apply = Proc(Migration(), "IFS.usp_ApplySiteWorkCenterBatch");
        Assert.DoesNotContain("INSERT dbo.FVN_ProductionLine", apply);
        Assert.DoesNotContain("INSERT dbo.FVN_ProductionDepartment", apply);
        Assert.DoesNotContain("INSERT dbo.FVN_PCTPSite", apply);
        Assert.DoesNotContain("DELETE", apply, StringComparison.OrdinalIgnoreCase);
        // Chỉ được làm mới SiteName của site đã nằm trong allow-list.
        Assert.Contains("UPDATE p SET SiteName", apply);
    }

    [Fact]
    public void SyncApply_IsAtomicAndLocked()
    {
        var apply = Proc(Migration(), "IFS.usp_ApplySiteWorkCenterBatch");
        foreach (var token in new[] { "sp_getapplock", "BEGIN TRANSACTION", "COMMIT TRANSACTION", "XACT_STATE", "THROW", "HAVING COUNT(*) > 1" })
            Assert.Contains(token, apply);
        Assert.Contains("Status IN (''STAGED'', ''APPLIED'')", apply);
    }

    [Fact]
    public void LinkProcedure_ValidatesCodesSiteWorkCenterAndAudits()
    {
        var link = Proc(Migration(), "MES.usp_FVN_SiteDepartmentLine_Link");
        foreach (var token in new[]
        {
            "LEN(ISNULL(@DepartmentCode", "<> 4", "LEN(ISNULL(@LineCode", "<> 3",
            "FVN_PCTPSite", "IFS.FVN_WorkCenter", "IsPresentInIfs = 1", "UPDLOCK",
            "FVN_ProductionLotCodeMap", "FVN_SiteDepartmentLineAudit", "ActorCode"
        })
            Assert.Contains(token, link);
    }

    [Fact]
    public void LotCodeMap_IsBoundToTheDepartmentLinePair()
    {
        var sql = Migration();
        Assert.Contains("UQ_FVN_ProductionDepartment_SiteDeptLine", sql);
        Assert.Contains("FK_FVN_ProductionLotCodeMap_SiteDeptLine", sql);
        Assert.Contains("FOREIGN KEY(SiteCode, DepartmentCode, LotLineCode)", sql);
        Assert.Contains("REFERENCES dbo.FVN_ProductionDepartment(SiteCode, DepartmentCode, LineCode)", sql);
        Assert.Contains("UX_FVN_ProductionDepartment_WorkCenter", sql);
    }

    [Fact]
    public void Migration_IsIdempotentAndGuarded()
    {
        var sql = Migration();
        Assert.Contains("DB_NAME() <> N'FVN_PCTP'", sql);
        Assert.Contains("MigrationVersion = '068'", sql);
        Assert.Contains("RETURN;", sql);
        Assert.Contains("BEGIN TRY", sql);
        Assert.Contains("ROLLBACK TRANSACTION", sql);
    }

    [Fact]
    public void IfsSyncService_StagesSiteAndWorkCenterAndAppliesThemBeforeTheSharedApply()
    {
        var src = Read("IfsSyncService.source.cs");
        Assert.Contains("\"SITE\", \"SELECT * FROM IFSAPP.SITE", src);
        Assert.Contains("\"WORK_CENTER\", \"SELECT * FROM IFSAPP.WORK_CENTER", src);
        Assert.Contains("ReadString(reader, \"WORK_CENTER_NO\")", src);

        var site = src.IndexOf("IFS.usp_ApplySiteWorkCenterBatch", StringComparison.Ordinal);
        var shared = src.IndexOf("\"IFS.usp_ApplySyncBatch\"", StringComparison.Ordinal);
        Assert.True(site > 0 && shared > 0 && site < shared,
            "Site/Work Center apply must run before the shared apply so a failure leaves the batch FAILED, not half APPLIED.");
    }

    [Fact]
    public void LinkRepository_RequiresPermissionScopeAndFixedLengthCodes()
    {
        var src = Read("SiteDepartmentLineRepository.source.cs");
        Assert.Contains("AuthorizationGuard.DemandActionAndScope(FunctionCode, \"EDIT\"", src);
        Assert.Contains("AuthorizationGuard.DemandScope(FunctionCode", src);
        Assert.Contains("dept.Length != 4", src);
        Assert.Contains("line.Length != 3", src);
        Assert.Contains("MES.usp_FVN_SiteDepartmentLine_Link", src);
        Assert.Contains("MES.usp_FVN_SiteDepartmentLine_Unlink", src);
    }
    [Fact]
    public void Migration069_AddsSelectedEntitiesToTheRuntimeBatchTable()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "069_IFS_SyncRuntimeSelectedEntitiesCompatibility.sql"));
        Assert.Contains("ALTER TABLE IFS.SyncBatch ADD SelectedEntities", sql);
        Assert.Contains("COL_LENGTH(N'IFS.SyncBatch', N'SiteRows')", sql);
        Assert.Contains("COL_LENGTH(N'IFS.SyncBatch', N'WorkCenterRows')", sql);
        Assert.Contains("ALTER TABLE IFS.SyncStageRow", sql);
        Assert.Contains("MigrationVersion = '069'", sql);
    }

}
