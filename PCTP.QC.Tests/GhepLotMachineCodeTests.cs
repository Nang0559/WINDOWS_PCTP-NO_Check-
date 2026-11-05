using PCTP.Common;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// In Ghép Lot: @MACHINE của Usp_gheplotPrint = ký tự 14-16 (3 ký tự) của LOTNO.
/// </summary>
public sealed class GhepLotMachineCodeTests
{
    [Theory]
    [InlineData("26103000840200861700", "086")]
    [InlineData("2610300084020086", "086")]
    [InlineData("26103000840200861700-100", "086")]
    [InlineData("26103000840200861700-100,26103000840200861700-50", "086")]
    [InlineData("  26103000840200861700-100 ", "086")]
    public void MachineCode_IsCharacters14To16OfTheFirstLot(string lot, string expected)
    {
        Assert.Equal(expected, LotCodeHelper.GetGhepLotMachineCode(lot));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2610300084020")]   // chuẩn cũ 13 ký tự: không đủ để xác định máy
    [InlineData("261030008402008")] // 15 ký tự
    public void MachineCode_IsEmptyWhenLotTooShort(string lot)
    {
        Assert.Equal(string.Empty, LotCodeHelper.GetGhepLotMachineCode(lot));
    }
}
