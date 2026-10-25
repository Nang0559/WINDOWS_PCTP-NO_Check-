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
