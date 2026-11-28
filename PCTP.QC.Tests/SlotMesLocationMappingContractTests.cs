using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 083: có UI + proc ghi Slot ↔ MES Location / vùng PART-WIP mà màn nhập kho linh kiện cần.</summary>
public sealed class SlotMesLocationMappingContractTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));
    private static string Migration() => Read("083_MES_SlotMesLocationMapping.sql");

    [Fact]
    public void Migration_follows_082_without_the_one_statement_create_form()
    {
        var sql = Migration();
        Assert.Contains("MigrationVersion = '082'", sql);
        Assert.Contains("VALUES ('083'", sql);
        Assert.DoesNotContain("CREATE OR ALTER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assign_sets_one_area_and_one_primary_location_from_the_ifs_list()
    {
        var sql = Migration();
        var assign = sql.Substring(sql.IndexOf("ALTER PROCEDURE MES.usp_FVN_SlotMesLocation_Assign", StringComparison.Ordinal));
        assign = assign.Substring(0, assign.IndexOf("\nGO", StringComparison.Ordinal));
        Assert.Contains("@AreaCode NOT IN ('PART', 'WIP')", assign);
        Assert.Contains("FROM MES.FVN_IfsInventoryLocation WHERE SiteCode = @SiteCode AND LocationNo = @LocationNo AND IsActive = 1", assign);
        Assert.Contains("THROW 57839", assign);
        Assert.Contains("UPDATE dbo.FVN_InventoryAreaSlot", assign);
        Assert.Contains("INSERT dbo.FVN_SlotMesLocation", assign);
        // the old primary location is retired before the new one is set (unique index UX_FVN_SlotMesLocation_Primary)
        Assert.True(assign.IndexOf("LocationNo <> @LocationNo", StringComparison.Ordinal) < assign.IndexOf("INSERT dbo.FVN_SlotMesLocation", StringComparison.Ordinal));
    }

    [Fact]
    public void Status_view_matches_the_conditions_used_by_the_receiving_slot_lookup()
    {
        var sql = Migration();
        var view = sql.Substring(sql.IndexOf("ALTER VIEW MES.vw_FVN_SlotMesLocationStatus", StringComparison.Ordinal));
        view = view.Substring(0, view.IndexOf("\nGO", StringComparison.Ordinal));
        Assert.Contains("sm.IsPrimary = 1 AND sm.IsActive = 1", view);
        Assert.Contains("ar.IsActive = 1", view);
        Assert.Contains("'READY'", view);
        Assert.Contains("'NO_LOCATION'", view);
        var lookup = Read("ComponentSlotLookupRepository.source.cs");
        Assert.Contains("sm.IsPrimary=1 AND sm.IsActive=1", lookup);
        Assert.Contains("ar.AreaCode='PART' AND ar.IsActive=1", lookup);
    }

    [Fact]
    public void Function_is_registered_for_view_and_edit_by_site()
    {
        var sql = Migration();
        Assert.Contains("'WAREHOUSE.SLOT_MES_LOCATION'", sql);
        Assert.Contains("p.FunctionCode = 'MES.SITE_DEPARTMENT_LINE' AND p.ActionCode IN ('VIEW', 'EDIT')", sql);
        var repo = Read("SlotMesLocationRepository.source.cs");
        Assert.Contains("AuthorizationGuard.DemandActionAndScope(FunctionCode, \"EDIT\", \"SITE\", site, site)", repo);
        Assert.Contains("usp_FVN_SlotMesLocation_Assign", repo);
    }

    [Fact]
    public void Screen_picks_location_from_a_list_and_receiving_form_can_open_it()
    {
        var form = Read("FormSlotMesLocation.source.cs");
        Assert.Contains("LookUpEdit", form);
        Assert.Contains("GetLocations", form);
        Assert.DoesNotContain("TextEdit _location", form);
        var scan = Read("FormKiemKeHangVeScan.source.cs");
        Assert.Contains("new FormSlotMesLocation(_siteCode)", scan);
        Assert.Contains("Map Slot ↔ Location", scan);
    }
}
