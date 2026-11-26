using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PCTP.Modules.KhoLinhKien;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// File Bravo "đặt hàng" gồm sheet header (vB30BizDoc_Edit) + sheet detail (vB30BizDocDetail_Edit).
/// Mapper biến chúng thành PO header + detail mà không phụ thuộc culture máy và không đoán sai ngày.
/// </summary>
public sealed class ComponentPoBravoMapperTests
{
    private const string FileName = "Bravo_dat_hang_2026-09-29_12-28-52.xlsx";
    private static readonly DateTime FileTime = new DateTime(2026, 11, 1, 8, 0, 0);

    private static BravoRawRow Header(int row, object customer = null, string site = "VN2W",
        string employee = "VIENPT", string description = "TEM JKVN TỪ 1→15.OCT.26")
    {
        return new BravoRawRow(row, new Dictionary<string, object>
        {
            { "CustomerCode", customer ?? 100016d },
            { "EmployeeCode", employee },
            { "Description", description },
            { "SiteCode", site }
        });
    }

    private static BravoRawRow Detail(int row, string item, object date, object qty, object customer = null)
    {
        return new BravoRawRow(row, new Dictionary<string, object>
        {
            { "ItemCode", item },
            { "RequestDate", date },
            { "Quantity9", qty },
            { "CustomerCode", customer ?? 100016d }
        });
    }

    private static BravoRawRow Blank(int row)
    {
        return new BravoRawRow(row, new Dictionary<string, object>
        {
            { "ItemCode", null }, { "RequestDate", null }, { "Quantity9", null }, { "CustomerCode", null }
        });
    }

    private static List<BravoPoDocument> Build(IList<BravoRawRow> header, IList<BravoRawRow> detail,
        string fileName = FileName)
    {
        return BravoPoMapper.Build(header, detail, fileName, FileTime);
    }

    [Fact]
    public void One_header_and_its_details_become_one_po_with_day_first_dates()
    {
        var docs = Build(
            new[] { Header(3) },
            new[]
            {
                Detail(3, "23123-KFL-8501", "01/10/2026", 4800d),
                Detail(4, "23123-KFL-8501", "13/10/2026", 7200d),
                Detail(5, "90202-KN7-6702-JK", "02/10/2026", 8000d)
            });

        BravoPoDocument po = Assert.Single(docs);
        Assert.Equal("BRV-20260929-122852", po.PurchaseOrderNo);
        Assert.Equal(new DateTime(2026, 9, 29), po.OrderDate);
        Assert.Equal("100016", po.SupplierCode);
        Assert.Equal("VN2W", po.SiteCode);
        Assert.Equal("TEM JKVN TỪ 1→15.OCT.26 | Lập: VIENPT", po.Remarks);

        Assert.Equal(new[] { 1, 2, 3 }, po.Lines.Select(l => l.LineNo).ToArray());
        // 01/10/2026 là mùng 1 tháng 10, không phải 10 tháng 1.
        Assert.Equal(new DateTime(2026, 10, 1), po.Lines[0].NeedByDate);
        Assert.Equal(new DateTime(2026, 10, 13), po.Lines[1].NeedByDate);
        Assert.Equal(new DateTime(2026, 10, 2), po.Lines[2].NeedByDate);
        Assert.Equal(20000m, po.TotalQty);
        Assert.Equal(2, po.PartCount);
        Assert.Equal(new DateTime(2026, 10, 1), po.MinNeedBy);
        Assert.Equal(new DateTime(2026, 10, 13), po.MaxNeedBy);
    }

    [Fact]
    public void Same_part_on_several_dates_stays_as_separate_lines_in_file_order()
    {
        var docs = Build(
            new[] { Header(3) },
            new[]
            {
                Detail(3, "A-1", "05/10/2026", 10d),
                Detail(4, "B-1", "05/10/2026", 20d),
                Detail(5, "A-1", "06/10/2026", 30d)
            });

        var lines = docs[0].Lines;
        Assert.Equal(new[] { "A-1", "B-1", "A-1" }, lines.Select(l => l.PartNo).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, lines.Select(l => l.LineNo).ToArray());
    }

    [Fact]
    public void Blank_formatted_rows_at_the_end_of_the_sheet_are_ignored()
    {
        var docs = Build(
            new[] { Header(3), Blank(4) },
            new[] { Detail(3, "A-1", "05/10/2026", 10d), Blank(4), Blank(5), Blank(6) });

        Assert.Single(docs);
        Assert.Single(docs[0].Lines);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("vi-VN")]
    [InlineData("en-GB")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("th-TH")]
    public void Text_dates_do_not_depend_on_the_machine_culture(string cultureName)
    {
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo ci;
            try { ci = new CultureInfo(cultureName); }
            catch (CultureNotFoundException) { return; }
            CultureInfo.CurrentCulture = ci;

            Assert.True(BravoPoMapper.TryParseDate("01/10/2026", out DateTime d1), cultureName);
            Assert.Equal(new DateTime(2026, 10, 1), d1);
            Assert.True(BravoPoMapper.TryParseDate("13/10/2026", out DateTime d2), cultureName);
            Assert.Equal(new DateTime(2026, 10, 13), d2);
            Assert.True(BravoPoMapper.TryParseDate("2026-10-15", out DateTime d3), cultureName);
            Assert.Equal(new DateTime(2026, 10, 15), d3);
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    [Fact]
    public void Date_cells_may_also_be_real_dates_or_excel_serial_numbers()
    {
        Assert.True(BravoPoMapper.TryParseDate(new DateTime(2026, 10, 5, 13, 0, 0), out DateTime a));
        Assert.Equal(new DateTime(2026, 10, 5), a);
        Assert.True(BravoPoMapper.TryParseDate(new DateTime(2026, 10, 5).ToOADate(), out DateTime b));
        Assert.Equal(new DateTime(2026, 10, 5), b);
    }

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("2026/10/05x")]
    [InlineData("")]
    [InlineData("hôm nay")]
    public void Invalid_text_dates_are_rejected_instead_of_guessed(string text)
    {
        Assert.False(BravoPoMapper.TryParseDate(text, out _));
    }

    [Fact]
    public void Invalid_request_date_names_the_sheet_row()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new[] { Header(3) },
            new[] { Detail(7, "A-1", "31/02/2026", 10d) }));
        Assert.Contains("dòng 7", ex.Message);
        Assert.Contains("RequestDate", ex.Message);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    public void Quantity_must_be_greater_than_zero(double qty)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new[] { Header(3) },
            new[] { Detail(9, "A-1", "05/10/2026", qty) }));
        Assert.Contains("dòng 9", ex.Message);
        Assert.Contains("Quantity9", ex.Message);
    }

    [Fact]
    public void Detail_for_a_supplier_without_header_is_rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new[] { Header(3) },
            new[] { Detail(3, "A-1", "05/10/2026", 10d, customer: 999999d) }));
        Assert.Contains("999999", ex.Message);
    }

    [Fact]
    public void Header_without_any_detail_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => Build(new[] { Header(3) }, new BravoRawRow[0]));
    }

    [Fact]
    public void Two_header_rows_for_the_same_supplier_are_ambiguous()
    {
        Assert.Throws<InvalidOperationException>(() => Build(
            new[] { Header(3), Header(4) },
            new[] { Detail(3, "A-1", "05/10/2026", 10d) }));
    }

    [Fact]
    public void Several_suppliers_get_one_po_each_with_the_supplier_in_the_number()
    {
        var docs = Build(
            new[] { Header(3, customer: 100016d), Header(4, customer: "100017") },
            new[]
            {
                Detail(3, "A-1", "05/10/2026", 10d, customer: 100016d),
                Detail(4, "B-1", "06/10/2026", 20d, customer: "100017")
            });

        Assert.Equal(2, docs.Count);
        Assert.Equal("BRV-20260929-122852-100016", docs[0].PurchaseOrderNo);
        Assert.Equal("BRV-20260929-122852-100017", docs[1].PurchaseOrderNo);
        Assert.Equal(1, docs[0].Lines[0].LineNo);
        Assert.Equal(1, docs[1].Lines[0].LineNo);
    }

    [Fact]
    public void Po_number_and_date_fall_back_to_file_time_when_the_name_has_no_stamp()
    {
        var docs = Build(new[] { Header(3) }, new[] { Detail(3, "A-1", "05/10/2026", 10d) }, "dat_hang.xlsx");
        Assert.Equal("BRV-20261101-080000", docs[0].PurchaseOrderNo);
        Assert.Equal(new DateTime(2026, 11, 1), docs[0].OrderDate);
    }

    [Fact]
    public void Same_file_name_gives_the_same_po_number_so_a_draft_reupload_replaces_itself()
    {
        var a = Build(new[] { Header(3) }, new[] { Detail(3, "A-1", "05/10/2026", 10d) });
        var b = Build(new[] { Header(3) }, new[] { Detail(3, "A-1", "05/10/2026", 10d) }, FileName);
        Assert.Equal(a[0].PurchaseOrderNo, b[0].PurchaseOrderNo);
    }

    [Fact]
    public void Supplier_code_in_a_numeric_cell_has_no_decimal_part()
    {
        Assert.Equal("100016", BravoPoMapper.AsText(100016d));
        Assert.Equal("100016", BravoPoMapper.AsText(" 100016 "));
        Assert.Null(BravoPoMapper.AsText("   "));
    }
}
