using PCTP.Common;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Form điều chỉnh LOT phải xử lý được cả chuẩn cũ (mã gốc 13 ký tự) và chuẩn mới (20 ký tự).
/// </summary>
public sealed class LotKeyKindTests
{
    private const string Full20 = "26103000840200861700";
    private const string Legacy13 = "2610300084020";

    [Fact]
    public void Classify_Distinguishes13And20CharacterStandards()
    {
        Assert.Equal(LotKeyKind.Legacy13, LotCodeHelper.ClassifyLotKey(Legacy13));
        Assert.Equal(LotKeyKind.Full20, LotCodeHelper.ClassifyLotKey(Full20));
        Assert.Equal(LotKeyKind.Full20, LotCodeHelper.ClassifyLotKey("  " + Full20 + "  "));
        // LOT in đầy đủ dài hơn 20 ký tự vẫn là chuẩn mới (lấy 20 ký tự đầu, giống AreLotKeysEquivalent).
        Assert.Equal(LotKeyKind.Full20, LotCodeHelper.ClassifyLotKey(Full20 + "00010100"));
        // 13..19 ký tự: chuẩn cũ, so theo 13 ký tự đầu.
        Assert.Equal(LotKeyKind.Legacy13, LotCodeHelper.ClassifyLotKey(Full20[..16]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("261030008402")]
    public void Classify_RejectsEmptyOrTooShort(string lot)
    {
        Assert.Equal(LotKeyKind.Unknown, LotCodeHelper.ClassifyLotKey(lot));
        Assert.Equal(string.Empty, LotCodeHelper.GetLegacyKey(lot));
        Assert.Equal(string.Empty, LotCodeHelper.GetFullKey(lot));
    }

    [Fact]
    public void Keys_AreDerivedFromTheSameLot()
    {
        Assert.Equal(Legacy13, LotCodeHelper.GetLegacyKey(Full20));
        Assert.Equal(Full20, LotCodeHelper.GetFullKey(Full20 + "00010100"));
        Assert.Equal(Legacy13, LotCodeHelper.GetLegacyKey(Legacy13));
        Assert.Equal(string.Empty, LotCodeHelper.GetFullKey(Legacy13));
    }

    [Fact]
    public void Classification_IsConsistentWithLotKeyEquivalence()
    {
        // Mã gốc 13 ký tự tương đương LOT 20 ký tự cùng mã gốc, nhưng hai LOT 20 ký tự khác nhau thì không.
        Assert.True(LotCodeHelper.AreLotKeysEquivalent(Legacy13, Full20));
        string otherFull20 = Legacy13 + "0099900";
        Assert.False(LotCodeHelper.AreLotKeysEquivalent(Full20, otherFull20));
        Assert.Equal(LotCodeHelper.GetLegacyKey(Full20), LotCodeHelper.GetLegacyKey(otherFull20));
    }
}
