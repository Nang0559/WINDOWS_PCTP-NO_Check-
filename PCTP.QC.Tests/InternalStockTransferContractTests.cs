using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class InternalStockTransferContractTests
{
    private static string Read(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));

    [Fact]
    public void Transfer_UsesOneTransactionToReduceSlotAndStockAndIncreaseInternalBalance()
    {
        var source = Read("InternalTransferRepository.source.cs");
        var start = source.IndexOf("public TransferResult ExecuteTransfer(", StringComparison.Ordinal);
        var end = source.IndexOf("public DataTable GetInternalStock(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source.Substring(start, end - start);

        var begin = method.IndexOf("uow.Begin()", StringComparison.Ordinal);
        var export = method.IndexOf("movement.Export(", StringComparison.Ordinal);
        var internalIncrease = method.IndexOf("IncreaseDestinationStock(", StringComparison.Ordinal);
        var history = method.IndexOf("history.SaveHistory(", StringComparison.Ordinal);
        var commit = method.IndexOf("uow.Commit()", StringComparison.Ordinal);
        Assert.True(begin >= 0 && begin < export && export < internalIncrease && internalIncrease < history && history < commit);
        Assert.Contains("ReferenceType = \"INTERNAL_TRANSFER\"", method, StringComparison.Ordinal);
        Assert.Contains("uow.Rollback()", method, StringComparison.Ordinal);
        Assert.DoesNotContain("PhieuGiao", method, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Recall_ConsumesInternalBalanceAndReceivesIntoSlotAndStockInSameTransaction()
    {
        var source = Read("InternalTransferRecallRepository.source.cs");
        var start = source.IndexOf("public ReturnResult ExecuteReturn(", StringComparison.Ordinal);
        var end = source.IndexOf("private void RefreshSlotHeader(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source.Substring(start, end - start);

        var begin = method.IndexOf("uow.Begin()", StringComparison.Ordinal);
        var lockTransfer = method.IndexOf("PCTP_InternalTransfer WITH (UPDLOCK, HOLDLOCK)", StringComparison.Ordinal);
        var consumeInternal = method.IndexOf("UPDATE PCTP_InternalStock", StringComparison.Ordinal);
        var receive = method.IndexOf("movement.Receive(", StringComparison.Ordinal);
        var history = method.IndexOf("history.SaveHistory(", StringComparison.Ordinal);
        var commit = method.IndexOf("uow.Commit()", StringComparison.Ordinal);
        Assert.True(begin >= 0 && begin < lockTransfer && lockTransfer < consumeInternal &&
                    consumeInternal < receive && receive < history && history < commit);
        Assert.Contains("ReferenceType = \"INTERNAL_RETURN\"", method, StringComparison.Ordinal);
        Assert.Contains("quantity > transferred - returned", method, StringComparison.Ordinal);
        Assert.Contains("slotService.LockSlotForUpdate(targetSlotId)", method, StringComparison.Ordinal);
        Assert.Contains("uow.Rollback()", method, StringComparison.Ordinal);
    }

    [Fact]
    public void NewForms_UseDevExpressControlsAndDoNotIntroduceWinFormsDataGridView()
    {
        var transfer = Read("FormChuyenHangNoiBo.source.cs");
        var recall = Read("FormThuHoiHangNoiBo.source.cs");
        foreach (var form in new[] { transfer, recall })
        {
            Assert.Contains("XtraForm", form, StringComparison.Ordinal);
            Assert.Contains("GridControl", form, StringComparison.Ordinal);
            Assert.Contains("GridView", form, StringComparison.Ordinal);
            Assert.Contains("SimpleButton", form, StringComparison.Ordinal);
            Assert.DoesNotContain("DataGridView", form, StringComparison.Ordinal);
        }
        Assert.Contains("LookUpEdit", recall, StringComparison.Ordinal);
        Assert.Contains("SpinEdit", recall, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_ExposesTransferAndRecallUnderDedicatedMenuGroup()
    {
        var menu = Read("Main_APP.Menu.internal-transfer.source.cs");
        Assert.Contains("Text = \"Điều chuyển nội bộ\"", menu, StringComparison.Ordinal);
        Assert.Contains("menuChuyenHangNoiBo", menu, StringComparison.Ordinal);
        Assert.Contains("menuThuHoiHangNoiBo", menu, StringComparison.Ordinal);
        Assert.Contains("OpenThuHoiHangNoiBo", menu, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_CreatesRecallLedgerWithUniqueRequestAndOriginalTransferReference()
    {
        var sql = Read("PCTP_InternalTransfer.source.sql");
        Assert.Contains("CREATE TABLE dbo.PCTP_InternalReturn", sql, StringComparison.Ordinal);
        Assert.Contains("RequestKey uniqueidentifier NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("UX_PCTP_InternalReturn_RequestKey", sql, StringComparison.Ordinal);
        Assert.Contains("FK_PCTP_InternalReturn_Transfer", sql, StringComparison.Ordinal);
        Assert.Contains("TargetSlotId int NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("Quantity int NOT NULL", sql, StringComparison.Ordinal);
    }
}
