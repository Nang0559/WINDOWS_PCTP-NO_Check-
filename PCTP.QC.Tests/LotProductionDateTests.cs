using PCTP.Common;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// "Ngày sản xuất" của ô tra cứu chung = 6 ký tự đầu của LOT (YYMMDD).
/// Dùng để lọc theo khoảng ngày sản xuất trực tiếp trên LOT, không cần truy vấn DB.
/// </summary>
public sealed class LotProductionDateTests
{
    [Theory]
    [InlineData("26103000840200861700", 2026, 10, 30)]   // chuẩn mới 20 ký tự
    [InlineData("2610300084020", 2026, 10, 30)]          // chuẩn cũ 13 ký tự
    [InlineData("26103000840200861700" + "00010100", 2026, 10, 30)] // LOT in đầy đủ (kèm Counter+Qty)
    [InlineData("2312022223", 2023, 12, 2)]              // LOT cũ trong kho (YYMMDD + hậu tố)
    [InlineData("  2312022223  ", 2023, 12, 2)]          // khoảng trắng hai đầu
    public void TryGetProductionDate_ReadsYyMmDdPrefix(string lot, int y, int m, int d)
    {
        Assert.True(LotCodeHelper.TryGetProductionDate(lot, out var date));
        Assert.Equal(new System.DateTime(y, m, d), date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2610")]            // quá ngắn
    [InlineData("ABCDEF0084020")]   // 6 ký tự đầu không phải số
    [InlineData("261330008402")]    // tháng 13
    [InlineData("260230008402")]    // 30/02 không tồn tại
    public void TryGetProductionDate_RejectsInvalidPrefix(string lot)
    {
        Assert.False(LotCodeHelper.TryGetProductionDate(lot, out _));
    }
}
