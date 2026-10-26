using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class ReworkExportIdempotencyContractTests
{
    private static string ReadSource(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        Assert.True(File.Exists(path), "Linked source fixture missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void ReworkExport_UsesCallerOwnedOperationIdAcrossUiServiceAndMovement()
    {
        var ui = ReadSource("FormReworkProcess.source.cs");
        var service = ReadSource("ReworkStockService.source.cs");
        var contract = ReadSource("IReworkStockService.source.cs");
        var qt = ReadSource("QTChungService.source.cs");

        Assert.Contains("_pendingReworkExportOperationId = Guid.NewGuid()", ui);
        Assert.Contains("_pendingReworkExportOperationId.Value", ui);
        Assert.Contains("Guid operationId", contract);
        Assert.Contains("ReferenceId = operationId.ToString(\"D\")", service);
        Assert.Contains("OperationId = operationId", service);
        Assert.Contains("GetXuatByOperationId(operationId)", qt);
        Assert.Contains("QT_REWORK_TICKET:", qt);
        Assert.Contains("@LockOwner = 'Transaction'", qt);
    }


    [Fact]
    public void ReworkExport_EnforcesPhase4QuotaFailClosedBeforeStockMovement()
    {
        var service = ReadSource("ReworkStockService.source.cs");

        var lockRow = service.IndexOf("UpdateStatusIfCurrentIs(", StringComparison.Ordinal);
        var guardProvider = service.IndexOf("_phase4Provider()", StringComparison.Ordinal);
        var failClosed = service.IndexOf("Không cấu hình được Phase 4 Rework guard", StringComparison.Ordinal);
        var quotaGuard = service.IndexOf("phase4.EnsureCanExport(phieuXuLyId, soLuong, nguoiXuat)", StringComparison.Ordinal);
        var movement = service.IndexOf("_stockMovement.Export(", StringComparison.Ordinal);

        Assert.True(lockRow >= 0, "The ticket row must be locked before quota validation.");
        Assert.True(guardProvider > lockRow, "The required Phase 4 guard must be resolved after acquiring the ticket row lock.");
        Assert.True(failClosed > guardProvider, "A missing Phase 4 guard must reject the export.");
        Assert.True(quotaGuard > failClosed, "The approved Rework quantity must be validated before movement.");
        Assert.True(movement > quotaGuard, "No stock movement may happen before the quota guard succeeds.");
    }

    [Fact]
    public void ReworkDelivery_LocksAndReadsLedgersInsideTransactionBeforeInsert()
    {
        var service = ReadSource("QTChungService.source.cs");
        var methodStart = service.IndexOf("public ScanResult GiaoHangRework(", StringComparison.Ordinal);
        var methodEnd = service.IndexOf("public void GhiNhanDangRework(", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart, "Rework delivery method boundaries must exist.");

        var method = service.Substring(methodStart, methodEnd - methodStart);
        var begin = method.IndexOf("_uow.Begin()", StringComparison.Ordinal);
        var appLock = method.IndexOf("QT_REWORK_TICKET:", StringComparison.Ordinal);
        var exportRead = method.IndexOf("_traHangQTChungRepository.GetXuat(", StringComparison.Ordinal);
        var deliveryRead = method.IndexOf("_traHangQTChungRepository.GetGiao(", StringComparison.Ordinal);
        var insert = method.IndexOf("_traHangQTChungRepository.InsertGiao(", StringComparison.Ordinal);
        var commit = method.IndexOf("_uow.Commit()", StringComparison.Ordinal);

        Assert.True(begin >= 0 && appLock > begin, "Transaction and ticket application lock must start before ledger reads.");
        Assert.True(exportRead > appLock && deliveryRead > appLock, "Both export and delivery totals must be read after serialization.");
        Assert.True(insert > deliveryRead && commit > insert, "Delivery ledger insert must remain in the locked transaction.");
        Assert.Contains("@LockOwner = 'Transaction'", method);
    }

    [Fact]
    public void ReworkExportRepository_PersistsAndReadsOperationId()
    {
        var repository = ReadSource("TraHangQTChungRepository.source.cs");

        Assert.Contains("OperationId,", repository);
        Assert.Contains("@OperationId", repository);
        Assert.Contains("GetXuatByOperationId(Guid operationId)", repository);
        Assert.Contains("WHERE OperationId = @OperationId", repository);
        Assert.Contains("OperationId = row[\"OperationId\"] == DBNull.Value", repository);
    }

    [Fact]
    public void ReworkExportDeliveryAndQc_ShareOneTicketLockResource()
    {
        var qt = ReadSource("QTChungService.source.cs");
        var qc = ReadSource("IQTChungService.source.cs");

        Assert.DoesNotContain("QT_REWORK_EXPORT:", qt);
        Assert.DoesNotContain("QT_REWORK_DELIVERY:", qt);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(qt, "\"QT_REWORK_TICKET:\"").Count);
        Assert.Contains("\"QT_REWORK_TICKET:\" + phieuXuLyId", qc);
    }

    [Fact]
    public void ReworkQcAndDisposition_TakeTicketLockInsideTransactionBeforeInsert()
    {
        var qc = ReadSource("IQTChungService.source.cs");
        foreach (var name in new[] { "ConfirmReworkQC", "ConfirmDisposition" })
        {
            var start = qc.IndexOf("public " + (name == "ConfirmReworkQC" ? "ReworkQCResult " : "DispositionResult ") + name, StringComparison.Ordinal);
            Assert.True(start >= 0, name + " implementation not found.");
            var insertToken = name == "ConfirmReworkQC" ? "INSERT INTO dbo.FVN_PXLB_ReworkQC" : "INSERT INTO dbo.FVN_PXLB_Disposition";
            var begin = qc.IndexOf("Uow.Begin()", start, StringComparison.Ordinal);
            var lockCall = qc.IndexOf("AcquireTicketLock(", begin, StringComparison.Ordinal);
            var insert = qc.IndexOf(insertToken, begin, StringComparison.Ordinal);
            Assert.True(begin > start && lockCall > begin && insert > lockCall,
                name + " must begin the transaction and take the ticket lock before the insert.");
        }
    }
}
