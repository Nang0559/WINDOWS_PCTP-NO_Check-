using PCTP.Shared.Helpers;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class TachLotQrParserTests
{
    [Fact]
    public void Lot28CharsWithPaddedQtyTailStripsAllFourDigits()
    {
        // Tem gốc: LOT 28 ký tự, đuôi "0300" = SL tem 300 đệm 4 số.
        var info = QRCodeParser.ParseTachLotQr("2611050075810082170003000300:22600-K58-TC11-M1:05/11/26:300");
        Assert.Equal("261105007581008217000300", info.LotNo);
        Assert.Equal("300", info.SlTemFccRaw);
        // Tem tách 100 phải là LOT gốc + "0100" (28 ký tự), không thừa số 0.
        Assert.Equal("2611050075810082170003000100", info.LotNo + "0100");
    }

    [Fact]
    public void Lot27CharsKeepsLegacyFourDigitStrip()
    {
        var info = QRCodeParser.ParseTachLotQr("261105007581008217000300300:ITEM:05/11/26:300");
        Assert.Equal(27 - 4, info.LotNo.Length);
    }
}
