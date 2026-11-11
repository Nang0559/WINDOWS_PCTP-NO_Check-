using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Đơn Giao DB nằm ở bảng NGHIỆP VỤ FVN_Purchase_Order_DB_HD / FVN_Purchase_Order_DB_CT
/// (trước đây TMPPHIEUGIAODBHD / TMPPHIEUGIAOHANGDBCT, dễ nhầm với bảng làm việc TMPPHIEUGIAOHANGDB).
/// Source-contract test (không chạy SQL Server).
/// </summary>
public sealed class GiaoDbOrderTablesContractTests
{
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));

    private static string WithoutLineComments(string src) =>
        string.Join("\n", src.Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    [Fact]
    public void Repository_UsesBusinessTableNames_NotLegacyTmpNames()
    {
        var repo = Read("PhieuGiaoDBRepository.giaodb.source.cs");

        Assert.Contains("TableHd = \"FVN_Purchase_Order_DB_HD\"", repo, StringComparison.Ordinal);
        Assert.Contains("TableCt = \"FVN_Purchase_Order_DB_CT\"", repo, StringComparison.Ordinal);

        var code = WithoutLineComments(repo);
        Assert.DoesNotContain("TMPPHIEUGIAODBHD", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TMPPHIEUGIAOHANGDBCT", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void XuLySauUpload_LoadsAllFactoriesInOnePass_BecauseGiaoDbSpWipesTmpTable()
    {
        var svc = Read("PhieuGiaoDbService.giaodb.source.cs");

        var m = Regex.Match(svc,
            @"public void XuLySauUpload\(\)(?<body>.*?)public string CanhBaoLuoiRong",
            RegexOptions.Singleline);
        Assert.True(m.Success, "Không tìm thấy XuLySauUpload / CanhBaoLuoiRong");

        var body = WithoutLineComments(m.Groups["body"].Value);

        // Nhánh (GIAO DB) của Usp_Qrcode_LOAD_PHIEU_DOCQR2405 DELETE toàn bộ bảng TMP rồi chép _IFS:
        // lặp theo nhà máy sẽ xóa mất dữ liệu của nhóm xử lý trước.
        Assert.DoesNotContain("GroupBy", body, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach", body, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(body, @"_phieuRepo\.LuuGiaoDB\(").Count);
    }

    [Fact]
    public void Service_ExplainsEmptyGrid_ByDateAndFactory()
    {
        var svc = Read("PhieuGiaoDbService.giaodb.source.cs");
        Assert.Contains("_giaoDbRepo.ThongKeDonChoXuLy()", svc, StringComparison.Ordinal);
        Assert.Contains("public string CanhBaoLuoiRong(DateTime ngayGiao, int addNm)", svc, StringComparison.Ordinal);
    }
}
