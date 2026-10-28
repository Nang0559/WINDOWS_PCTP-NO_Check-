using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Schema contract found by the static DB57 column check: the Rack table stores the row count in
/// [RackRowCount] (DB57 export and SQL/QC_Schema/01_Tables.sql). RackRepository.Create once inserted into
/// a non-existent [RowCount] column, which fails at runtime on DB57 (WarehouseService.CreateWithRack path).
/// </summary>
public sealed class RackRepositoryColumnContractTests
{
    private static string ReadSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "RackRepository.source.cs");
        Assert.True(File.Exists(path), "Linked source fixture missing: " + path);
        return File.ReadAllText(path);
    }

    [Fact]
    public void EveryRackInsert_WritesRackRowCount_NotRowCount()
    {
        var source = ReadSource();
        var inserts = Regex.Matches(source, @"INSERT\s+INTO\s+(?:dbo\.)?Rack\s*\(([^)]*)\)", RegexOptions.IgnoreCase);
        Assert.NotEmpty(inserts);
        foreach (Match insert in inserts)
        {
            var columns = insert.Groups[1].Value;
            Assert.Matches(new Regex(@"\bRackRowCount\b", RegexOptions.IgnoreCase), columns);
            Assert.DoesNotMatch(new Regex(@"(?<![A-Za-z])RowCount\b", RegexOptions.IgnoreCase), columns);
        }
    }
}
