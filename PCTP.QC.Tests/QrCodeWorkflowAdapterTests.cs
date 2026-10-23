using PCTP.Shared.Helpers;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class QrCodeWorkflowAdapterTests
{
    [Fact]
    public void ReceivingQr_IsMappedToTheCentralStockCommandWithoutChangingPayloadSemantics()
    {
        var request = QRCodeWorkflowAdapter.BuildReceivingRequest(
            QrCodeFormatContractTests.ReceivingMasterLabel, 101, "qc-user");

        Assert.Equal("RECEIVE", request.MovementType);
        Assert.Equal(101, request.TargetSlotId);
        Assert.Equal("2608070085810078170000030500", request.LotNo);
        Assert.Equal("22110-K2T-V000", request.ItemCode);
        Assert.Equal(500, request.Quantity);
        Assert.Equal("A010000000059136", request.ReferenceId);
        Assert.Equal(QrCodeFormatContractTests.ReceivingMasterLabel, request.QrData);
    }

    [Fact]
    public void CustomerCartonQr_IsMappedToExactDeliveryReference()
    {
        var request = QRCodeWorkflowAdapter.BuildCustomerDeliveryRequest(
            QrCodeFormatContractTests.CustomerCartonLabel, 501, "delivery-user", "PO-001|22110-K2T-V000|2026-08-07|10:30");

        Assert.Equal("EXPORT", request.MovementType);
        Assert.Equal(501, request.SlotLotId);
        Assert.Equal("260807008581007817008000", request.LotNo);
        Assert.Equal("22110-K2T-V000", request.ItemCode);
        Assert.Equal(1000, request.Quantity);
        Assert.Equal("PO-001|22110-K2T-V000|2026-08-07|10:30", request.ReferenceId);
        Assert.Equal(QrCodeFormatContractTests.CustomerCartonLabel, request.QrData);
    }

    [Fact]
    public void MalformedQr_IsRejectedBeforeAnyStockCommandCanBeBuilt()
    {
        Assert.Throws<System.FormatException>(() =>
            QRCodeWorkflowAdapter.BuildReceivingRequest("lot:item:31/13/2026:10", 101, "qc-user"));
    }
}
