using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class DeliveryQrAllocationSourceContractTests
{
    private static string Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name);
        Assert.True(File.Exists(path), "Missing production source fixture: " + path);
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    [Fact]
    public void SqlAllocator_MustSearchExactSubsetsInsteadOfGreedyAccumulation()
    {
        var sql = Read("Usp_Qrcode_Take_Lot2405.sql");
        Assert.Contains("CREATE TABLE #DP", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE #DPPrev", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p.Qty+@qrQty<=@_SLGIAO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE Qty=@_SLGIAO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY STT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET @SLTT = @SLTT + @_SLTEMFCC", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlAllocator_MustMarkOnlyChosenQrRowsAndRollbackPartialWrites()
    {
        var sql = Read("Usp_Qrcode_Take_Lot2405.sql");
        Assert.Contains("JOIN #Chosen c ON c.STT=q.STT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @affected<>@chosenCount", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @affected<>1 THROW", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SAVE TRANSACTION PctpTakeLot2405", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROLLBACK TRANSACTION PctpTakeLot2405", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ISNULL(KETQUA,N'''')<>N''DG''", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repository_MustPersistLotInSameUnitOfWorkAsQrClaim()
    {
        var source = Read("PhieuLotRepository.source.cs");
        var transaction = source.IndexOf("return InTransaction(() =>", StringComparison.Ordinal);
        var procedure = source.IndexOf("ExecuteStoredProcedure(", transaction, StringComparison.Ordinal);
        var lotUpdate = source.IndexOf("UPDATE [{tmpTable}] SET LOT = @lot", transaction, StringComparison.Ordinal);
        Assert.True(transaction >= 0 && procedure > transaction && lotUpdate > procedure,
            "QR claim and selected TMP LOT write must run inside one repository transaction.");
        Assert.Contains("if (updated != 1)", source);
    }

    [Fact]
    public void LotService_MustProcessEachDuplicateGroupOnceAndValidateSelectedStt()
    {
        var source = Read("PhieuLotService.source.cs");
        Assert.Contains("processedGroups", source);
        Assert.Contains("if (!processedGroups.Add(groupKey))", source);
        Assert.Contains("while (pending.Rows.Count > 0)", source);
        Assert.Contains("if (pending.Rows.Count > 1)", source);
        Assert.Contains("FindByStt(pending, selectedStt)", source);
        Assert.DoesNotContain("_lotRepo.CapNhapLotTmpPhieu(stt", source);
    }

    [Fact]
    public void DuplicateDialog_MustNotUseStaticSelectionState()
    {
        var form = Read("FRM_LISTRUNGMSL.source.cs");
        var dialog = Read("PhieuDialogControl.source.cs");
        Assert.Contains("public int? SelectedStt", form);
        Assert.Contains("DialogResult = DialogResult.OK", form);
        Assert.DoesNotContain("static string MAHANG", form);
        Assert.DoesNotContain("FRM_LISTRUNGMSL.STTPHIEU", dialog);
        Assert.Contains("form.SelectedStt", dialog);
    }

    [Fact]
    public void DuplicateBusinessKey_MustStillContainBothPartAndQuantityFilters()
    {
        var source = Read("PhieuValidationRepository.source.cs");
        Assert.Contains("WHERE MAHANG = @ma AND SOLUONG = @sl", source);
    }
    [Fact]
    public void CustomerRouting_KeepsYmvNOnItsDedicatedConfiguredProcedureAndTables()
    {
        var config = Read("CustomerTableConfig.source.cs");
        var ymvn = Read("PhieuYmvnService.source.cs");
        var hvnProcedure = Read("Usp_Qrcode_Take_Lot2405.sql");
        var ymvnProcedure = Read("Usp_Qrcode_Take_LotYMVN2405.sql");

        Assert.Contains("DocQRTable = \"YMVN_DOCQRCODE\"", config);
        Assert.Contains("DocQRTableSP = \"SP_DOCQRCODE\"", config);
        Assert.Contains("TmpTableSP = \"SP_TMPPHIEUGIAOHANG\"", config);
        Assert.Contains("_phieuRepo.TakeLotYMVN(", ymvn);
        Assert.Contains("_cfg.Delivery.TmpTable", ymvn);
        Assert.Contains("_cfg.Delivery.DocQRTable", ymvn);
        Assert.Contains("isLoaiSP ? _cfg.Delivery.TmpTableSP : _cfg.Delivery.TmpTable", ymvn);
        Assert.Contains("isLoaiSP ? _cfg.Delivery.DocQRTableSP : _cfg.Delivery.DocQRTable", ymvn);
        Assert.Contains("N'SP_TMPPHIEUGIAOHANG'", ymvnProcedure);
        Assert.Contains("N'SP_DOCQRCODE'", ymvnProcedure);
        Assert.Contains("@ISLOAISP = 1", ymvnProcedure);
        Assert.DoesNotContain("YMVN_DOCQRCODE", hvnProcedure);
        Assert.DoesNotContain("SP_DOCQRCODE", hvnProcedure);
    }

}
