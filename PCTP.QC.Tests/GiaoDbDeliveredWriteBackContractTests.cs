using System;
using System.IO;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Giao DB: dòng đã giao phải được ghi 'OK' + LOT về FVN_Purchase_Order_DB_CT sau Cập nhật kho, và lần dựng bảng
/// nguồn kế tiếp phải giữ chúng là đã giao (không dựng lại thành NG, LOT trống). Source-contract test.
/// </summary>
public sealed class GiaoDbDeliveredWriteBackContractTests
{
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name)).Replace("\r\n", "\n");

    [Fact]
    public void CapNhapKho_GiaoDb_WritesDeliveredLinesBackToDetailTable()
    {
        var kho = Read("PhieuKhoService.giaodb.source.cs");
        var publish = kho.IndexOf("_bus.Publish(new KhoUpdatedEvent(soLot, errors))", StringComparison.Ordinal);
        var sync = kho.IndexOf("_phieuRepo.DongBoDaGiaoVaoDonGiaoDB(tmpTable)", StringComparison.Ordinal);
        Assert.True(sync >= 0 && publish > sync, "Write-back to DB_CT must run before KhoUpdatedEvent is published.");
        Assert.Contains("if (useGiaoDbTables)", kho, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_MarksOnlyUndeliveredLines_AndKeepsDeliveredOnesOnRebuild()
    {
        var repo = Read("PhieuGiaoDBRepository.giaodb.source.cs");

        // write-back: chỉ dòng chưa giao, nối bằng STT + mã hàng, chỉ từ dòng TMP đã OK có LOT
        Assert.Contains("D.STT = T.STT", repo, StringComparison.Ordinal);
        Assert.Contains("ISNULL(D.STATUS, 'NG') = 'NG'", repo, StringComparison.Ordinal);
        Assert.Contains("WHERE T.STATUS = 'OK'", repo, StringComparison.Ordinal);

        // rebuild: lấy cả NG và OK, dòng OK mang LOT + STATUS OK
        Assert.Contains("IN ('NG', 'OK')", repo, StringComparison.Ordinal);
        Assert.Contains("row[\"STATUS\"] = daGiao ? \"OK\" : \"NG\";", repo, StringComparison.Ordinal);
        Assert.Contains("row[\"LOT\"] = daGiao ?", repo, StringComparison.Ordinal);

        // STT ghi lại lúc nạp, ID_CT không lọt vào bảng IFS
        Assert.Contains("ifsData.Columns.Remove(\"ID_CT\")", repo, StringComparison.Ordinal);
        Assert.Contains("SET STT = @stt WHERE ID = @id", repo, StringComparison.Ordinal);
    }
}
