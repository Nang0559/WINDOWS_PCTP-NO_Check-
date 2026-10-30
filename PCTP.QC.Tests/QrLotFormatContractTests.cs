using System;
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

    [Theory]
    [InlineData("2610300027230077170007520020", "26103000272300771700")]
    public void BoxLabel_UsesTheSameTwentyCharacterStockLotIdentity(
        string boxLotToken, string expectedStockLot)
    {
        Assert.Equal(28, boxLotToken.Length);
        Assert.Equal(expectedStockLot, StockLotIdentity(boxLotToken));
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

    [Fact]
    public void PreviouslySuppliedBoxQr_IsNotAValidTwentyEightCharacterLotFixture()
    {
        const string earlierBoxToken = "2610300027230077520020";

        Assert.Equal(22, earlierBoxToken.Length);
        Assert.False(TryGetStockLotIdentity(earlierBoxToken, out _));
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
