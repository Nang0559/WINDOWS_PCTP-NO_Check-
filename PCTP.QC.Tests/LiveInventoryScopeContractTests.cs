using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Prevents the live metadata workflow from reverting to a full-database scan.
/// These tests inspect the checked-in PowerShell query contract; the live workflow
/// remains read-only and requires its separate manually gated execution.
/// </summary>
public sealed class LiveInventoryScopeContractTests
{
    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));

    [Fact]
    public void LiveInventory_FailsClosedAndRequiresPctpScopeCsv()
    {
        var source = ReadSource("Invoke-PctpLiveDataInventory.source.ps1");

        Assert.Contains("PCTP scope CSV not found", source, StringComparison.Ordinal);
        Assert.Contains("Refusing to inventory the entire database", source, StringComparison.Ordinal);
        Assert.Contains("refusing broad inventory", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Assert-PctpSourceReadOnly $source 'SQL14 source'", source, StringComparison.Ordinal);
        Assert.Contains("Assert-PctpSourceReadOnly $target 'SQL57 metadata target'", source, StringComparison.Ordinal);
        Assert.Contains("full_database_inventory=$false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveInventory_ScopesEveryTableMetadataQueryToAllowlist()
    {
        var source = ReadSource("Invoke-PctpLiveDataInventory.source.ps1");

        Assert.Contains("s.name=N'dbo' AND LOWER(t.name) IN ($tableList)", source, StringComparison.Ordinal);
        Assert.Contains("s.name=N'dbo' AND LOWER(p.name) IN ($procedureList)", source, StringComparison.Ordinal);
        Assert.Contains("(ps.name=N'dbo' AND LOWER(pt.name) IN ($tableList)) OR (rs.name=N'dbo' AND LOWER(rt.name) IN ($tableList))", source, StringComparison.Ordinal);
        Assert.Contains("OBJECT_SCHEMA_NAME(tr.parent_id)=N'dbo' AND LOWER(OBJECT_NAME(tr.parent_id)) IN ($tableList)", source, StringComparison.Ordinal);
        Assert.Contains("LOWER(p.name) IN ($procedureList)", source, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WHERE p.is_ms_shipped=0 ORDER BY s.name,p.name;",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LiveInventoryScope_ContainsPctpClassifiedObjectsNotWholeDatabaseNames()
    {
        var csv = ReadSource("SCHEMA_COVERAGE_EFFECTIVE.csv");
        Assert.Contains("object,kind,", csv, StringComparison.Ordinal);
        Assert.Contains("OK-fk-dependency", csv, StringComparison.Ordinal);
        Assert.Contains("OK-testable", csv, StringComparison.Ordinal);
    }
}
