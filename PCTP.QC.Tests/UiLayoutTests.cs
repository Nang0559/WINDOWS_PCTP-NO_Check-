using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Framework.UiLayout;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Layout validator and query builder: names only from the approved view catalogue, every value a parameter,
/// Site predicate always present, quick filters limited to column + operator + (closed token | typed literal).
/// </summary>
public sealed class UiLayoutTests
{
    private static UiSourceColumn C(string name, string group) =>
        new UiSourceColumn { ColumnName = name, TypeName = group, TypeGroup = group };

    private static UiColumnLayout L(string region, string name, int seq, bool visible = true, string align = "LEFT") =>
        new UiColumnLayout { RegionCode = region, ColumnName = name, Caption = name, SequenceNo = seq, IsVisible = visible, Align = align };

    private static (UiScreenLayout layout, Dictionary<string, List<UiSourceColumn>> catalog) Sample()
    {
        var catalog = new Dictionary<string, List<UiSourceColumn>>(StringComparer.OrdinalIgnoreCase)
        {
            [UiRegion.Main] = new List<UiSourceColumn> { C("PurchaseOrderId", "NUM"), C("SiteCode", "TEXT"), C("PurchaseOrderNo", "TEXT"), C("OrderDate", "DATE"), C("Status", "TEXT") },
            ["LINES"] = new List<UiSourceColumn> { C("PurchaseOrderId", "NUM"), C("SiteCode", "TEXT"), C("PartNo", "TEXT"), C("LineNo", "NUM") }
        };
        var layout = new UiScreenLayout
        {
            ScreenCode = "COMPONENT_PO", Title = "PO", SourceCode = "COMPONENT_PO", ViewName = "MES.vw_FVN_UiComponentPo",
            ViewFunctionCode = "WAREHOUSE.COMPONENT_PO", SiteColumn = "SiteCode", DefaultSortColumn = "OrderDate", DefaultSortDesc = true,
            GroupColumn = "Status", PageSize = 100, ConfigVersion = 3
        };
        layout.Columns.AddRange(new[]
        {
            L(UiRegion.Main, "PurchaseOrderId", 1), L(UiRegion.Main, "SiteCode", 2, false), L(UiRegion.Main, "PurchaseOrderNo", 3),
            L(UiRegion.Main, "OrderDate", 4), L(UiRegion.Main, "Status", 5),
            L("LINES", "PurchaseOrderId", 1, false), L("LINES", "SiteCode", 2, false), L("LINES", "PartNo", 3), L("LINES", "LineNo", 4)
        });
        layout.Children.Add(new UiChildLayout
        {
            ChildCode = "LINES", TabCaption = "Chi tiết", SourceCode = "COMPONENT_PO_LINE", ViewName = "MES.vw_FVN_UiComponentPoLine",
            ViewFunctionCode = "WAREHOUSE.COMPONENT_PO", SiteColumn = "SiteCode", ParentColumn = "PurchaseOrderId",
            ChildColumn = "PurchaseOrderId", SortColumn = "LineNo", SequenceNo = 1
        });
        layout.Filters.Add(new UiQuickFilter { FilterCode = "TODAY", Caption = "Hôm nay", ColumnName = "OrderDate", OperatorCode = "GE", TokenCode = "TODAY", SequenceNo = 1 });
        layout.Filters.Add(new UiQuickFilter { FilterCode = "OPEN", Caption = "Đang xử lý", ColumnName = "Status", OperatorCode = "NE", LiteralValue = "CLOSED", SequenceNo = 2, IsDefault = true });
        return (layout, catalog);
    }

    [Fact]
    public void A_consistent_layout_has_no_errors()
    {
        var (layout, catalog) = Sample();
        Assert.Empty(UiLayoutValidator.Validate(layout, catalog));
    }

    [Fact]
    public void A_column_missing_from_the_approved_view_is_rejected()
    {
        var (layout, catalog) = Sample();
        layout.Columns.Add(L(UiRegion.Main, "Secret; DROP TABLE x", 9));
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("không có trong view"));
    }

    [Fact]
    public void Every_region_keeps_at_least_one_visible_column()
    {
        var (layout, catalog) = Sample();
        foreach (var c in layout.ColumnsOf("LINES")) c.IsVisible = false;
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("ít nhất một cột hiển thị"));
    }

    [Fact]
    public void Parent_and_child_key_must_share_a_type_group()
    {
        var (layout, catalog) = Sample();
        layout.Children[0].ChildColumn = "PartNo";
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("cùng nhóm kiểu"));
    }

    [Fact]
    public void Filter_operator_token_and_literal_must_suit_the_column_type()
    {
        var (layout, catalog) = Sample();
        layout.Filters[0].ColumnName = "Status";          // date token on a text column
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("token ngày"));

        (layout, catalog) = Sample();
        layout.Filters[1].OperatorCode = "CONTAINS";
        layout.Filters[1].ColumnName = "OrderDate";       // CONTAINS on a date
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("CONTAINS"));

        (layout, catalog) = Sample();
        layout.Filters[0].TokenCode = "DROP_TABLE";       // token outside the closed list
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("danh sách cho phép"));

        (layout, catalog) = Sample();
        layout.Filters[0].LiteralValue = "2026-01-01";    // token and literal together
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("đúng một"));
    }

    [Fact]
    public void Only_one_default_filter_is_allowed()
    {
        var (layout, catalog) = Sample();
        layout.Filters[0].IsDefault = true;
        Assert.Contains(UiLayoutValidator.Validate(layout, catalog), e => e.Contains("một bộ lọc mặc định"));
    }

    [Fact]
    public void Main_page_is_site_scoped_parameterized_and_paged()
    {
        var (layout, catalog) = Sample();
        var cmd = UiQueryBuilder.BuildMainPage(layout, catalog, "VN1", layout.Filters[1], new DateTime(2026, 11, 26), 2);

        Assert.StartsWith("SELECT [PurchaseOrderId]", cmd.Sql);
        Assert.Contains("FROM [MES].[vw_FVN_UiComponentPo] WHERE [SiteCode] = @Site AND [Status] <> @f0", cmd.Sql);
        Assert.Contains("ORDER BY [OrderDate] DESC, [PurchaseOrderId] OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY", cmd.Sql);
        Assert.DoesNotContain("CLOSED", cmd.Sql);                       // values never inline
        Assert.Equal("VN1", Value(cmd, "@Site"));
        Assert.Equal("CLOSED", Value(cmd, "@f0"));
        Assert.Equal(200, Value(cmd, "@Skip"));                         // page 2 * PageSize 100
        Assert.Equal(100, Value(cmd, "@Take"));
    }

    [Fact]
    public void Date_tokens_resolve_on_the_client_into_date_parameters()
    {
        var (layout, catalog) = Sample();
        var today = new DateTime(2026, 11, 26, 14, 30, 0);
        Assert.Equal(new DateTime(2026, 11, 26), Value(UiQueryBuilder.BuildMainCount(layout, catalog, "VN1", layout.Filters[0], today), "@f0"));
        Assert.Equal(new DateTime(2026, 11, 1), UiQueryBuilder.ResolveToken("MONTH_START", today, "VN1"));
        Assert.Equal(new DateTime(2026, 1, 1), UiQueryBuilder.ResolveToken("YEAR_START", today, "VN1"));
        Assert.Equal("VN1", UiQueryBuilder.ResolveToken("SITE", today, "VN1"));
        Assert.Throws<ArgumentException>(() => UiQueryBuilder.ResolveToken("1;DROP", today, "VN1"));
    }

    [Fact]
    public void Contains_filter_escapes_like_wildcards()
    {
        var (layout, catalog) = Sample();
        var filter = new UiQuickFilter { FilterCode = "F1", Caption = "x", ColumnName = "PurchaseOrderNo", OperatorCode = "CONTAINS", LiteralValue = "50%_[a" };
        var cmd = UiQueryBuilder.BuildMainCount(layout, catalog, "VN1", filter, DateTime.Today);
        Assert.Contains("LIKE @f0 ESCAPE '\\'", cmd.Sql);
        Assert.Equal("%50\\%\\_\\[a%", Value(cmd, "@f0"));
    }

    [Fact]
    public void Site_is_mandatory_for_main_and_child_reads()
    {
        var (layout, catalog) = Sample();
        Assert.Throws<ArgumentException>(() => UiQueryBuilder.BuildMainCount(layout, catalog, " ", null, DateTime.Today));
        var cmd = UiQueryBuilder.BuildChild(layout, layout.Children[0], catalog, "VN1", 42L);
        Assert.Contains("WHERE [PurchaseOrderId] = @Key AND [SiteCode] = @Site", cmd.Sql);
        Assert.StartsWith("SELECT TOP (2000)", cmd.Sql);
        Assert.Equal(42L, Value(cmd, "@Key"));
    }

    [Fact]
    public void Builder_refuses_a_column_that_is_not_in_the_catalogue()
    {
        var (layout, catalog) = Sample();
        layout.Columns.Add(L(UiRegion.Main, "NotInView", 9));
        Assert.Throws<InvalidOperationException>(() => UiQueryBuilder.BuildMainPage(layout, catalog, "VN1", null, DateTime.Today, 0));
    }

    private static object Value(UiSqlCommand cmd, string name) => cmd.Parameters.First(p => p.Key == name).Value;
}
