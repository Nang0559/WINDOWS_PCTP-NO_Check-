using System;
using PCTP.Modules.GiaoHangKhach.Models;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// DEL-010 (phần SQL text): câu UPDATE đánh dấu đã giao phải khoá theo PO + mã hàng + ngày + giờ + phút.
/// Hành vi thật trên SQL Server được kiểm ở PCTP.QC.SqlIntegration (OrderTableDeliveryMarkingScenario).
/// </summary>
public sealed class OrderTableSqlTests
{
    [Fact]
    public void MarkDelivered_FiltersByPoPartDateHourAndMinute()
    {
        var sql = OrderTableSql.MarkDelivered("Purchase_Order_YMVN");

        Assert.StartsWith("UPDATE [Purchase_Order_YMVN] SET IsDelivered = 1", sql);
        Assert.Contains("Oder_no = @po", sql);
        Assert.Contains("Part_no = @pno", sql);
        Assert.Contains("CAST(NgayGiao AS DATE) = @ng", sql);
        Assert.Contains("DATEPART(HH, NgayGiao) = @hh", sql);
        Assert.Contains("DATEPART(MI, NgayGiao) = @mi", sql);
    }

    [Fact]
    public void MarkDelivered_AcceptsSafeSessionLocalTempTable()
    {
        var sql = OrderTableSql.MarkDelivered("#PCTP_QC_ORD_012345");

        Assert.StartsWith("UPDATE [#PCTP_QC_ORD_012345] SET IsDelivered = 1", sql);
        Assert.EndsWith("AND IsDelivered = 0", sql);
    }

    [Fact]
    public void MarkDelivered_OnlyTouchesUndeliveredRows_SoReplayIsIdempotent()
    {
        Assert.EndsWith("AND IsDelivered = 0", OrderTableSql.MarkDelivered("Purchase_Order_HTN"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("##global")]\n    [InlineData("#bad name")]\n    [InlineData("#T]; DROP TABLE X;--")]\n    [InlineData("a b")]
    [InlineData("dbo.Purchase_Order")]
    [InlineData("T]; DROP TABLE X;--")]
    [InlineData("1Table")]
    [InlineData("T'x")]
    public void MarkDelivered_RejectsUnsafeTableNames(string table)
    {
        Assert.Throws<ArgumentException>(() => OrderTableSql.MarkDelivered(table));
    }
}
