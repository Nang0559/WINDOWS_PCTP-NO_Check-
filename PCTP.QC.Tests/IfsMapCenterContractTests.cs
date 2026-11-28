using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 085 (map Kho IFS ↔ FVN_Warehouse, hub), SiteContext theo phiên và nhóm menu "Đồng bộ & Map IFS".</summary>
public sealed class IfsMapCenterContractTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, relative));
    private static string Migration() => Read("085_IFS_WarehouseMapAndMapCenter.sql");

    [Fact]
    public void Migration_follows_084_registers_itself_and_has_no_destructive_statement()
    {
        var sql = Migration();
        Assert.Contains("MigrationVersion = '084'", sql);
        Assert.Contains("VALUES ('085'", sql);
        Assert.DoesNotContain("CREATE OR ALTER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warehouse_is_mapped_to_the_ifs_warehouse_of_the_same_site_and_one_to_one()
    {
        var sql = Migration();
        Assert.Contains("ADD IfsWarehouseCode", sql);
        Assert.Contains("FOREIGN KEY (SiteCode, IfsWarehouseCode) REFERENCES MES.FVN_IfsWarehouse (SiteCode, WarehouseCode)", sql);
        Assert.Contains("UX_FVN_Warehouse_IfsWarehouse", sql);
        Assert.Contains("THROW 57859", sql);
    }

    [Fact]
    public void Slot_assignment_must_match_the_mapped_ifs_warehouse_but_unmapped_warehouses_keep_working()
    {
        var sql = Migration();
        Assert.Contains("THROW 57851", sql);
        Assert.Contains("IF @WhIfs IS NOT NULL AND @LocWh <> @WhIfs", sql);
        // READY logic of 083 is untouched: the warehouse state is reported in separate trailing columns.
        Assert.Contains("AS WarehouseMapStatus", sql);
        Assert.Contains("ELSE 'READY' END AS MapStatus", sql);
    }

    [Fact]
    public void Hub_view_covers_every_stage_of_the_chain()
    {
        var sql = Migration();
        foreach (var stage in new[] { "'SITE'", "'WORK_CENTER'", "'PRODUCTION_LINE'", "'WAREHOUSE'", "'LOCATION'", "'SLOT'" })
            Assert.Contains(stage, sql);
        Assert.Contains("'MES.IFS_MAP_CENTER'", sql);
        Assert.Contains("'WAREHOUSE.WAREHOUSE_IFS_MAP'", sql);
    }

    [Fact]
    public void Site_context_falls_back_to_the_session_site_instead_of_failing_on_empty_config()
    {
        var cs = Read("src/ISiteContext.cs");
        Assert.Contains("class ActiveSiteStore", cs);
        Assert.Contains("ActiveSiteStore.Current", cs);
        // still fail-closed when neither config nor session gives a Site
        Assert.Contains("throw new InvalidOperationException", cs);
    }

    [Fact]
    public void Site_is_resolved_on_demand_and_never_asked_when_the_app_opens()
    {
        var cs = Read("src/ISiteContext.cs");
        Assert.Contains("Func<string, string> Resolver", cs);
        Assert.Contains("ActiveSiteStore.Resolve(", cs);

        var menu = Read("src/Main_APP.Menu.cs");
        int ensure = menu.IndexOf("private void EnsureActiveSite", System.StringComparison.Ordinal);
        int next = menu.IndexOf("private string ResolveSiteOnDemand", ensure, System.StringComparison.Ordinal);
        Assert.True(ensure >= 0 && next > ensure);
        string body = menu.Substring(ensure, next - ensure);
        Assert.DoesNotContain("PickSite(", body);   // no Site dialog at startup
        Assert.Contains("ActiveSiteStore.Resolver = ResolveSiteOnDemand", body);
        Assert.DoesNotContain("_documentSiteChoice", menu);   // one Site source, not one per function
    }

    [Fact]
    public void Site_is_not_bound_to_the_machine_but_to_the_account_scope()
    {
        // Machines are shared by several factories: the PctpSiteCode app setting must not decide the Site any more.
        var cs = Read("src/ISiteContext.cs");
        Assert.DoesNotContain("ConfigurationManager.AppSettings[SettingKey]", cs);
        var menu = Read("src/Main_APP.Menu.cs");
        Assert.DoesNotContain("AppSettings[AppSettingsSiteContext.SettingKey]", menu);
        // the user can see and change the working Site (within the allowed Sites) from the status bar
        var shell = Read("Main_APP.source.cs");
        Assert.Contains("barSiteStatus", shell);
        Assert.Contains("DocumentSiteResolver.Candidates", shell);
    }

    [Fact]
    public void Ifs_related_menus_live_in_one_group_and_are_gone_from_the_old_groups()
    {
        var cs = Read("src/Main_APP.Menu.cs");
        foreach (var item in new[] { "menuIfsMapCenter", "menuIfsMasterDataSync", "menuKhaiBaoSite", "menuMaBoPhan4", "menuMaChuyen5Va3",
                                     "menuLienKetWorkCenter", "menuKhaiBaoChuyenSanXuat", "menuWarehouseIfsMap", "menuSlotMesLocation" })
        {
            Assert.Contains("_menuIfsMap.Elements.Add(CreateMenuItem(\"" + item + "\"", cs);
            Assert.DoesNotContain("_menuSanXuat.Elements.Add(CreateMenuItem(\"" + item + "\"", cs);
            Assert.DoesNotContain("_menuQuanTri.Elements.Add(CreateMenuItem(\n                \"" + item + "\"", cs);
            Assert.DoesNotContain("_menuLinhKien.Elements.Add(CreateMenuItem(\"" + item + "\"", cs);
        }
    }
}
