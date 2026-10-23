using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Guards production source contracts that cannot be compiled into the isolated
/// .NET 8 test project because the WinForms application targets .NET Framework 4.7.2.
/// These are source-contract tests, not substitutes for executing SQL Server procedures.
/// </summary>
public sealed class OrderTableSourceContractTests
{
    private static string ReadSource(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName));

    [Fact]
    public void LoadPhieuTuBangRieng_PreservesEachSourceOrderRow()
    {
        var source = ReadSource("TableOrderRepo.source.cs");
        var methodStart = source.IndexOf("public DataTable LoadPhieuTuBangRieng(", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "LoadPhieuTuBangRieng method was not found.");

        var methodEnd = source.IndexOf("// Load phiếu đang đọc YMVN", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart, "Could not isolate LoadPhieuTuBangRieng method.");

        var method = source.Substring(methodStart, methodEnd - methodStart);
        Assert.Contains("o.Slgiao", method);
        Assert.Contains("o.Part_name", method);
        Assert.Contains("o.NgayGiao", method);
        Assert.DoesNotContain("SUM(o.Slgiao)", method, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MAX(o.", method, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GROUP BY", method, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoredProcedure_DoesNotDuplicateOrderDeliveredFinalization()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock2405.sql");
        Assert.Contains("OrderLineKey", sql, StringComparison.Ordinal);
        Assert.Contains("OrderTableSql.MarkDelivered", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE po", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Purchase_Order_HTN po", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("Purchase_Order_YMVN po", sql, StringComparison.Ordinal);

        var repository = ReadSource("PhieuKhoRepository.source.cs");
        Assert.Contains("OrderLineKey.Build", repository, StringComparison.Ordinal);
        Assert.Contains("OrderTableSql.MarkDelivered", repository, StringComparison.Ordinal);
        Assert.Contains("BuildOrderLineKeyFromTmp", repository, StringComparison.Ordinal);
        Assert.Contains("GIOGIAO", repository, StringComparison.Ordinal);
    }
}
