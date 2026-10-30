using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Source-contract guards for "Mở lại Lot" (lot đã kết thúc nhưng kế hoạch SX tăng) và báo cáo tồn kho hiện tại.
/// Production code targets .NET Framework 4.7.2 and is linked as text (same approach as
/// AtomicityFindingsSourceContractTests); runtime behavior is covered by the SQL integration scenarios.
/// </summary>
public sealed class ReceivingWorklistReopenContractTests
{
    private static string Read(string fileName) =>
        Regex.Replace(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName)), @"\s+", " ");

    [Fact]
    public void Worklist_KeepsLockedLotsWhoseProductionPlanIncreased()
    {
        var filter = Read("ReceivingWorklistFilter.source.cs");
        Assert.Contains("ISNULL(SL_DA_NHAP,0) < ISNULL(SL_DA_SAN_XUAT,0)", filter, StringComparison.Ordinal);
        Assert.Contains("ISNULL(KET_THUC_LOT,0) = 0", filter, StringComparison.Ordinal);
        Assert.Contains("ISNULL(SLSX_DA_LUU,0) <> ISNULL(SL_DA_SAN_XUAT,0)", filter, StringComparison.Ordinal);
        Assert.Contains("ISNULL(SLSX_DA_LUU,0) > 0", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void Badge_Grid_AndWorklist_ShareTheSameFilter()
    {
        foreach (var file in new[] { "NhapKhoDashboardRepository.source.cs", "StockTpProductionRepository.source.cs" })
            Assert.Contains("ReceivingWorklistFilter.WhereClause", Read(file), StringComparison.Ordinal);

        // No query may re-declare its own copy of the old "chưa kết thúc" condition.
        Assert.DoesNotContain("WHERE ISNULL(KET_THUC_LOT,0) = 0 AND ISNULL(SL_DA_NHAP,0)",
            Read("StockTpProductionRepository.source.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE ISNULL(KET_THUC_LOT,0)=0",
            Read("NhapKhoDashboardRepository.source.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void ReceivingView_ExposesTheSavedProductionQuantityInEveryUnionBranch()
    {
        var view = Read("PCTP_vNhapTP.source.sql");
        Assert.Equal(3, Regex.Matches(view, @"AS SLSX_DA_LUU").Count);
    }

    [Fact]
    public void CurrentStockProcedure_ReportsFullQr_AndUnslottedRemainder()
    {
        var sql = Read("sp_GetCurrentStockStatus.sql");
        Assert.Contains("NULLIF(sl.QrData, N'')", sql, StringComparison.Ordinal);
        Assert.Contains("stp.SLCONLAI - ISNULL(st.SlotQty, 0) > 0", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sl.TemCode AS QrData", sql, StringComparison.Ordinal);
    }
}
