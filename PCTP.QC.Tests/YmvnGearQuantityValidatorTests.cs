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
    public void LotGearCodeZeroMatchesBlankGearOnDeliveryOrder()
    {
        const string fcc = "261102009410008217000860010:PART01:02/11/2026:5";
        var order = Order(("PART01", "", 5));

        var ok = YmvnGearQuantityValidator.ValidateScan(
            fcc, order, Scans(), out var error, code => code == 0 ? "" : null);

        Assert.True(ok, error);
    }

    [Fact]
    public void CompletionTreatsBlankOrderGearAsB20GearCodeZero()
    {
        var order = Order(("PART01", "", 5));
        var scans = Scans(("PART01", Lot('0'), 5));

        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            order, scans, out var error, code => code == 0 ? "" : null);

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
    public void CompletionAllowsPartialPartGearQuantitiesButKeepsGearValidation()
    {
        // Gear A đủ; Gear B mới đọc một phần. Chỉ Gear A/B đã đọc được tổng hợp,
        // không yêu cầu phần còn thiếu của Gear B phải được đọc trong lần hoàn thành này.
        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            Order(("PART01", "A", 5), ("PART01", "B", 3)),
            Scans(("PART01", Lot('1'), 5), ("PART01", Lot('2'), 2)), out var error);
        Assert.True(ok, error);
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

    // Regression: TMPPHIEUGIAOHANG_100002 chỉ có dòng B5V (GEAR = "Gear D :450pcs"). Hoàn thành phải
    // đối chiếu đúng tập dòng TMP này, không đòi mã khác (vd 1FP-E6300-01-00-80) từ nguồn khác.
    [Fact]
    public void CompletionPassesWhenTmpRowsAreFullyScanned()
    {
        var tmp = new DataTable();
        tmp.Columns.Add("MAHANG", typeof(string));
        tmp.Columns.Add("GEAR", typeof(string));
        tmp.Columns.Add("SOLUONG", typeof(int));
        tmp.Rows.Add("B5V-E6300-00-00-80", "Gear D :450pcs", 450);

        var scans = Scans();
        for (int i = 0; i < 90; i++) scans.Rows.Add("B5V-E6300-00-00-80", Lot('3'), 5);

        var ok = YmvnGearQuantityValidator.ValidateCompletion(tmp, scans, out var error);
        Assert.True(ok, error);
    }

    [Fact]
    public void CompletionAllowsScannedLineWhenOtherOrderLinesAreUnread()
    {
        // Dòng 1FP chưa đọc không được chặn tổng hợp LOT của dòng B5V đã đọc đủ.
        var order = Order(("B5V-E6300-00-00-80", "Gear D :450pcs", 450), ("1FP-E6300-01-00-80", "Gear D :150pcs", 150));
        var scans = Scans();
        for (int i = 0; i < 90; i++) scans.Rows.Add("B5V-E6300-00-00-80", Lot('3'), 5);

        var ok = YmvnGearQuantityValidator.ValidateCompletion(order, scans, out var error);
        Assert.True(ok, error);
    }

    [Fact]
    public void CompletionAllowsPartialQuantityForScannedLine()
    {
        // Có 5 dòng trong phiếu; chỉ dòng 4/5 có QR. Hai dòng đã đọc vẫn phải được tổng hợp LOT.
        var order = Order(
            ("PART01", "A", 10),
            ("PART02", "A", 10),
            ("PART03", "A", 10),
            ("PART04", "A", 10),
            ("PART05", "B", 10));
        var scans = Scans(
            ("PART04", Lot('1'), 5),
            ("PART05", Lot('2'), 7));

        var ok = YmvnGearQuantityValidator.ValidateCompletion(order, scans, out var error);
        Assert.True(ok, error);
    }

    [Fact]
    public void CompletionRejectsWhenNoQrWasScanned()
    {
        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            Order(("PART01", "A", 5)), Scans(), out var error);

        Assert.False(ok);
        Assert.Contains("Chưa đọc QR", error);
    }

    [Fact]
    public void CompletionRejectsPartialScanThatExceedsDeliveryQuantity()
    {
        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            Order(("PART01", "A", 5)),
            Scans(("PART01", Lot('1'), 6)), out var error);

        Assert.False(ok);
        Assert.Contains("vượt", error);
    }

    [Fact]
    public void CombinedGearOrderAllowsGearDOnlyUpToItsOwnQuantity()
    {
        var order = Order(
            ("B5V-E6300-00-00-80", "Gear E :400pcs, Gear D :100pcs", 500));

        var withinLimit = YmvnGearQuantityValidator.ValidateScan(
            "ABCDEFGHIJKL4:B5V-E6300-00-00-80:07/11/2026:100",
            order, Scans(), out var error, code => code == 4 ? "Gear D" : code == 5 ? "Gear E" : "");

        Assert.True(withinLimit, error);

        var overLimit = YmvnGearQuantityValidator.ValidateScan(
            "ABCDEFGHIJKL4:B5V-E6300-00-00-80:07/11/2026:101",
            order, Scans(), out error, code => code == 4 ? "Gear D" : code == 5 ? "Gear E" : "");

        Assert.False(overLimit);
        Assert.Contains("vượt", error);
        Assert.Contains("100", error);
    }

    [Fact]
    public void CombinedGearOrderAllowsGearEOnlyUpToItsOwnQuantity()
    {
        var order = Order(
            ("B5V-E6300-00-00-80", "Gear E :400pcs, Gear D :100pcs", 500));

        var ok = YmvnGearQuantityValidator.ValidateScan(
            "ABCDEFGHIJKL5:B5V-E6300-00-00-80:07/11/2026:400",
            order, Scans(), out var error, code => code == 4 ? "Gear D" : code == 5 ? "Gear E" : "");

        Assert.True(ok, error);
    }

    [Fact]
    public void CombinedGearCompletionAggregatesEachGearSeparately()
    {
        var order = Order(
            ("B5V-E6300-00-00-80", "Gear E :400pcs, Gear D :100pcs", 500));
        var scans = Scans(
            ("B5V-E6300-00-00-80", Lot('4'), 100),
            ("B5V-E6300-00-00-80", Lot('5'), 400));

        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            order, scans, out var error,
            code => code == 4 ? "Gear D" : code == 5 ? "Gear E" : "");

        Assert.True(ok, error);
    }

    [Fact]
    public void CombinedGearBreakdownMustEqualOrderLineQuantity()
    {
        var order = Order(
            ("B5V-E6300-00-00-80", "Gear E :400pcs, Gear D :100pcs", 600));

        var ok = YmvnGearQuantityValidator.ValidateCompletion(
            order, Scans(), out var error);

        Assert.False(ok);
        Assert.Contains("không khớp SOLUONG", error);
    }

    // ── Real YMVN labels (photo IMG_5411: B5V-E6300-00-00-80, order #01289, Total Ordered 700) ──
    private const string GS = "\u001D";
    private const string YamahaCardWithSeparators =
        "[)>\u001E06" + GS + "9KY0E" + GS + "PB5VE6300000080" + GS + "V1073" + GS + "3L9204" + GS +
        "K01289" + GS + "Q000700" + GS + "10K" + "                " + GS + "Z" + "       " + GS + "\u001E\u0004";
    private const string YamahaCardSeparatorsDropped =
        "[)>0699KY0EPB5VE6300000080V10733L9204K01289Q00070010K                Z       ";

    [Theory]
    [InlineData(YamahaCardWithSeparators)]
    [InlineData(YamahaCardSeparatorsDropped)]
    public void YamahaItemCardIsParsedIntoPartOrderAndTotalQuantity(string raw)
    {
        Assert.True(YmvnTemParser.TryParse(raw, out var part, out var order, out var qty));
        Assert.Equal("B5VE6300000080", part);
        Assert.Equal("01289", order);
        Assert.Equal(700, qty);
    }

    [Fact]
    public void FccTemIsNeverTreatedAsYamahaCard()
    {
        // Part names such as 1FP-... contain P/K/Q but the FCC tem has ':' separators.
        Assert.False(YmvnTemParser.TryParse("2610290059924081170005730005:1FP-E6300-01-00-80:29/10/2026:5", out _, out _, out _));
    }

    [Fact]
    public void YamahaItemCardPassesScanValidationWithoutLotOrGear()
    {
        var ok = YmvnGearQuantityValidator.ValidateScan(
            YamahaCardSeparatorsDropped, Order(("B5V-E6300-00-00-80", "D", 200)), Scans(), out var error);
        Assert.True(ok, error);
    }

    [Fact]
    public void FourPartFccTemIsAcceptedAndGearComesFromCatalog()
    {
        const string fcc = "2610290059924081170005730005:B5V-E6300-00-00-80:29/10/2026:5";
        // Lot gear char is '4': without the catalog it is unknown ...
        var withoutCatalog = YmvnGearQuantityValidator.ValidateScan(
            fcc, Order(("B5V-E6300-00-00-80", "Gear D :200pcs", 200)), Scans(), out var err1);
        Assert.False(withoutCatalog);
        Assert.Contains("Gear", err1);

        // ... and resolved through B20Gear when the catalog is supplied (order text "Gear D :200pcs" -> D).
        var withCatalog = YmvnGearQuantityValidator.ValidateScan(
            fcc, Order(("B5V-E6300-00-00-80", "Gear D :200pcs", 200)), Scans(), out var err2,
            code => code == 4 ? "Gear D" : "");
        Assert.True(withCatalog, err2);
    }
}
