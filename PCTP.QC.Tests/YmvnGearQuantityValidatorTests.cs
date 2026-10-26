using System.Data;
using PCTP.Modules.GiaoHangKhach.Services;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class YmvnGearQuantityValidatorTests
{
    private static DataTable Order(params (string Part, string Gear, int Qty)[] rows)
    {
        var table = new DataTable();
        table.Columns.Add("MAHANGFCC", typeof(string));
        table.Columns.Add("GEAR", typeof(string));
        table.Columns.Add("SLTEMFCC", typeof(int));
        foreach (var row in rows) table.Rows.Add(row.Part, row.Gear, row.Qty);
        return table;
    }

    private static DataTable Scans(params (string Part, string Lot, int Qty)[] rows)
    {
        var table = new DataTable();
        table.Columns.Add("MAHANGFCC", typeof(string));
        table.Columns.Add("LOTFCC", typeof(string));
        table.Columns.Add("SLTEMFCC", typeof(int));
        foreach (var row in rows) table.Rows.Add(row.Part, row.Lot, row.Qty);
        return table;
    }

    private static string Lot(char gear) => "ABCDEFGHIJKL" + gear;

    [Fact]
    public void ScanUsesGearFromThirteenthLotCharacter()
    {
        var ok = YmvnGearQuantityValidator.ValidateScan(
            Lot('1') + ":PART01:20261022:5:X:Y", Order(("PART01", "A", 5)), Scans(), out var error);
        Assert.True(ok, error);
    }

    [Fact]
    public void ScanCannotExceedPartAndGearOrderQuantity()
    {
        var ok = YmvnGearQuantityValidator.ValidateScan(
            Lot('1') + ":PART01:20261022:6:X:Y", Order(("PART01", "A", 5)), Scans(), out var error);
        Assert.False(ok);
        Assert.Contains("vượt", error);
    }

    [Fact]
    public void ScanRejectsGearNotPresentInOrder()
    {
        var ok = YmvnGearQuantityValidator.ValidateScan(
            Lot('2') + ":PART01:20261022:1:X:Y", Order(("PART01", "A", 5)), Scans(), out var error);
        Assert.False(ok);
        Assert.Contains("Gear", error);
    }

    [Fact]
    public void CompletionRequiresExactPartGearQuantities()
    {
        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            Order(("PART01", "A", 5), ("PART01", "B", 3)),
            Scans(("PART01", Lot('1'), 5), ("PART01", Lot('2'), 2)), out var error);
        Assert.False(ok);
        Assert.Contains("Chưa đủ", error);
    }

    [Fact]
    public void CompletionRejectsUnexpectedScannedGear()
    {
        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            Order(("PART01", "A", 5)),
            Scans(("PART01", Lot('1'), 5), ("PART01", Lot('3'), 1)), out var error);
        Assert.False(ok);
        Assert.Contains("không tồn tại", error);
    }

    [Fact]
    public void EmptyOrderIsRejectedWithDiagnostic()
    {
        var ok = YmvnGearQuantityValidator.ValidateCompletion(Order(), Scans(), out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }
}