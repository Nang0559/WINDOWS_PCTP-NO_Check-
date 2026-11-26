using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Framework.UiLayout;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>A menu profile can only arrange and rename existing shell items; bad or stale data must never break the shell.</summary>
public sealed class UiMenuProfileTests
{
    private static UiMenuNode G(int id, int? parent, string caption, int seq = 1) =>
        new UiMenuNode { NodeId = id, ParentNodeId = parent, NodeType = "GROUP", Caption = caption, SequenceNo = seq };

    private static UiMenuNode I(int id, int? parent, string key, int seq = 1, string caption = null) =>
        new UiMenuNode { NodeId = id, ParentNodeId = parent, NodeType = "ITEM", ItemKey = key, Caption = caption, SequenceNo = seq };

    private static ISet<string> Keys(params string[] keys) => new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Validator_accepts_a_nested_tree_with_roles()
    {
        var nodes = new List<UiMenuNode> { G(1, null, "Kho"), I(2, 1, "menuKhoTonTP"), G(3, 1, "Con"), I(4, 3, "menuHangChoGiao") };
        Assert.Empty(UiMenuValidator.Validate("WAREHOUSE", nodes, new List<string> { "WH_USER" }));
    }

    [Fact]
    public void Validator_rejects_duplicate_item_blank_group_unknown_parent_and_item_with_children()
    {
        Assert.NotEmpty(UiMenuValidator.Validate("P", new List<UiMenuNode> { I(1, null, "a"), I(2, null, "A") }, null));
        Assert.NotEmpty(UiMenuValidator.Validate("P", new List<UiMenuNode> { G(1, null, "  ") }, null));
        Assert.NotEmpty(UiMenuValidator.Validate("P", new List<UiMenuNode> { I(1, 99, "a") }, null));
        Assert.NotEmpty(UiMenuValidator.Validate("P", new List<UiMenuNode> { I(1, null, "a"), I(2, 1, "b") }, null));
    }

    [Fact]
    public void Validator_rejects_cycles_depth_over_three_and_unsafe_keys()
    {
        var cycle = new List<UiMenuNode> { G(1, 2, "A"), G(2, 1, "B") };
        Assert.NotEmpty(UiMenuValidator.Validate("P", cycle, null));

        var deep = new List<UiMenuNode> { G(1, null, "A"), G(2, 1, "B"), G(3, 2, "C"), I(4, 3, "x") };
        Assert.NotEmpty(UiMenuValidator.Validate("P", deep, null));

        Assert.NotEmpty(UiMenuValidator.Validate("P", new List<UiMenuNode> { I(1, null, "a'; DROP TABLE x;--") }, null));
        Assert.NotEmpty(UiMenuValidator.Validate("P", new List<UiMenuNode> { I(1, null, "a b") }, null));
    }

    [Fact]
    public void Validator_rejects_bad_profile_code_and_roles_on_DEFAULT()
    {
        Assert.NotEmpty(UiMenuValidator.Validate("bad code", new List<UiMenuNode>(), null));
        Assert.NotEmpty(UiMenuValidator.Validate("DEFAULT", new List<UiMenuNode>(), new List<string> { "R" }));
        Assert.Empty(UiMenuValidator.Validate("DEFAULT", new List<UiMenuNode>(), null));
    }

    [Fact]
    public void Resolver_orders_by_sequence_drops_unknown_items_and_empty_groups()
    {
        var nodes = new List<UiMenuNode>
        {
            G(1, null, "Sau", 2), I(2, 1, "gone"),                       // empty after dropping an unknown key
            G(3, null, "Truoc", 1), I(4, 3, "b", 2), I(5, 3, "a", 1, "Tên mới"),
            I(6, null, "root", 3)
        };
        var plan = UiMenuResolver.Resolve(nodes, Keys("a", "b", "root"));

        Assert.Equal(new[] { "Truoc", null }, plan.Select(p => p.IsGroup ? p.Caption : null).ToArray());
        Assert.Equal(2, plan.Count);
        Assert.Equal(new[] { "a", "b" }, plan[0].Children.Select(c => c.ItemKey).ToArray());
        Assert.Equal("Tên mới", plan[0].Children[0].Caption);
        Assert.Equal("root", plan[1].ItemKey);
    }

    [Fact]
    public void Resolver_uses_each_key_once_and_is_not_fooled_by_cycles()
    {
        var dup = new List<UiMenuNode> { G(1, null, "G1"), I(2, 1, "a"), G(3, null, "G2", 2), I(4, 3, "a") };
        var plan = UiMenuResolver.Resolve(dup, Keys("a"));
        Assert.Single(plan);                                              // second group became empty, so it vanished

        var cyc = new List<UiMenuNode> { G(1, 2, "A"), G(2, 1, "B"), I(3, 1, "a") };
        Assert.Empty(UiMenuResolver.Resolve(cyc, Keys("a")));            // unreachable from a root: nothing, no hang
    }

    [Fact]
    public void Resolver_returns_nothing_for_no_nodes_so_the_standard_menu_stays()
    {
        Assert.Empty(UiMenuResolver.Resolve(null, Keys("a")));
        Assert.Empty(UiMenuResolver.Resolve(new List<UiMenuNode>(), Keys("a")));
        Assert.Empty(UiMenuResolver.Resolve(new List<UiMenuNode> { I(1, null, "a") }, null));
    }
}
