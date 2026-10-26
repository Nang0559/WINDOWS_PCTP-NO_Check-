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
        Assert.Contains("QT_REWORK_EXPORT:", qt);
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
    public void ReworkExportRepository_PersistsAndReadsOperationId()
    {
        var repository = ReadSource("TraHangQTChungRepository.source.cs");

        Assert.Contains("OperationId,", repository);
        Assert.Contains("@OperationId", repository);
        Assert.Contains("GetXuatByOperationId(Guid operationId)", repository);
        Assert.Contains("WHERE OperationId = @OperationId", repository);
        Assert.Contains("OperationId = row[\"OperationId\"] == DBNull.Value", repository);
    }
}
