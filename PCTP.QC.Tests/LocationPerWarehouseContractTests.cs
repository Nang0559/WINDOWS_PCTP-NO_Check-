using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 086: Location IFS chọn ở cấp Kho; IFS không có Rack / Slot nên Slot chỉ gán vùng và kế thừa Location của Kho.</summary>
public sealed class LocationPerWarehouseContractTests
{
    private static string Read(string relative) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, relative));
    private static string Migration() => Read("086_IFS_LocationPerWarehouse.sql");

    private static string Block(string sql, string header)
    {
        var start = sql.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, header);
        var rest = sql.Substring(start);
        var end = rest.IndexOf("\nGO", StringComparison.Ordinal);
        return end < 0 ? rest : rest.Substring(0, end);
    }

    [Fact]
    public void Migration_follows_085_registers_itself_and_is_not_destructive()
    {
        var sql = Migration();
        Assert.Contains("MigrationVersion = '085'", sql);
        Assert.Contains("VALUES ('086'", sql);
        Assert.DoesNotContain("CREATE OR ALTER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Location_belongs_to_the_warehouse_and_must_be_a_location_of_its_mapped_ifs_warehouse()
    {
        var sql = Migration();
        Assert.Contains("ADD IfsLocationNo", sql);
        Assert.Contains("FOREIGN KEY (SiteCode, IfsLocationNo) REFERENCES MES.FVN_IfsInventoryLocation (SiteCode, LocationNo)", sql);
        var set = Block(sql, "ALTER PROCEDURE MES.usp_FVN_Warehouse_SetLocation");
        Assert.Contains("THROW 57878", set);   // Kho not mapped to an IFS warehouse yet
        Assert.Contains("THROW 57880", set);   // Location of another IFS warehouse
        Assert.Contains("WarehouseCode = @IfsWh", set);
    }

    [Fact]
    public void Slot_assignment_sets_the_area_only_and_refuses_a_slot_level_location()
    {
        var sql = Migration();
        var assign = Block(sql, "ALTER PROCEDURE MES.usp_FVN_SlotMesLocation_Assign");
        Assert.Contains("@LocationNo nvarchar(100) = NULL", assign);
        Assert.Contains("THROW 57877", assign);
        Assert.Contains("UPDATE dbo.FVN_InventoryAreaSlot", assign);
        Assert.DoesNotContain("INSERT dbo.FVN_SlotMesLocation", assign);   // the Location row is projected from the Kho, never written per Slot here
        var clear = Block(sql, "ALTER PROCEDURE MES.usp_FVN_SlotMesLocation_Clear");
        Assert.Contains("UPDATE dbo.FVN_InventoryAreaSlot SET IsActive = 0", clear);
        Assert.DoesNotContain("FVN_SlotMesLocation SET", clear);
    }

    [Fact]
    public void Projection_keeps_the_table_every_consumer_reads_and_never_touches_unmapped_warehouses_in_bulk()
    {
        var sql = Migration();
        var sync = Block(sql, "ALTER PROCEDURE MES.usp_FVN_Warehouse_SyncSlotLocations");
        Assert.Contains("dbo.FVN_SlotMesLocation", sync);
        Assert.Contains("(@WarehouseId IS NULL AND w.IfsLocationNo IS NOT NULL) OR w.WarehouseId = @WarehouseId", sync);
        // retire first (unique index UX_FVN_SlotMesLocation_Primary), then re-activate / insert
        Assert.True(sync.IndexOf("SET IsPrimary = 0, IsActive = 0", StringComparison.Ordinal) < sync.IndexOf("INSERT dbo.FVN_SlotMesLocation", StringComparison.Ordinal));
        Assert.Contains("AFTER INSERT", Block(sql, "ALTER TRIGGER dbo.TR_FVN_Slot_InheritWarehouseLocation"));
        // READY logic of 083 is unchanged
        var view = Block(sql, "ALTER VIEW MES.vw_FVN_SlotMesLocationStatus");
        Assert.Contains("ELSE 'READY' END AS MapStatus", view);
        Assert.Contains("sm.IsPrimary = 1 AND sm.IsActive = 1", view);
    }

    [Fact]
    public void Changing_the_ifs_warehouse_drops_a_location_that_no_longer_fits_instead_of_blocking_on_derived_rows()
    {
        var map = Block(Migration(), "ALTER PROCEDURE MES.usp_FVN_Warehouse_MapIfs");
        Assert.DoesNotContain("THROW 57860", map);
        Assert.Contains("loc.WarehouseCode = @IfsWarehouseCode", map);
        Assert.Contains("usp_FVN_Warehouse_SyncSlotLocations", map);
    }

    [Fact]
    public void Screens_choose_the_location_per_warehouse_and_the_slot_screen_has_no_location_picker()
    {
        var wh = Read("FormWarehouseIfsMap.source.cs");
        Assert.Contains("PickIfsLocation", wh);
        Assert.Contains("SetWarehouseLocation", wh);
        var repo = Read("IfsMapCenterRepository.source.cs");
        Assert.Contains("MES.usp_FVN_Warehouse_SetLocation", repo);
        Assert.Contains("MES.usp_FVN_Warehouse_SyncSlotLocations", repo);
        var slot = Read("FormSlotMesLocation.source.cs");
        Assert.DoesNotContain("LookUpEdit", slot);
        Assert.DoesNotContain("Gán vùng + Location", slot);
    }
}
