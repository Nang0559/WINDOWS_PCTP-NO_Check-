using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Models;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// DEL-009: một dòng đơn trong bảng đơn riêng được định danh bằng
/// PO + mã hàng + ngày + giờ:phút. PO + ngày + mã hàng KHÔNG đủ duy nhất vì cùng ngày,
/// cùng PO, cùng mã hàng vẫn có nhiều đợt giao khác giờ.
/// </summary>
public sealed class OrderLineKeyTests
{
    private static readonly DateTime Day = new DateTime(2026, 10, 23);

    private static OrderLineKey Key(string po, string part, int hh, int mm = 0, DateTime? day = null) =>
        OrderLineKey.Create(po, part, day ?? Day, new TimeSpan(hh, mm, 0));

    [Fact]
    public void SamePoPartAndDate_DifferentDeliveryHours_AreDifferentKeys()
    {
        var morning = Key("PO-001", "PART-A", 8);
        var afternoon = Key("PO-001", "PART-A", 14);

        Assert.NotEqual(morning, afternoon);
        Assert.False(morning.Equals(afternoon));
    }

    [Fact]
    public void ThreeDeliveriesOfSamePoPartDate_ProduceThreeDistinctKeys()
    {
        var keys = new HashSet<OrderLineKey>
        {
            Key("PO-001", "PART-A", 8),
            Key("PO-001", "PART-A", 11),
            Key("PO-001", "PART-A", 14)
        };

        Assert.Equal(3, keys.Count);
    }

    [Fact]
    public void SameHourDifferentMinutes_AreDifferentKeys()
    {
        // TableOrderRepo gom dòng theo HH:mm, nên 08:00 và 08:30 là hai dòng TMP khác nhau.
        Assert.NotEqual(Key("PO-001", "PART-A", 8, 0), Key("PO-001", "PART-A", 8, 30));
    }

    [Fact]
    public void SecondsAreIgnored_SameMinuteIsSameKey()
    {
        var a = OrderLineKey.FromDateTime("PO-001", "PART-A", new DateTime(2026, 10, 23, 14, 0, 0));
        var b = OrderLineKey.FromDateTime("PO-001", "PART-A", new DateTime(2026, 10, 23, 14, 0, 45));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void DifferentPo_DifferentPart_DifferentDate_AreDifferentKeys()
    {
        var baseKey = Key("PO-001", "PART-A", 14);

        Assert.NotEqual(baseKey, Key("PO-002", "PART-A", 14));
        Assert.NotEqual(baseKey, Key("PO-001", "PART-B", 14));
        Assert.NotEqual(baseKey, Key("PO-001", "PART-A", 14, 0, Day.AddDays(1)));
    }

    [Fact]
    public void PoAndPart_AreTrimmedAndCaseInsensitive_LikeSqlServerDefaultCollation()
    {
        var a = Key("PO-001", "part-a", 14);
        var b = Key("  po-001 ", "PART-A  ", 14);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal("po-001", b.PoNo);
        Assert.Equal("PART-A", b.MaHang);
    }

    [Fact]
    public void NgayGiao_KeepsOnlyTheDatePart()
    {
        var key = OrderLineKey.Create("PO-001", "PART-A", new DateTime(2026, 10, 23, 17, 45, 0), new TimeSpan(8, 0, 0));

        Assert.Equal(new DateTime(2026, 10, 23), key.NgayGiao);
        Assert.Equal(new TimeSpan(8, 0, 0), key.GioGiao);
    }

    [Fact]
    public void FromTmpValues_UsesTimeOfDayOfNgayGiao()
    {
        var key = OrderLineKey.FromTmpValues("PO-001", "PART-A", "2026-10-23 14:00:00", "'08','14'");

        Assert.Equal(new TimeSpan(14, 0, 0), key.GioGiao);
        Assert.Equal(Day, key.NgayGiao);
    }

    [Fact]
    public void FromTmpValues_AcceptsDateTimeObjectFromDatabase()
    {
        var key = OrderLineKey.FromTmpValues("PO-001", "PART-A", new DateTime(2026, 10, 23, 8, 30, 0), "");

        Assert.Equal(new TimeSpan(8, 30, 0), key.GioGiao);
    }

    [Fact]
    public void FromTmpValues_MidnightNgayGiao_FallsBackToConcreteGioGiaoColumn()
    {
        var key = OrderLineKey.FromTmpValues("PO-001", "PART-A", "2026-10-23 00:00:00", "14:30");

        Assert.Equal(new TimeSpan(14, 30, 0), key.GioGiao);
    }

    [Fact]
    public void FromTmpValues_MidnightNgayGiao_IgnoresGroupedHours()
    {
        // GIOGIAO dạng nhóm ("'15','16'") không phải một giờ cụ thể nên không được đoán.
        var key = OrderLineKey.FromTmpValues("PO-001", "PART-A", "2026-10-23", "'15','16'");

        Assert.Equal(TimeSpan.Zero, key.GioGiao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void FromTmpValues_UnreadableNgayGiao_Throws(string ngay)
    {
        Assert.Throws<FormatException>(() => OrderLineKey.FromTmpValues("PO-001", "PART-A", ngay, "14:00"));
    }

    [Theory]
    [InlineData(null, "PART-A")]
    [InlineData("", "PART-A")]
    [InlineData("   ", "PART-A")]
    [InlineData("PO-001", null)]
    [InlineData("PO-001", " ")]
    public void BlankPoOrPart_IsRejected_BecauseKeyWouldNotBeUnique(string po, string part)
    {
        Assert.Throws<ArgumentException>(() => OrderLineKey.Create(po, part, Day, new TimeSpan(8, 0, 0)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24 * 60)]
    public void TimeOutsideOneDay_IsRejected(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderLineKey.Create("PO-001", "PART-A", Day, TimeSpan.FromMinutes(minutes)));
    }

    [Theory]
    [InlineData("8", 8, 0, false)]
    [InlineData("08", 8, 0, false)]
    [InlineData("8H", 8, 0, false)]
    [InlineData("'15'", 15, 0, false)]
    [InlineData("08:30", 8, 30, true)]
    [InlineData("08:30:00", 8, 30, true)]
    [InlineData(" 7:05 ", 7, 5, true)]
    [InlineData("23:59", 23, 59, true)]
    [InlineData("0", 0, 0, false)]
    public void TryParseGio_AcceptsKnownFormats(string raw, int hh, int mm, bool hasMinute)
    {
        Assert.True(OrderLineKey.TryParseGio(raw, out var gio, out var minute));
        Assert.Equal(new TimeSpan(hh, mm, 0), gio);
        Assert.Equal(hasMinute, minute);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("'15','16'")]
    [InlineData("15+16")]
    [InlineData("24")]
    [InlineData("08:60")]
    [InlineData("08:30:61")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1:2:3:4")]
    public void TryParseGio_RejectsGroupsAndGarbage_InsteadOfMappingToMidnight(string raw)
    {
        Assert.False(OrderLineKey.TryParseGio(raw, out _, out _));
    }

    [Fact]
    public void ToString_ContainsEveryIdentityComponent()
    {
        var text = Key("PO-001", "PART-A", 8, 5).ToString();

        Assert.Contains("PO-001", text);
        Assert.Contains("PART-A", text);
        Assert.Contains("2026-10-23", text);
        Assert.Contains("08:05", text);
    }

    [Fact]
    public void KeysWorkAsDictionaryKeys_PerDeliveryTime()
    {
        var delivered = new Dictionary<OrderLineKey, bool>
        {
            [Key("PO-001", "PART-A", 8)] = true,
            [Key("PO-001", "PART-A", 14)] = false
        };

        Assert.True(delivered[Key("po-001", "part-a", 8)]);
        Assert.False(delivered[Key("PO-001", "PART-A", 14)]);
        Assert.False(delivered.ContainsKey(Key("PO-001", "PART-A", 11)));
        Assert.Equal(2, delivered.Keys.Distinct().Count());
    }
}
