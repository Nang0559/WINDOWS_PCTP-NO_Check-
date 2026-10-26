using System;
using System.IO;
using System.Text.RegularExpressions;
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

    private static string NormalizeSql(string value) =>
        Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    private static string ExtractGomNhomCheckBlock(string sql)
    {
        var normalized = NormalizeSql(sql);
        const string startMarker = "INTO #GomNhomCheck";
        var start = normalized.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0, "#GomNhomCheck block was not found.");

        var end = normalized.IndexOf(";", start, StringComparison.Ordinal);
        Assert.True(end > start, "Could not find the end of #GomNhomCheck SELECT.");

        return normalized.Substring(start, end - start + 1);
    }

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

        var sqlStart = method.IndexOf("string sql =", StringComparison.Ordinal);
        Assert.True(sqlStart >= 0, "SQL query construction was not found.");

        var sqlEnd = method.IndexOf("DataTable dt = _db.LoadData(sql);", sqlStart, StringComparison.Ordinal);
        Assert.True(sqlEnd > sqlStart, "Could not isolate the SQL passed to LoadData.");

        var sql = method.Substring(sqlStart, sqlEnd - sqlStart);
        Assert.DoesNotContain("SUM(o.Slgiao)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MAX(o.", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("OrderTableSql.MarkDelivered", repository, StringComparison.Ordinal);
        Assert.Contains("BuildOrderLineKeyFromTmp", repository, StringComparison.Ordinal);
        Assert.Contains("GIOGIAO", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void DeliveryProcedure_UsesFullPhysicalLotIdentityAndFailsClosedOnAmbiguousRoot()
    {
        var sql = ReadSource("Usp_Qrcode_Update_Stock2405.sql");
        var normalized = NormalizeSql(sql);
        var gomNhomCheck = ExtractGomNhomCheckBlock(sql);

        Assert.Contains(NormalizeSql("RTRIM(LOT) = RTRIM(@_LOTOK)"), normalized, StringComparison.Ordinal);
        Assert.Contains(NormalizeSql("RTRIM(LOT) = RTRIM(@_LOTUD)"), normalized, StringComparison.Ordinal);
        Assert.Contains(NormalizeSql("COUNT(DISTINCT RTRIM(LOT))"), normalized, StringComparison.Ordinal);
        Assert.Contains(NormalizeSql("Mã LOT gốc ánh xạ nhiều LOT vật lý"), normalized, StringComparison.Ordinal);
        Assert.Contains(NormalizeSql("S.PhysicalLot = RTRIM(G.LOT)"), normalized, StringComparison.Ordinal);

        // Scope these assertions to #GomNhomCheck. Later control-flow queries
        // legitimately contain SELECT DISTINCT STT FROM #STNG.
        Assert.Contains(NormalizeSql("NOT EXISTS ( SELECT 1 FROM #STNG E"), gomNhomCheck, StringComparison.Ordinal);
        Assert.DoesNotContain(NormalizeSql("STT NOT IN ( SELECT DISTINCT STT FROM #STNG"), gomNhomCheck, StringComparison.Ordinal);
    }

    [Fact]
    public void DeliveryProcedure_SecondStockCheck_AggregatesOnceAndHasNoCorrelatedDistinctCountJoin()
    {
        // Regression guard: the old LEFT JOIN ... ON (... OR ... (SELECT COUNT(DISTINCT RTRIM(S2.LOT)) ...)) compared
        // every delivery line with every STOCKTP LOT and ran for minutes on the shared test database.
        var normalized = NormalizeSql(ReadSource("Usp_Qrcode_Update_Stock2405.sql"));

        Assert.Contains("INTO #StockByLot", normalized, StringComparison.Ordinal);
        Assert.Contains("INTO #LotKeyCount", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("COUNT(DISTINCT RTRIM(S2.LOT))", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void DeliveryProcedure_LedgerInsert_CopiesPoFieldsOnlyWhenTmpTableHasThem()
    {
        var sql = NormalizeSql(ReadSource("Usp_Qrcode_Update_Stock2405.sql"));

        Assert.Contains(NormalizeSql("COL_LENGTH(N'dbo.' + @TMPTABLE, N'PO_NO') IS NOT NULL"), sql, StringComparison.Ordinal);
        Assert.Contains(NormalizeSql("COL_LENGTH(N'dbo.' + @TMPTABLE, N'PO_ITEM') IS NOT NULL"), sql, StringComparison.Ordinal);
        Assert.Contains(NormalizeSql("Note' + @LuuPoCols + N'"), sql, StringComparison.Ordinal);
        Assert.Contains(NormalizeSql("LEFT(tmp.Note, 250)' + @LuuPoSel + N'"), sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomerTableConfig_TenNhaMay_IsPlainValueNotSqlLiteral()
    {
        // TenNhaMay duoc so sanh bang NHAMAY = '...' voi ledger va in ra phieu, nen phai la
        // gia tri thuan (ledger: 'YAMAHA - VIET NAM'). Chi NhaMayCase la bieu thuc SQL co dau nhay.
        var config = ReadSource("CustomerTableConfig.source.cs");

        Assert.DoesNotContain("TenNhaMay = \"'", config, StringComparison.Ordinal);
        Assert.Contains("TenNhaMay = \"YAMAHA - VIET NAM\"", config, StringComparison.Ordinal);
    }
}
