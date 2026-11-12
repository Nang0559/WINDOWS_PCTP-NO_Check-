using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Quy tắc phiếu Giao DB: 1 ngày lập + 1 nhà máy (khách hàng HVN) = tối đa 1 phiếu (header) + 1..n chi tiết
/// thêm ở nhiều thời điểm; chỉ sửa/xóa được dòng CHƯA GIAO (STATUS='NG'). Source-contract test (không chạy SQL Server).
/// </summary>
public sealed class GiaoDbOnePhieuPerDayContractTests
{
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));

    private static string WithoutLineComments(string src) =>
        string.Join("\n", src.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    [Fact]
    public void PhieuRepositoryFacade_ForwardsEveryMemberOfIPhieuGiaoDBRepository()
    {
        // Lỗi CS0535 trước đây: thêm member vào interface nhưng quên dòng chuyển tiếp ở facade.
        var iface = WithoutLineComments(Read("IPhieuGiaoDBRepository.source.cs"));
        var facade = Read("PhieuRepository.source.cs");

        var names = Regex.Matches(iface, @"(?m)^\s*(?:void|int|bool|string|DataTable|DataSet)\s+(\w+)\s*\(")
            .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.Contains("GetHeaderGiaoDB", names);
        Assert.Contains("GetChiTietGiaoDB", names);
        Assert.Contains("LuuPhieuGiaoDB", names);
        Assert.Contains("XoaPhieuGiaoDB", names);
        Assert.Contains("ThongKeDonChoXuLy", names);

        foreach (var n in names)
            Assert.True(Regex.IsMatch(facade, @"public\s+[\w<>]+\s+" + n + @"\s*\("),
                $"PhieuRepository chưa chuyển tiếp {n}()");
    }

    [Fact]
    public void Repository_KeysPhieuByDateAndFactory_AndSerializesCreation()
    {
        var repo = Read("PhieuGiaoDBRepository.giaodb.source.cs");

        Assert.Contains("CAST(NGAYLAP AS date) = @ngay AND NHAMAY = @nm", repo, StringComparison.Ordinal);
        // tìm header có khóa UPDLOCK/HOLDLOCK → 2 máy không thể cùng tạo 2 phiếu cho một ngày + nhà máy
        Assert.Matches(@"SELECT TOP \(1\) IDP FROM \[\{TableHd\}\] WITH \(UPDLOCK, HOLDLOCK\)", repo);
        // đường tạo cũ nay đi qua LuuPhieuGiaoDB (không còn tạo header thứ hai)
        Assert.Matches(@"TaoPhieuVaChiTietGiaoDB\([^)]*\)\s*\{[^}]*return LuuPhieuGiaoDB\(", repo);
    }

    [Fact]
    public void Repository_OnlyEditsOrDeletesLinesThatAreNotDeliveredYet()
    {
        var repo = Read("PhieuGiaoDBRepository.giaodb.source.cs");

        Assert.Contains("ChuaGiao = \"ISNULL(D.STATUS, 'NG') = 'NG'\"", repo, StringComparison.Ordinal);

        var sua = Regex.Match(repo, @"private void SuaChiTiet\(.*?\n        }\n", RegexOptions.Singleline).Value;
        var xoa = Regex.Match(repo, @"private void XoaChiTiet\(.*?\n        }\n", RegexOptions.Singleline).Value;
        Assert.Contains("{ChuaGiao}", sua, StringComparison.Ordinal);
        Assert.Contains("{ChuaGiao}", xoa, StringComparison.Ordinal);
        Assert.Contains("n == 0", sua, StringComparison.Ordinal);   // dòng vừa bị máy khác xử lý → báo lỗi, rollback
        Assert.Contains("n == 0", xoa, StringComparison.Ordinal);

        // xóa cả phiếu bị chặn khi còn dòng đã giao
        var xoaPhieu = Regex.Match(repo, @"public int XoaPhieuGiaoDB\(.*?\n        }\n", RegexOptions.Singleline).Value;
        Assert.Contains("NOT ({ChuaGiao})", xoaPhieu, StringComparison.Ordinal);
    }

    [Fact]
    public void UploadForm_LoadsExistingPhieu_LocksDeliveredRows_AndSavesViaUpsert()
    {
        var form = Read("FRM_UploadGiaoDB.source.cs");

        Assert.Contains("GetHeaderGiaoDB(", form, StringComparison.Ordinal);
        Assert.Contains("GetChiTietGiaoDB(", form, StringComparison.Ordinal);
        Assert.Contains("ShowingEditor", form, StringComparison.Ordinal);        // khóa ô dòng đã giao
        Assert.Contains("DongDaGiao(row)", form, StringComparison.Ordinal);      // chặn Xóa Dòng
        Assert.Contains("LuuPhieuGiaoDB(", form, StringComparison.Ordinal);
        Assert.DoesNotContain("TaoPhieuVaChiTietGiaoDB(", form, StringComparison.Ordinal);
        // đổi ngày / nhà máy → nạp lại phiếu của khóa mới
        Assert.Contains("dateNgayLap.EditValueChanged += (s, e) => NapPhieuDaCo", form, StringComparison.Ordinal);
        Assert.Contains("cboNhaMay.SelectedIndexChanged += (s, e) => NapPhieuDaCo", form, StringComparison.Ordinal);
    }

    [Fact]
    public void XuLySauUpload_StillReloadsGrid_WhenNoPendingLinesRemain()
    {
        // Xóa hết dòng chưa giao → phải nạp bảng rỗng để lưới không giữ dòng của đơn đã xóa.
        var svc = Read("PhieuGiaoDbService.giaodb.source.cs");
        var body = Regex.Match(svc, @"public void XuLySauUpload\(\).*?\n        }\n", RegexOptions.Singleline).Value;

        Assert.DoesNotContain("donHang.Rows.Count == 0", body, StringComparison.Ordinal);
    }
}
