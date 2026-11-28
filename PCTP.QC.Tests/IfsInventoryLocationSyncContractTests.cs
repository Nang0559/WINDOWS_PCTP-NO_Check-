using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>Source-contract guards cho migration 084: nhóm INVENTORY_LOCATION đổ IFS location vào MES.FVN_IfsInventoryLocation cho màn Map Slot.</summary>
public sealed class IfsInventoryLocationSyncContractTests
{
    private static string Migration() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "084_IFS_InventoryLocationSync.sql"));

    [Fact]
    public void Migration_follows_083_and_registers_itself()
    {
        var sql = Migration();
        Assert.Contains("MigrationVersion = '083'", sql);
        Assert.Contains("VALUES ('084'", sql);
        Assert.DoesNotContain("CREATE OR ALTER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stage_constraint_accepts_the_new_entity_and_keeps_all_existing_ones()
    {
        var sql = Migration();
        foreach (var entity in new[] { "PART_CATALOG", "INVENTORY_PART", "BOM_HEADER", "BOM_ALTERNATIVE", "BOM_COMPONENT", "WAREHOUSE", "SUPPLIER", "PRODUCTION_LINE", "SITE", "WORK_CENTER", "INVENTORY_LOCATION" })
            Assert.Contains("'" + entity + "'", sql);
    }

    [Fact]
    public void Apply_requires_the_ifs_warehouse_and_never_deletes_locations()
    {
        var sql = Migration();
        Assert.Contains("THROW 57842", sql);
        Assert.Contains("FROM MES.FVN_IfsWarehouse", sql);
        Assert.Contains("MERGE MES.FVN_IfsInventoryLocation", sql);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        // base IFS.usp_ApplySyncBatch is not rewritten by this migration
        Assert.DoesNotContain("ALTER PROCEDURE IFS.usp_ApplySyncBatch", sql, StringComparison.OrdinalIgnoreCase);
    }
}
