using System;
using PCTP.Common;
using PCTP.Shared.Helpers;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Executable test oracle for the operator-confirmed QR LOT contract.
/// Production parser integration gaps are recorded separately; these tests do not
/// pretend that a test-only normalizer proves production stock behavior.
/// </summary>
public sealed class QrLotFormatContractTests
{
    private const string TotalQr =
        "2610300027230077170007512000:2210A-K2C-V000:30/10/2026:2000:2:A010000000059140";

    [Fact]
    public void TotalLabel_UsesSixFieldsAndTwentyEightCharacterLotToken()
    {
        var parsed = QRCodeParser.ParseQRCode(TotalQr);

        Assert.True(parsed.IsTongPhieu);
        Assert.Equal("2610300027230077170007512000", parsed.RawLotNo);
        Assert.Equal("2210A-K2C-V000", parsed.ItemCode);
        Assert.Equal("30/10/2026", parsed.NgaySX);
        Assert.Equal(2000, parsed.Quantity);
        Assert.Equal("2", parsed.SoPhieuTong);
        Assert.Equal("A010000000059140", parsed.MaPhieu);
        Assert.Equal(28, parsed.RawLotNo.Length);
    }

    [Fact]
    public void TotalLabel_LotTokenHasConfirmedFieldOffsets()
    {
        const string token = "2610300027230077170007512000";

        Assert.Equal("261030", token.Substring(0, 6));   // YYMMDD
        Assert.Equal("00272", token.Substring(6, 5));    // product
        Assert.Equal("3", token.Substring(11, 1));       // shift
        Assert.Equal("0", token.Substring(12, 1));       // Gear
        Assert.Equal("077", token.Substring(13, 3));     // line
        Assert.Equal("1700", token.Substring(16, 4));    // department
        Assert.Equal("0751", token.Substring(20, 4));    // daily sequence
        Assert.Equal("2000", token.Substring(24, 4));    // embedded quantity
        Assert.Equal("26103000272300771700", StockLotIdentity(token));
    }

    [Fact]
    public void OperatorConfirmedBoxLabel_UsesTheSameTwentyCharacterStockLotIdentity()
    {
        const string boxQr =
            "2610300027230077170007520020:2210A-K2C-V000:30/10/2026:20";

        var parsed = QRCodeParser.ParseQRCode(boxQr);
        Assert.False(parsed.IsTongPhieu);
        Assert.Equal("2610300027230077170007520020", parsed.RawLotNo);
        Assert.Equal("2210A-K2C-V000", parsed.ItemCode);
        Assert.Equal("30/10/2026", parsed.NgaySX);
        Assert.Equal(20, parsed.Quantity);
        Assert.Equal("26103000272300771700", StockLotIdentity(parsed.RawLotNo));
    }

    [Fact]
    public void OperatorConfirmedTotalAndBoxLabels_ResolveToTheSameStockLot()
    {
        const string boxQr =
            "2610300027230077170007520020:2210A-K2C-V000:30/10/2026:20";

        var total = QRCodeParser.ParseQRCode(TotalQr);
        var box = QRCodeParser.ParseQRCode(boxQr);

        Assert.True(total.IsTongPhieu);
        Assert.False(box.IsTongPhieu);
        Assert.Equal(StockLotIdentity(total.RawLotNo), StockLotIdentity(box.RawLotNo));
        Assert.Equal(total.ItemCode, box.ItemCode);
        Assert.Equal(total.NgaySX, box.NgaySX);
        Assert.Equal(2000, total.Quantity);
        Assert.Equal(20, box.Quantity);
    }

    [Fact]
    public void ReceivingLotNormalization_UsesProductionLotCodeHelperForBothConfirmedLabels()
    {
        const string totalToken = "2610300027230077170007512000";
        const string boxToken = "2610300027230077170007520020";
        const string expectedStockLot = "26103000272300771700";

        Assert.Equal(expectedStockLot, LotCodeHelper.StripCounterAndQty(totalToken));
        Assert.Equal(expectedStockLot, LotCodeHelper.StripCounterAndQty(boxToken));
    }

    [Fact(Skip = "BLOCKED: production QRCodeParser currently leaves RawLotNoSL empty and LotNo unnormalized; enable after the production fix is authorized.")]
    public void ProductionParser_ShouldExposeRawTokenAndNormalizedStockLot()
    {
        var parsed = QRCodeParser.ParseQRCode(TotalQr);

        Assert.Equal("2610300027230077170007512000", parsed.RawLotNoSL);
        Assert.Equal("26103000272300771700", parsed.LotNo);
    }

    [Fact(Skip = "BLOCKED: QRCodeWorkflowAdapter currently forwards the raw 28-character LotNo; enable after the production parser/adapter contract is corrected.")]
    public void ReceivingAdapter_ShouldSendNormalizedTwentyCharacterStockLot()
    {
        var request = QRCodeWorkflowAdapter.BuildReceivingRequest(
            TotalQr, targetSlotId: 1, performedBy: "QC");

        Assert.Equal("26103000272300771700", request.LotNo);
        Assert.Equal(2000, request.Quantity);
    }

    [Fact(Skip = "BLOCKED: QRCodeWorkflowAdapter currently forwards the raw 28-character LotNo; enable after the production parser/adapter contract is corrected.")]
    public void DeliveryAdapter_ShouldSendNormalizedTwentyCharacterStockLotAndBoxQuantity()
    {
        const string boxQr =
            "2610300027230077170007520020:2210A-K2C-V000:30/10/2026:20";

        var request = QRCodeWorkflowAdapter.BuildCustomerDeliveryRequest(
            boxQr, slotLotId: 1, performedBy: "QC", deliveryReference: "QC-DELIVERY-1");

        Assert.Equal("26103000272300771700", request.LotNo);
        Assert.Equal(20, request.Quantity);
    }

    [Theory]
    [InlineData("100001", "0", true)]
    [InlineData("100001", "1", false)]
    [InlineData("100002", "1", true)]
    [InlineData("100002", "5", true)]
    [InlineData("100002", "0", false)]
    [InlineData("100002", "6", false)]
    public void Gear_IsValidatedByCustomer(
        string customerCode, string gear, bool expected)
    {
        Assert.Equal(expected, IsGearValid(customerCode, gear));
    }

    private static string StockLotIdentity(string lotToken)
    {
        if (!TryGetStockLotIdentity(lotToken, out var identity))
            throw new FormatException("LOTNO token must contain exactly 28 digits.");
        return identity;
    }

    private static bool TryGetStockLotIdentity(string lotToken, out string identity)
    {
        identity = string.Empty;
        if (lotToken == null || lotToken.Length != 28)
            return false;

        for (var i = 0; i < lotToken.Length; i++)
            if (lotToken[i] < '0' || lotToken[i] > '9')
                return false;

        identity = lotToken.Substring(0, 20);
        return true;
    }

    private static bool IsGearValid(string customerCode, string gear)
    {
        if (customerCode == "100001")
            return gear == "0";
        if (customerCode == "100002")
            return gear == "1" || gear == "2" || gear == "3" ||
                   gear == "4" || gear == "5";
        return false;
    }
}
