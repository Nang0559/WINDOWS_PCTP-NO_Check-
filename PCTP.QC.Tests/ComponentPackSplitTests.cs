using System;
using System.Linq;
using PCTP.Modules.KhoLinhKien;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Chia số lượng đặt thành thùng (SL / thùng) và palet (MaxCloseQty) như usp_FVN_ComponentPO_Release / _PalletBuild.</summary>
public sealed class ComponentPackSplitTests
{
    [Fact]
    public void Order_is_split_into_full_cartons_and_a_remainder_carton()
    {
        var r = ComponentPackSplit.Plan(7300m, 2400m, null);

        Assert.Equal(new[] { 2400m, 2400m, 2400m, 100m }, r.Cartons.ToArray());
        Assert.Equal(7300m, r.Cartons.Sum());
    }

    [Fact]
    public void Exact_multiple_has_no_remainder_carton()
    {
        var r = ComponentPackSplit.Plan(7200m, 2400m, null);

        Assert.Equal(new[] { 2400m, 2400m, 2400m }, r.Cartons.ToArray());
    }

    [Fact]
    public void Order_smaller_than_one_carton_is_one_carton_with_the_whole_quantity()
    {
        var r = ComponentPackSplit.Plan(500m, 2400m, 4800m);

        Assert.Equal(new[] { 500m }, r.Cartons.ToArray());
        Assert.Single(r.Pallets);
        Assert.Equal(500m, r.Pallets[0].Qty);
    }

    [Fact]
    public void Pallets_group_consecutive_cartons_up_to_MaxCloseQty()
    {
        var r = ComponentPackSplit.Plan(10000m, 2400m, 4800m);   // cartons 2400,2400,2400,2400,400

        Assert.Equal(new[] { 2, 2, 1 }, r.Pallets.Select(p => p.CartonCount).ToArray());
        Assert.Equal(new[] { 4800m, 4800m, 400m }, r.Pallets.Select(p => p.Qty).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, r.Pallets.Select(p => p.Seq).ToArray());
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void MaxCloseQty_that_is_not_a_multiple_warns_and_uses_whole_cartons()
    {
        var r = ComponentPackSplit.Plan(9600m, 2400m, 5000m);

        Assert.Equal(new[] { 4800m, 4800m }, r.Pallets.Select(p => p.Qty).ToArray());
        Assert.Single(r.Warnings);
        Assert.Contains("bội số", r.Warnings[0]);
    }

    [Fact]
    public void Missing_MaxCloseQty_gives_no_pallets_and_a_warning()
    {
        var r = ComponentPackSplit.Plan(7200m, 2400m, null);

        Assert.Empty(r.Pallets);
        Assert.Single(r.Warnings);
    }

    [Fact]
    public void MaxCloseQty_below_pack_qty_puts_each_carton_on_its_own_pallet()
    {
        var r = ComponentPackSplit.Plan(4800m, 2400m, 1000m);

        Assert.Equal(2, r.Pallets.Count);
        Assert.Contains("nhỏ hơn", r.Warnings[0]);
    }

    [Fact]
    public void More_than_5000_cartons_for_one_line_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => ComponentPackSplit.Plan(5001m, 1m, null));
        Assert.Equal(5000, ComponentPackSplit.Plan(5000m, 1m, null).Cartons.Count);
    }

    [Fact]
    public void Non_positive_inputs_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ComponentPackSplit.Plan(0m, 10m, null));
        Assert.Throws<ArgumentException>(() => ComponentPackSplit.Plan(10m, 0m, null));
        Assert.Throws<ArgumentException>(() => ComponentPackSplit.Plan(10m, 5m, 0m));
    }

    [Theory]
    [InlineData(2400, 4800, 2)]
    [InlineData(2400, 5000, 2)]
    [InlineData(2400, 1000, 1)]
    [InlineData(2400, null, 0)]
    public void CartonsPerPallet_matches_the_pallet_build_rule(int pack, int? max, int expected)
    {
        Assert.Equal(expected, ComponentPackSplit.CartonsPerPallet(pack, max));
    }
}
