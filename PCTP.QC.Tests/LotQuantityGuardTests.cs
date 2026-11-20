using System.Data;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Services;
using Xunit;

namespace PCTP.QC.Tests;

public sealed class LotQuantityGuardTests
{
    private static DataTable Tmp(params (int Stt, string Ma, int Qty, string Lot, string Status)[] rows)
    {
        var t = new DataTable();
        t.Columns.Add("STT", typeof(int));
        t.Columns.Add("MAHANG", typeof(string));
        t.Columns.Add("SOLUONG", typeof(int));
        t.Columns.Add("LOT", typeof(string));
        t.Columns.Add("STATUS", typeof(string));
        foreach (var r in rows) t.Rows.Add(r.Stt, r.Ma, r.Qty, r.Lot, r.Status);
        return t;
    }

    [Fact]
    public void TotalIsSumOfAllLotParts()
    {
        Assert.True(LotQuantityGuard.TryParseTotal("A-100,B-350", out int total));
        Assert.Equal(450, total);
    }

    [Fact]
    public void MalformedPartIsNotConclusive()
    {
        Assert.False(LotQuantityGuard.TryParseTotal("A-100,B", out _));
        Assert.False(LotQuantityGuard.TryParseTotal("", out _));
    }

    [Fact]
    public void LotTotalOverOrderQuantityIsReported()
    {
        var over = LotQuantityGuard.FindOver(Tmp((7, "P1", 200, "A-100,B-350", "NG")));
        var item = Assert.Single(over);
        Assert.Equal(7, item.Stt);
        Assert.Equal(200, item.SoLuongDon);
        Assert.Equal(450, item.TongLot);
    }

    [Fact]
    public void ExactOrShortTotalIsAllowed()
    {
        Assert.Empty(LotQuantityGuard.FindOver(Tmp(
            (1, "P1", 200, "A-200", "NG"),
            (2, "P2", 200, "A-150", "NG"))));
    }

    [Fact]
    public void AlreadyUpdatedRowsAndEmptyLotsAreIgnored()
    {
        Assert.Empty(LotQuantityGuard.FindOver(Tmp(
            (1, "P1", 100, "A-500", "OK"),
            (2, "P2", 100, "", "NG"))));
    }

    [Fact]
    public void ErrorTableCarriesQuantitiesAndMessage()
    {
        var table = LotQuantityGuard.ToErrorTable(LotQuantityGuard.FindOver(Tmp((7, "P1", 200, "A-450", "NG"))));
        var row = Assert.Single(table.Rows.Cast<DataRow>());
        Assert.Equal(200, (int)row["SLC"]);
        Assert.Equal(450, (int)row["SLTK"]);
        Assert.Equal(250, (int)row["SLT"]);
        Assert.Contains("STT 7", (string)row["STATUS"]);
    }
}
