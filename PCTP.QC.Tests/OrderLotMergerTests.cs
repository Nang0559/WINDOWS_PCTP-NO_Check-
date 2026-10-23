using System.Data;
using PCTP.Modules.GiaoHangKhach.Models;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// DEL-011: LOT đã lưu (LUUPHIEUGIAOHANG) chỉ được ghép vào đúng dòng đơn
/// (PO + mã hàng + giờ:phút). Các test này khoá lại 2 lỗi cũ của MergeLotTuBangRieng:
/// giờ bị cắt về HH, và khoá dự phòng "mã hàng|giờ" bỏ qua PO.
/// </summary>
public sealed class OrderLotMergerTests
{
    private static DataTable OrderRows(params (string Po, string Part, string Gio, string Gear)[] rows)
    {
        var dt = new DataTable();
        foreach (var c in new[] { "PO_NO", "MAHANG", "GIO", "LOT", "STATUS", "STATUSDOC", "GEAR" })
            dt.Columns.Add(c, typeof(string));
        foreach (var r in rows)
            dt.Rows.Add(r.Po, r.Part, r.Gio, "", "NG", "NG", r.Gear);
        return dt;
    }

    private static DataTable SavedRows(params (string Po, string Part, string Gio, string Lot, string Status, string Gear)[] rows)
    {
        var dt = new DataTable();
        foreach (var c in new[] { "MAHANG", "GIOGIAO", "LOT", "STATUS", "STATUSDOC", "PO_NO", "SOLUONG", "GEAR" })
            dt.Columns.Add(c, typeof(string));
        foreach (var r in rows)
            dt.Rows.Add(r.Part, r.Gio, r.Lot, r.Status, r.Status, r.Po, "0", r.Gear);
        return dt;
    }

    [Fact]
    public void SamePoPartDate_TwoDeliveryTimes_EachRowGetsItsOwnLot()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-1", "P-A", "14:00", ""));
        var saved = SavedRows(
            ("PO-1", "P-A", "14:00", "LOT-AFTERNOON", "OK", ""),
            ("PO-1", "P-A", "08:00", "LOT-MORNING", "OK", ""));

        Assert.Equal(2, OrderLotMerger.Merge(order, saved));

        Assert.Equal("LOT-MORNING", order.Rows[0]["LOT"]);
        Assert.Equal("LOT-AFTERNOON", order.Rows[1]["LOT"]);
    }

    [Fact]
    public void OnlyOneDeliveryTimeSaved_OtherTimeStaysUnmerged()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-1", "P-A", "14:00", ""));
        var saved = SavedRows(("PO-1", "P-A", "14:00", "LOT-AFTERNOON", "OK", ""));

        Assert.Equal(1, OrderLotMerger.Merge(order, saved));

        Assert.Equal("", order.Rows[0]["LOT"]);
        Assert.Equal("NG", order.Rows[0]["STATUS"]);
        Assert.Equal("LOT-AFTERNOON", order.Rows[1]["LOT"]);
        Assert.Equal("OK", order.Rows[1]["STATUS"]);
    }

    [Fact]
    public void SameHourDifferentMinutes_DoNotShareTheSameLot()
    {
        // Lỗi cũ: giờ bị cắt về "08" nên 08:00 và 08:30 cùng nhận LOT của 08:30.
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-1", "P-A", "08:30", ""));
        var saved = SavedRows(("PO-1", "P-A", "08:30", "LOT-0830", "OK", ""));

        Assert.Equal(1, OrderLotMerger.Merge(order, saved));

        Assert.Equal("", order.Rows[0]["LOT"]);
        Assert.Equal("LOT-0830", order.Rows[1]["LOT"]);
    }

    [Fact]
    public void LotSavedForAnotherPo_IsNeverAssigned_EvenWithSamePartAndTime()
    {
        // Lỗi cũ: khoá dự phòng "mã hàng|giờ" bỏ PO nên LOT của PO-2 bị gán cho PO-1.
        var order = OrderRows(("PO-1", "P-A", "08:00", ""));
        var saved = SavedRows(("PO-2", "P-A", "08:00", "LOT-OF-PO-2", "OK", ""));

        Assert.Equal(0, OrderLotMerger.Merge(order, saved));

        Assert.Equal("", order.Rows[0]["LOT"]);
    }

    [Fact]
    public void TwoPosSamePartSameTime_EachGetsItsOwnLot()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-2", "P-A", "08:00", ""));
        var saved = SavedRows(
            ("PO-2", "P-A", "08:00", "LOT-PO2", "OK", ""),
            ("PO-1", "P-A", "08:00", "LOT-PO1", "OK", ""));

        Assert.Equal(2, OrderLotMerger.Merge(order, saved));

        Assert.Equal("LOT-PO1", order.Rows[0]["LOT"]);
        Assert.Equal("LOT-PO2", order.Rows[1]["LOT"]);
    }

    [Fact]
    public void PoAndPartMatch_IsCaseAndWhitespaceInsensitive()
    {
        var order = OrderRows((" po-1 ", "p-a", "08:00", ""));
        var saved = SavedRows(("PO-1", "P-A ", "08:00", "LOT-1", "OK", ""));

        Assert.Equal(1, OrderLotMerger.Merge(order, saved));
        Assert.Equal("LOT-1", order.Rows[0]["LOT"]);
    }

    [Fact]
    public void LegacySavedRowWithoutPo_MatchesWhenUnambiguous()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""));
        var saved = SavedRows(("", "P-A", "08:00", "LOT-LEGACY", "OK", ""));

        Assert.Equal(1, OrderLotMerger.Merge(order, saved));
        Assert.Equal("LOT-LEGACY", order.Rows[0]["LOT"]);
    }

    [Fact]
    public void LegacySavedRowWithoutPo_IsNotGuessedWhenTwoPosShareTheSameSlot()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-2", "P-A", "08:00", ""));
        var saved = SavedRows(("", "P-A", "08:00", "LOT-LEGACY", "OK", ""));

        Assert.Equal(0, OrderLotMerger.Merge(order, saved));
        Assert.Equal("", order.Rows[0]["LOT"]);
        Assert.Equal("", order.Rows[1]["LOT"]);
    }

    [Fact]
    public void LegacyHourOnlySavedRow_MatchesTheSingleRowOfThatHour()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-1", "P-A", "14:00", ""));
        var saved = SavedRows(("PO-1", "P-A", "14", "LOT-LEGACY-HOUR", "OK", ""));

        Assert.Equal(1, OrderLotMerger.Merge(order, saved));
        Assert.Equal("", order.Rows[0]["LOT"]);
        Assert.Equal("LOT-LEGACY-HOUR", order.Rows[1]["LOT"]);
    }

    [Fact]
    public void LegacyHourOnlySavedRow_IsNotGuessedWhenTwoMinutesShareTheHour()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-1", "P-A", "08:30", ""));
        var saved = SavedRows(("PO-1", "P-A", "08", "LOT-LEGACY-HOUR", "OK", ""));

        Assert.Equal(0, OrderLotMerger.Merge(order, saved));
    }

    [Fact]
    public void ExactMinuteMatch_WinsOverLegacyHourOnlyRow()
    {
        var order = OrderRows(("PO-1", "P-A", "08:30", ""));
        var saved = SavedRows(
            ("PO-1", "P-A", "08", "LOT-LEGACY-HOUR", "OK", ""),
            ("PO-1", "P-A", "08:30", "LOT-EXACT", "OK", ""));

        Assert.Equal(1, OrderLotMerger.Merge(order, saved));
        Assert.Equal("LOT-EXACT", order.Rows[0]["LOT"]);
    }

    [Fact]
    public void GroupedHoursAndBlankLots_AreIgnored()
    {
        var order = OrderRows(("PO-1", "P-A", "15:00", ""));
        var saved = SavedRows(
            ("PO-1", "P-A", "'15','16'", "LOT-GROUP", "OK", ""),
            ("PO-1", "P-A", "15:00", "", "OK", ""));

        Assert.Equal(0, OrderLotMerger.Merge(order, saved));
        Assert.Equal("", order.Rows[0]["LOT"]);
    }

    [Fact]
    public void MergedRow_CopiesStatusAndFillsGearOnlyWhenEmpty()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""), ("PO-1", "P-B", "08:00", "G-KEEP"));
        var saved = SavedRows(
            ("PO-1", "P-A", "08:00", "LOT-A", "OK", "G-SAVED"),
            ("PO-1", "P-B", "08:00", "LOT-B", "OK", "G-OTHER"));

        Assert.Equal(2, OrderLotMerger.Merge(order, saved));

        Assert.Equal("OK", order.Rows[0]["STATUS"]);
        Assert.Equal("OK", order.Rows[0]["STATUSDOC"]);
        Assert.Equal("G-SAVED", order.Rows[0]["GEAR"]);
        Assert.Equal("G-KEEP", order.Rows[1]["GEAR"]);
        Assert.True(order.Columns.Contains("GearSuDung"));
    }

    [Fact]
    public void NoSavedRows_ReturnsZeroAndLeavesOrderUntouched()
    {
        var order = OrderRows(("PO-1", "P-A", "08:00", ""));

        Assert.Equal(0, OrderLotMerger.Merge(order, SavedRows()));
        Assert.Equal(0, OrderLotMerger.Merge(order, null));
        Assert.Equal(0, OrderLotMerger.Merge(null, SavedRows(("PO-1", "P-A", "08:00", "L", "OK", ""))));
        Assert.False(order.Columns.Contains("GearSuDung"));
        Assert.Equal("", order.Rows[0]["LOT"]);
    }

    [Fact]
    public void OrderRowsWithoutPoColumn_StillMatchSavedRowsWithoutPo()
    {
        var order = new DataTable();
        foreach (var c in new[] { "MAHANG", "GIO", "LOT", "STATUS", "STATUSDOC" })
            order.Columns.Add(c, typeof(string));
        order.Rows.Add("P-A", "08:00", "", "NG", "NG");
        var saved = SavedRows(("", "P-A", "08:00", "LOT-1", "OK", ""));

        Assert.Equal(1, OrderLotMerger.Merge(order, saved));
        Assert.Equal("LOT-1", order.Rows[0]["LOT"]);
    }
}
