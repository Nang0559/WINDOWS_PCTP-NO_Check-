using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Màn "Đơn đã upload" (bảng đơn riêng Milkrun): xem / sửa / xóa dòng CHƯA GIAO, khóa dòng đã giao.
/// Source-contract test (không chạy SQL Server).
/// </summary>
public sealed class UploadedOrderManagerContractTests
{
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));

    [Fact]
    public void NewFiles_AreCompiledByTheOldStyleProject()
    {
        // PCTP.csproj là project kiểu cũ: file .cs mới KHÔNG tự được biên dịch nếu thiếu <Compile Include>.
        var proj = Read("PCTP.csproj.source.xml");
        Assert.Contains(@"Modules\GiaoHangKhach\Repositories\UploadedOrderRepository.cs", proj, StringComparison.Ordinal);
        Assert.Contains(@"Modules\GiaoHangKhach\Controls\UploadedOrderManagerControl.cs", proj, StringComparison.Ordinal);
        Assert.Contains(@"Modules\GiaoHangKhach\SubForm\FRM_QuanLyDonUpload.cs", proj, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_ValidatesTableName_AndNeverTouchesDeliveredRows()
    {
        var repo = Read("UploadedOrderRepository.source.cs");

        Assert.Contains("SafeName", repo, StringComparison.Ordinal);   // tên bảng ghép vào SQL phải qua whitelist

        var xoa = Regex.Match(repo, @"private void XoaDong\(.*?\n        }\n", RegexOptions.Singleline).Value;
        var sua = Regex.Match(repo, @"private void SuaDong\(.*?\n        }\n", RegexOptions.Singleline).Value;
        Assert.Contains("ISNULL(IsDelivered, 0) = 0", xoa, StringComparison.Ordinal);
        Assert.Contains("ISNULL(IsDelivered, 0) = 0", sua, StringComparison.Ordinal);
        Assert.Contains("ExecuteNonQuery() != 1", xoa, StringComparison.Ordinal);   // không đúng 1 dòng → báo lỗi + rollback
        Assert.Contains("ExecuteNonQuery() != 1", sua, StringComparison.Ordinal);

        // thiếu cột IsDelivered → từ chối ghi thay vì coi mọi dòng là chưa giao
        Assert.Matches(@"if \(!info\.HasIsDelivered\)\s*throw new InvalidOperationException", repo);
        // một transaction cho cả lần lưu
        Assert.Contains("BeginTransaction()", repo, StringComparison.Ordinal);
        Assert.Contains("tran.Rollback()", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void Control_LocksDeliveredRows_AndBlocksDeleteOfThem()
    {
        var ctrl = Read("UploadedOrderManagerControl.source.cs");

        Assert.Contains("ShowingEditor += View_ShowingEditor", ctrl, StringComparison.Ordinal);
        Assert.Contains("if (DaGiao(v.GetDataRow(v.FocusedRowHandle))) e.Cancel = true;", ctrl, StringComparison.Ordinal);
        Assert.Matches(@"BtnXoa_Click[\s\S]*?if \(DaGiao\(r\)\)", ctrl);
    }

    [Fact]
    public void MilkrunForm_OffersTheManagerScreen()
    {
        var form = Read("FRM_UploadMikrun.source.cs");

        Assert.Contains("new FRM_QuanLyDonUpload(_sql, _targetTable", form, StringComparison.Ordinal);
        Assert.Contains("btnQuanLy", form, StringComparison.Ordinal);
    }
}
