using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Services;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class ReworkQuantityPolicyTests
{
    // approved A, exported X, delivered D, requested q

    [Fact]
    public void Export_NotDelivered_WithinRemaining_IsAllowed()
    {
        var d = ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaDinhHuong, true, 10, 0, 0, 10);
        Assert.True(d.Allowed);
    }

    [Fact]
    public void Export_PartiallyDelivered_AllowsOnlyUpToApprovedMinusExported()
    {
        Assert.True(ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaXuatKhoRework, true, 10, 6, 4, 4).Allowed);
        Assert.False(ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaXuatKhoRework, true, 10, 6, 4, 5).Allowed);
    }

    [Fact]
    public void Export_CapIsApprovedMinusExported_NotApprovedMinusDelivered()
    {
        // A-D = 10 would wrongly allow another 10; the cap must be A-X = 0.
        var d = ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaXuatKhoRework, true, 10, 10, 0, 1);
        Assert.False(d.Allowed);
    }

    [Theory]
    [InlineData(QTChungStatus.DaGiaoSanXuat)]
    [InlineData(QTChungStatus.DaQCXacNhanCuoi)]
    [InlineData(QTChungStatus.HoanTat)]
    [InlineData(QTChungStatus.Huy)]
    [InlineData(QTChungStatus.TuChoiGiaoBu)]
    public void Export_ClosedCancelledOrLaterStatus_IsRejected(QTChungStatus status)
    {
        Assert.False(ReworkQuantityPolicy.EvaluateExport(status, true, 10, 5, 0, 1).Allowed);
    }

    [Fact]
    public void Export_AfterFullDelivery_IsRejectedEvenIfStatusIsStale()
    {
        var d = ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaXuatKhoRework, true, 10, 10, 10, 1);
        Assert.False(d.Allowed);
    }

    [Fact]
    public void Export_RejectsNonReworkNonPositiveAndZeroApproved()
    {
        Assert.False(ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaDinhHuong, false, 10, 0, 0, 1).Allowed);
        Assert.False(ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaDinhHuong, true, 10, 0, 0, 0).Allowed);
        Assert.False(ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaDinhHuong, true, 0, 0, 0, 1).Allowed);
    }

    [Fact]
    public void Delivery_Partial_IsAllowedAndDoesNotCompleteTicket()
    {
        var d = ReworkQuantityPolicy.EvaluateDelivery(QTChungStatus.DaXuatKhoRework, true, 10, 6, 0, 6);
        Assert.True(d.Allowed);
        Assert.False(d.CompletesDelivery);
    }

    [Fact]
    public void Delivery_ReachingApproved_CompletesTicket()
    {
        var d = ReworkQuantityPolicy.EvaluateDelivery(QTChungStatus.DaXuatKhoRework, true, 10, 10, 6, 4);
        Assert.True(d.Allowed);
        Assert.True(d.CompletesDelivery);
    }

    [Fact]
    public void Delivery_CannotExceedExportedEvenWhenApprovedAllowsMore()
    {
        var d = ReworkQuantityPolicy.EvaluateDelivery(QTChungStatus.DaXuatKhoRework, true, 10, 6, 4, 3);
        Assert.False(d.Allowed);
    }

    [Fact]
    public void Delivery_AfterFullDelivery_IsNotAccepted()
    {
        Assert.False(ReworkQuantityPolicy.EvaluateDelivery(QTChungStatus.DaGiaoSanXuat, true, 10, 10, 10, 1).Allowed);
        Assert.False(ReworkQuantityPolicy.EvaluateDelivery(QTChungStatus.DaXuatKhoRework, true, 10, 10, 10, 1).Allowed);
    }

    [Theory]
    [InlineData(QTChungStatus.DaDinhHuong)]
    [InlineData(QTChungStatus.Huy)]
    [InlineData(QTChungStatus.HoanTat)]
    [InlineData(QTChungStatus.DaQCXacNhanCuoi)]
    public void Delivery_WrongOrClosedStatus_IsRejected(QTChungStatus status)
    {
        Assert.False(ReworkQuantityPolicy.EvaluateDelivery(status, true, 10, 5, 0, 1).Allowed);
    }

    [Fact]
    public void Retry_AfterCommit_IsRejectedWhenItWouldExceedLimits()
    {
        // First export of 10 commits; an identical retry without OperationId dedupe sees X=10 and is refused.
        Assert.True(ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaDinhHuong, true, 10, 0, 0, 10).Allowed);
        Assert.False(ReworkQuantityPolicy.EvaluateExport(QTChungStatus.DaXuatKhoRework, true, 10, 10, 0, 10).Allowed);
        // Same for delivery: second identical delivery of 6 after D=6, X=6 is refused.
        Assert.False(ReworkQuantityPolicy.EvaluateDelivery(QTChungStatus.DaXuatKhoRework, true, 10, 6, 6, 6).Allowed);
    }
}
