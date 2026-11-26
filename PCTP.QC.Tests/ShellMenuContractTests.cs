using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Source-level guards for the main-app navigation: every item is created once, the component-ordering screen carries
/// the Excel upload action, and the former duplicate MES root groups stay gone.
/// </summary>
public sealed class ShellMenuContractTests
{
    private static string Read(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));

    [Fact]
    public void Every_menu_item_name_is_created_exactly_once()
    {
        string menu = Read("Main_APP.Menu.internal-transfer.source.cs");
        var names = Regex.Matches(menu, "CreateMenuItem\\(\\s*\"(?<n>[A-Za-z0-9_]+)\"")
            .Cast<Match>().Select(m => m.Groups["n"].Value).ToList();
        Assert.NotEmpty(names);
        var duplicate = names.GroupBy(n => n, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        Assert.True(duplicate == null, duplicate == null ? "" : "Menu item created twice: " + duplicate.Key);
    }

    [Fact]
    public void Component_ordering_items_live_in_the_Linh_kien_group_in_flow_order()
    {
        string menu = Read("Main_APP.Menu.internal-transfer.source.cs");
        int group = menu.IndexOf("Name = \"menuLinhKien\"", StringComparison.Ordinal);
        Assert.True(group >= 0);
        int end = menu.IndexOf("_menuSanXuat = new", group, StringComparison.Ordinal);
        string block = menu.Substring(group, end - group);
        int[] order = new[] { "menuPhieuDatHangLinhKienUi", "menuNhanLinhKien", "menuKhoLinhKien",
                "menuChungTuYeuCauLinhKien", "menuYeuCauXuatLinhKien" }
            .Select(n => block.IndexOf("\"" + n + "\"", StringComparison.Ordinal)).ToArray();
        Assert.All(order, i => Assert.True(i >= 0));
        Assert.Equal(order.OrderBy(i => i).ToArray(), order);
    }

    [Fact]
    public void Component_po_screen_is_opened_with_the_upload_action()
    {
        string menu = Read("Main_APP.Menu.internal-transfer.source.cs");
        int open = menu.IndexOf("private void OpenPhieuDatHangLinhKienUi", StringComparison.Ordinal);
        Assert.True(open >= 0);
        string body = menu.Substring(open, Math.Min(1400, menu.Length - open));
        Assert.Contains("ComponentPoUploadAction.Create()", body);
    }

    [Fact]
    public void Former_duplicate_MES_root_groups_are_gone_and_admin_items_have_their_own_group()
    {
        string main = Read("Main_APP.source.cs");
        Assert.DoesNotContain("menuMesExecutionGroup", main);
        Assert.DoesNotContain("menuMesBomGroup", main);
        Assert.DoesNotContain("accordionControl.Elements.Add(", main.Substring(main.IndexOf("private void AddMesBomNavigation", StringComparison.Ordinal)));

        string menu = Read("Main_APP.Menu.internal-transfer.source.cs");
        int admin = menu.IndexOf("Name = \"menuQuanTri\"", StringComparison.Ordinal);
        Assert.True(admin >= 0);
        string block = menu.Substring(admin, 1500);
        foreach (var n in new[] { "menuSecurityAdmin", "menuIfsMasterDataSync", "menuDocumentDesigner", "menuUiDataSourceDesigner" })
            Assert.Contains("\"" + n + "\"", block);
    }
}
