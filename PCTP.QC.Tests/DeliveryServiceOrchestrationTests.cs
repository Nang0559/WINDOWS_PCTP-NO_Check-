using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Source-contract regression guards for delivery-service FIFO orchestration.
/// These complement, but do not replace, SQL Server and WinForms end-to-end tests.
/// </summary>
public sealed class DeliveryServiceOrchestrationTests
{
    private static string Source()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "PhieuKhoService.source.cs");
        Assert.True(File.Exists(path), "Linked PhieuKhoService source fixture missing: " + path);
        return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
    }

    [Fact]
    public void Delivery_FifoMustBeEvaluatedBeforeAnyStockUpdateRoute()
    {
        var source = Source();
        var firstEvaluation = source.IndexOf("EvaluateFifoViolations(tmpTable, docQrTable)", StringComparison.Ordinal);
        var updateRoute = source.IndexOf("_phieuRepo.CapNhapKho(", StringComparison.Ordinal);
        var htnRoute = source.IndexOf("_phieuRepo.CapNhapKhoHTN(", StringComparison.Ordinal);

        Assert.True(firstEvaluation >= 0 && updateRoute > firstEvaluation && htnRoute > firstEvaluation,
            "FIFO evaluation must precede both stock-update routes.");
    }

    [Fact]
    public void Delivery_FifoDecisionMustFailClosedAndReevaluateAfterRelease()
    {
        var source = Source();
        var synchronousPublish = source.IndexOf("_bus.PublishSynchronous(confirmation)", StringComparison.Ordinal);
        var decision = source.IndexOf("if (!confirmation.IsCompleted || !confirmation.WaitForDecision())", StringComparison.Ordinal);
        var release = source.IndexOf("_phieuRepo.ReleaseFifoViolations", StringComparison.Ordinal);
        var reevaluation = source.IndexOf("remainingViolations", release, StringComparison.Ordinal);
        var stockUpdate = source.IndexOf("_phieuRepo.CapNhapKho(", StringComparison.Ordinal);

        Assert.True(synchronousPublish >= 0 && decision > synchronousPublish &&
                    release > decision && reevaluation > release && stockUpdate > reevaluation,
            "FIFO release must require a completed positive decision, release only after approval, re-evaluate, then allow stock update.");
        Assert.Contains("if (remainingViolations.Count > 0)", source);
        Assert.Contains("if (!confirmation.IsCompleted || !confirmation.WaitForDecision())", source);
    }

    [Fact]
    public void Delivery_MustNotPublishStockUpdatedEventBeforeRepositoryReturns()
    {
        var source = Source();
        var stockUpdate = source.IndexOf("_phieuRepo.CapNhapKho(", StringComparison.Ordinal);
        var publish = source.IndexOf("_bus.Publish(new KhoUpdatedEvent(soLot, errors))", StringComparison.Ordinal);

        Assert.True(stockUpdate >= 0 && publish > stockUpdate,
            "Stock-updated notification must be published only after the stock repository route completes.");
    }
}
