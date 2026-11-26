using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PCTP.Framework.Documents;
using PCTP.Framework.UiLayout;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Migration 060 and its service rules: profile priority, delete profile, full screen catalogue.</summary>
public sealed class UiMenuProfileOrderDeleteTests
{
    private sealed class FakeAuth : IDocumentDesignerAuthorizer
    {
        public bool CanEdit = true;
        public long ActorUserId => 7;
        public string ActorCode => "tester";
        public void Demand(string actionCode)
        {
            if (actionCode == DocumentDesignerPermissions.Edit && !CanEdit) throw new UnauthorizedAccessException();
        }
        public bool Has(string actionCode) => actionCode != DocumentDesignerPermissions.Edit || CanEdit;
    }

    private sealed class FakeStore : IUiMenuStore
    {
        public readonly List<string> Calls = new List<string>();
        public IList<UiMenuItemInfo> Screens = new List<UiMenuItemInfo>();
        public IList<UiMenuProfileInfo> ListProfiles(long a) => new List<UiMenuProfileInfo>();
        public IList<UiMenuRole> ListRoles(long a) => new List<UiMenuRole>();
        public UiMenuProfile Load(long a, string c) => new UiMenuProfile { ProfileCode = c };
        public void CreateProfile(long a, string ac, string c, string n) { Calls.Add("create"); }
        public int Save(long a, string ac, UiMenuProfile p, int v) { Calls.Add("save"); return v + 1; }
        public int SetOrder(long a, string ac, string c, int v, int order) { Calls.Add("order:" + c + ":" + order); return v + 1; }
        public void DeleteProfile(long a, string ac, string c, int v) { Calls.Add("delete:" + c + ":" + v); }
        public IList<UiMenuItemInfo> ListScreenItems(long a) => Screens;
        public IList<UiMenuNode> LoadRuntime(long a) => new List<UiMenuNode>();
    }

    private static string Sql => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "060_MES_UiMenuProfileOrderDelete.sql"));

    [Fact]
    public void Order_range_is_1_to_9999_and_a_new_profile_starts_at_the_default()
    {
        Assert.True(UiMenuValidator.IsValidOrder(1));
        Assert.True(UiMenuValidator.IsValidOrder(9999));
        Assert.False(UiMenuValidator.IsValidOrder(0));
        Assert.False(UiMenuValidator.IsValidOrder(10000));
        Assert.Equal(UiMenuValidator.DefaultOrder, new UiMenuProfile().ProfileOrder);
    }

    [Fact]
    public void SetOrder_validates_range_and_refuses_DEFAULT_before_touching_the_store()
    {
        var store = new FakeStore();
        var service = new UiMenuDesignerService(store, new FakeAuth());
        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetOrder("WH", 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetOrder("WH", 1, 10000));
        Assert.Throws<InvalidOperationException>(() => service.SetOrder("DEFAULT", 1, 5));
        Assert.Empty(store.Calls);
        Assert.Equal(4, service.SetOrder("WH", 3, 5));
        Assert.Equal(new[] { "order:WH:5" }, store.Calls);
    }

    [Fact]
    public void Delete_refuses_DEFAULT_and_needs_the_edit_right()
    {
        var store = new FakeStore();
        var auth = new FakeAuth();
        var service = new UiMenuDesignerService(store, auth);
        Assert.Throws<InvalidOperationException>(() => service.Delete("DEFAULT", 1));
        service.Delete("WH", 2);
        Assert.Equal(new[] { "delete:WH:2" }, store.Calls);

        auth.CanEdit = false;
        Assert.Throws<UnauthorizedAccessException>(() => service.Delete("WH", 2));
        Assert.Throws<UnauthorizedAccessException>(() => service.SetOrder("WH", 2, 5));
        Assert.Single(store.Calls);
    }

    [Fact]
    public void Screen_catalogue_for_the_designer_leaves_out_the_screen_that_has_its_own_menu_item()
    {
        var store = new FakeStore
        {
            Screens = new List<UiMenuItemInfo>
            {
                new UiMenuItemInfo { ItemKey = UiMenuScreenKey.For("A_SCREEN"), Caption = "A" },
                new UiMenuItemInfo { ItemKey = UiMenuScreenKey.For(UiLayoutModule.ComponentPoScreenCode), Caption = "PO" }
            }
        };
        var items = new UiMenuDesignerService(store, new FakeAuth()).ListScreenItems();
        Assert.Equal(new[] { "menuUiScreen_A_SCREEN" }, items.Select(i => i.ItemKey).ToArray());
        Assert.True(items.All(i => UiMenuValidator.Validate("P", new List<UiMenuNode>
            { new UiMenuNode { NodeId = 1, NodeType = "ITEM", ItemKey = i.ItemKey, SequenceNo = 1 } }, null).Count == 0));
    }

    [Fact]
    public void Migration_060_uses_no_dynamic_sql_and_every_procedure_checks_rbac()
    {
        Assert.DoesNotContain("sp_executesql", Sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(Regex.IsMatch(Sql, @"\bEXEC(UTE)?\s*\(", RegexOptions.IgnoreCase));
        var bodies = Regex.Split(Sql, @"CREATE PROCEDURE\s+", RegexOptions.IgnoreCase).Skip(1).ToList();
        Assert.Equal(6, bodies.Count);
        foreach (var body in bodies)
        {
            string text = body.Split(new[] { "\nGO" }, StringSplitOptions.None)[0];
            if (text.StartsWith("MES.usp_FVN_UiMenuProfile_Runtime", StringComparison.Ordinal)) { Assert.Contains("SEC.UserAccount", text); continue; }
            Assert.Contains("fn_FVN_DocumentDesigner_HasAction(@ActorUserId, 'VIEW') = 0", text);
        }
    }

    [Fact]
    public void Order_and_delete_need_edit_are_versioned_and_audited_and_default_is_protected()
    {
        foreach (string name in new[] { "SetOrder", "Delete" })
        {
            string proc = Sql.Substring(Sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_" + name, StringComparison.Ordinal));
            proc = proc.Split(new[] { "\nGO" }, StringSplitOptions.None)[0];
            Assert.Contains("HasAction(@ActorUserId, 'EDIT') = 0", proc);
            Assert.Contains("59131", proc);
            Assert.Contains("FVN_UiScreenAudit", proc);
            Assert.Contains("'DEFAULT'", proc);
        }
    }

    [Fact]
    public void Runtime_picks_the_profile_by_ProfileOrder_not_alphabetically()
    {
        string runtime = Sql.Substring(Sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_Runtime", StringComparison.Ordinal));
        runtime = runtime.Split(new[] { "\nGO" }, StringSplitOptions.None)[0];
        Assert.Contains("ORDER BY p.ProfileOrder, p.ProfileCode", runtime);
        Assert.Contains("CK_MES_FVN_UiMenuProfile_Order", Sql);
    }

    [Fact]
    public void Screen_list_for_the_designer_does_not_filter_by_the_actors_data_rights()
    {
        string list = Sql.Substring(Sql.IndexOf("CREATE PROCEDURE MES.usp_FVN_UiMenuProfile_ListScreens", StringComparison.Ordinal));
        list = list.Split(new[] { "\nGO" }, StringSplitOptions.None)[0];
        Assert.DoesNotContain("fn_FVN_UiHasFunctionAction", list);
        Assert.Contains("sc.IsActive = 1", list);
    }
}
