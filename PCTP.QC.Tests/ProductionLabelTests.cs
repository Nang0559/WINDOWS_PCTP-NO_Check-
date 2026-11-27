using System;
using System.Data;
using PCTP.Modules.MesPlanning;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Mã trạng thái kế hoạch / lệnh sản xuất và dữ liệu hai tem thành phẩm (migration 065 / 066).</summary>
public sealed class ProductionStatusCodesTests
{
    [Theory]
    [InlineData("IMPORTED", 0)]
    [InlineData("validated", 10)]
    [InlineData(" FIRM ", 20)]
    [InlineData("CANCELLED", 900)]
    public void Plan_status_name_maps_to_workflow_code(string name, int expected)
    {
        Assert.True(ProductionPlanStatus.TryFromName(name, out int code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("CREATED", 0)]
    [InlineData("RELEASED", 10)]
    [InlineData("running", 20)]
    [InlineData("PAUSED", 30)]
    [InlineData("COMPLETED", 40)]
    [InlineData("CLOSED", 50)]
    [InlineData("CANCELLED", 900)]
    public void Order_status_name_maps_to_workflow_code(string name, int expected)
    {
        Assert.True(ProductionOrderStatus.TryFromName(name, out int code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("DRAFT")]
    public void Unknown_status_name_is_rejected(string name)
    {
        Assert.False(ProductionPlanStatus.TryFromName(name, out _));
        Assert.False(ProductionOrderStatus.TryFromName(name, out _));
    }

    [Fact]
    public void Required_actions_match_ProductionWorkflowService()
    {
        Assert.Equal("CONFIRM", ProductionPlanStatus.RequiredAction(ProductionPlanStatus.Validated));
        Assert.Equal("APPROVE", ProductionPlanStatus.RequiredAction(ProductionPlanStatus.Firm));
        Assert.Equal("CANCEL", ProductionPlanStatus.RequiredAction(ProductionPlanStatus.Cancelled));
        Assert.Equal("APPROVE", ProductionOrderStatus.RequiredAction(ProductionOrderStatus.Released));
        Assert.Equal("CONFIRM", ProductionOrderStatus.RequiredAction(ProductionOrderStatus.Running));
        Assert.Equal("CONFIRM", ProductionOrderStatus.RequiredAction(ProductionOrderStatus.Paused));
        Assert.Equal("APPROVE", ProductionOrderStatus.RequiredAction(ProductionOrderStatus.Completed));
        Assert.Equal("APPROVE", ProductionOrderStatus.RequiredAction(ProductionOrderStatus.Closed));
        Assert.Equal("CANCEL", ProductionOrderStatus.RequiredAction(ProductionOrderStatus.Cancelled));
    }

    [Fact]
    public void Only_cancel_needs_a_reason()
    {
        Assert.True(ProductionPlanStatus.NeedsReason(ProductionPlanStatus.Validated, ProductionPlanStatus.Cancelled));
        Assert.False(ProductionPlanStatus.NeedsReason(ProductionPlanStatus.Imported, ProductionPlanStatus.Validated));
        Assert.True(ProductionOrderStatus.NeedsReason(ProductionOrderStatus.Released, ProductionOrderStatus.Cancelled));
        Assert.False(ProductionOrderStatus.NeedsReason(ProductionOrderStatus.Running, ProductionOrderStatus.Paused));
    }

    [Fact]
    public void Labels_are_built_for_released_to_completed_and_reprinted_also_when_closed()
    {
        foreach (int s in new[] { ProductionOrderStatus.Released, ProductionOrderStatus.Running, ProductionOrderStatus.Paused, ProductionOrderStatus.Completed })
        {
            Assert.True(ProductionOrderStatus.CanBuildLabels(s));
            Assert.True(ProductionOrderStatus.CanReprintLabels(s));
        }
        foreach (int s in new[] { ProductionOrderStatus.Created, ProductionOrderStatus.Closed, ProductionOrderStatus.Cancelled })
            Assert.False(ProductionOrderStatus.CanBuildLabels(s));
        Assert.True(ProductionOrderStatus.CanReprintLabels(ProductionOrderStatus.Closed));
        Assert.False(ProductionOrderStatus.CanReprintLabels(ProductionOrderStatus.Created));
        Assert.False(ProductionOrderStatus.CanReprintLabels(ProductionOrderStatus.Cancelled));
    }
}

public sealed class ProductionLabelDataTests
{
    private static DataTable Source(string kind, int qty)
    {
        var t = new DataTable();
        t.Columns.Add("Seq", typeof(int));
        t.Columns.Add("Qty", typeof(int));
        t.Columns.Add("ProductionDate", typeof(DateTime));
        t.Columns.Add("ShiftCode", typeof(string));
        t.Columns.Add("DepartmentText", typeof(string));
        t.Rows.Add(1, qty, new DateTime(2026, 11, 27), "1", "PD-2W");
        return t;
    }

    [Fact]
    public void Carton_matches_the_sample_label()
    {
        DataTable t = ProductionLabelData.PrepareCarton(Source("CARTON", 5));
        DataRow r = t.Rows[0];
        Assert.Equal("1", r["ShiftText"]);
        Assert.Equal("27/11/26", r["DateText"]);
        Assert.Equal("SL: 5", r["QtyText"]);
        Assert.Equal("(PD-2W)", r["DepartmentLine"]);
    }

    [Fact]
    public void Pallet_matches_the_sample_sheet()
    {
        DataTable t = ProductionLabelData.PreparePallet(Source("PALLET", 300));
        DataRow r = t.Rows[0];
        Assert.Equal("1", r["SeqText"]);
        Assert.Equal("27/11/2026", r["DateText"]);
        Assert.Equal("300", r["QtyText"]);
    }

    [Fact]
    public void Quantity_uses_thousands_separator_and_source_table_is_not_changed()
    {
        DataTable source = Source("PALLET", 1200);
        DataTable t = ProductionLabelData.PreparePallet(source);
        Assert.Equal("1,200", t.Rows[0]["QtyText"]);
        Assert.False(source.Columns.Contains("QtyText"));
    }

    [Theory]
    [InlineData("PD-2W", "(PD-2W)")]
    [InlineData("(PD-2W)", "(PD-2W)")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Department_line_gets_parentheses_once(string input, string expected)
    {
        Assert.Equal(expected, ProductionLabelData.DepartmentLine(input));
    }
}
