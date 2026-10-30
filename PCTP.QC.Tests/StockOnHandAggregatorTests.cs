using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Form "Tồn kho thành phẩm" (TONKHOTP) dựng từ sp_GetCurrentStockStatus: tách phần đã xếp slot / chưa gán slot,
/// cộng theo mã hàng, sắp ngày nhập mới nhất lên trên và đánh dấu LOT cần đối chiếu.
/// </summary>
public sealed class StockOnHandAggregatorTests
{
    private static CurrentStockRow Row(string lot, string item, decimal qty, string location,
        DateTime? received = null, DateTime? slotImport = null, decimal onHand = 0, decimal recv = 0, decimal issued = 0,
        string status = "Dang mo") => new CurrentStockRow
    {
        LotNo = lot, ItemCode = item, ItemName = item + "-name", Quantity = qty, LocationCode = location,
        QtyOnHandLot = onHand, QtyReceived = recv, QtyIssued = issued, ReceivedDate = received,
        SlotImportDate = slotImport, Status = status
    };

    [Fact]
    public void LotRows_SplitSlottedAndUnslotted_AndKeepLotOnHandOnce()
    {
        var lots = StockOnHandAggregator.BuildLotRows(new[]
        {
            Row("LOT-A", "P1", 600, "Kho-R1-Slot1", onHand: 1000, recv: 1000),
            Row("LOT-A", "P1", 100, "Kho-R1-Slot2", onHand: 1000, recv: 1000),
            Row("LOT-A", "P1", 300, StockOnHandAggregator.UnslottedLocation, onHand: 1000, recv: 1000),
        });

        var lot = Assert.Single(lots);
        Assert.Equal(1000, lot.QtyOnHand);          // SLCONLAI không bị cộng lặp theo số dòng
        Assert.Equal(700, lot.QtySlotted);
        Assert.Equal(300, lot.QtyUnslotted);
        Assert.Equal(2, lot.SlotCount);
        Assert.Contains("Chưa gán slot", lot.Check);
    }

    [Fact]
    public void LotRows_AreOrderedByNewestReceiptFirst_WithUndatedLast()
    {
        var lots = StockOnHandAggregator.BuildLotRows(new[]
        {
            Row("OLD", "P1", 1, "S", received: new DateTime(2026, 1, 1), onHand: 1, recv: 1),
            Row("NEW", "P1", 1, "S", received: new DateTime(2026, 10, 30), onHand: 1, recv: 1),
            Row("NODATE", "P1", 1, "S", onHand: 1, recv: 1),
            Row("SLOTNEW", "P1", 1, "S", received: new DateTime(2026, 2, 1), slotImport: new DateTime(2026, 10, 31), onHand: 1, recv: 1),
        });

        Assert.Equal(new[] { "SLOTNEW", "NEW", "OLD", "NODATE" }, lots.Select(l => l.LotNo).ToArray());
    }

    [Fact]
    public void Check_FlagsScientificLot_AndReceivedIssuedMismatch_ButOkLotStaysOk()
    {
        var lots = StockOnHandAggregator.BuildLotRows(new[]
        {
            Row("2.61021E+19", "P1", 100, StockOnHandAggregator.UnslottedLocation, onHand: 100, recv: 100),
            Row("GOOD", "P2", 50, "Kho-R1-Slot1", onHand: 50, recv: 80, issued: 30),
            Row("DIFF", "P3", 50, "Kho-R1-Slot2", onHand: 50, recv: 80, issued: 10),
        }).ToDictionary(l => l.LotNo);

        Assert.True(lots["2.61021E+19"].LotNoFormatError);
        Assert.Contains("sai định dạng", lots["2.61021E+19"].Check);
        Assert.Equal("OK", lots["GOOD"].Check);
        Assert.Contains("lệch 20", lots["DIFF"].Check);
    }

    [Fact]
    public void ItemRows_SumLots_AndCountLotsToCheck()
    {
        var lots = StockOnHandAggregator.BuildLotRows(new[]
        {
            Row("L1", "P1", 100, "S1", received: new DateTime(2026, 3, 1), onHand: 100, recv: 100),
            Row("L2", "P1", 40, StockOnHandAggregator.UnslottedLocation, received: new DateTime(2026, 4, 1), onHand: 40, recv: 40, status: "Da ket thuc"),
            Row("L3", "P2", 5, "S2", received: new DateTime(2026, 5, 1), onHand: 5, recv: 5),
        });

        var items = StockOnHandAggregator.BuildItemRows(lots);

        Assert.Equal(new[] { "P2", "P1" }, items.Select(i => i.ItemCode).ToArray());   // P2 nhập mới nhất
        var p1 = items.Single(i => i.ItemCode == "P1");
        Assert.Equal(2, p1.LotCount);
        Assert.Equal(1, p1.OpenLotCount);
        Assert.Equal(140, p1.QtyOnHand);
        Assert.Equal(100, p1.QtySlotted);
        Assert.Equal(40, p1.QtyUnslotted);
        Assert.Equal(1, p1.LotsToCheck);
    }

    [Fact]
    public void LegacyProcedureWithoutLotColumns_FallsBackToRowSum()
    {
        var lots = StockOnHandAggregator.BuildLotRows(new[]
        {
            new CurrentStockRow { LotNo = "L", ItemCode = "P", Quantity = 70, LocationCode = "S1" },
            new CurrentStockRow { LotNo = "L", ItemCode = "P", Quantity = 30, LocationCode = StockOnHandAggregator.UnslottedLocation },
        });

        Assert.Equal(100, Assert.Single(lots).QtyOnHand);
    }

    [Fact]
    public void CurrentStockRow_LastReceivedDate_IsTheNewerOfLotAndSlotDate()
    {
        var a = new CurrentStockRow { ReceivedDate = new DateTime(2026, 5, 1), SlotImportDate = new DateTime(2026, 6, 1) };
        var b = new CurrentStockRow { ReceivedDate = new DateTime(2026, 7, 1), SlotImportDate = new DateTime(2026, 6, 1) };
        var c = new CurrentStockRow { SlotImportDate = new DateTime(2026, 6, 1) };
        var d = new CurrentStockRow();

        Assert.Equal(new DateTime(2026, 6, 1), a.LastReceivedDate.Value);
        Assert.Equal(new DateTime(2026, 7, 1), b.LastReceivedDate.Value);
        Assert.Equal(new DateTime(2026, 6, 1), c.LastReceivedDate.Value);
        Assert.True(!d.LastReceivedDate.HasValue);
    }
}
