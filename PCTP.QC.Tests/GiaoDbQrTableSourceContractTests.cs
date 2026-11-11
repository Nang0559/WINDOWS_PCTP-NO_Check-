using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace PCTP.QC.Tests;

/// <summary>
/// Giao DB (giao đặc biệt) nạp đơn từ TMPPHIEUGIAOHANGDB nên phiên đọc QR phải kiểm tra đơn
/// trên TMPPHIEUGIAOHANGDB và ghi QR vào DOCQRCODE_DB, không dùng TMPPHIEUGIAOHANG/DOCQRCODE
/// của giao thường. Đây là source-contract test (không chạy SQL Server).
/// </summary>
public sealed class GiaoDbQrTableSourceContractTests
{
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));

    private static string Norm(string s) => Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();

    [Fact]
    public void HvnConfig_DeclaresGiaoDbTablePair_AndKeepsDefaultMpTables()
    {
        var config = Read("CustomerTableConfig.source.cs");
        Assert.Contains("TmpTableGiaoDB = \"TMPPHIEUGIAOHANGDB\"", config, StringComparison.Ordinal);
        Assert.Contains("DocQRTableGiaoDB = \"DOCQRCODE_DB\"", config, StringComparison.Ordinal);
        // Không đổi bảng mặc định của giao thường.
        Assert.Contains("TmpTable = \"TMPPHIEUGIAOHANG\"", config, StringComparison.Ordinal);
        Assert.Contains("DocQRTable = \"DOCQRCODE\"", config, StringComparison.Ordinal);
    }

    [Fact]
    public void DeliveryOptions_GiaoDbWinsOverSp_OnlyWhenConfigured()
    {
        var o = Norm(Read("GiaoHangKhachConfiguration.giaodb.source.cs"));
        Assert.Contains(Norm("isGiaoDB && CoConfigGiaoDB ? DocQRTableGiaoDB : GetDocQRTable(isSP)"), o, StringComparison.Ordinal);
        Assert.Contains(Norm("isGiaoDB && CoConfigGiaoDB ? TmpTableGiaoDB : GetTmpTable(isSP)"), o, StringComparison.Ordinal);
    }

    [Fact]
    public void QrSession_ResolvesTablesWithGiaoDbFlag_AndKeepsFlagAcrossSetCheDoBanSP()
    {
        var s = Norm(Read("DocQRSessionState.giaodb.source.cs"));
        Assert.Contains("_tables.GetDocQrTable(IsBanSP, IsGiaoDB)", s, StringComparison.Ordinal);
        Assert.Contains("_tables.GetTmpTable(IsBanSP, IsGiaoDB)", s, StringComparison.Ordinal);

        // SetCategory(isSp, isOType) (dùng bởi SetCheDoBanSP) không được xoá cờ Giao DB.
        int start = s.IndexOf("public void SetCategory(bool isSp, bool isOType) {", StringComparison.Ordinal);
        Assert.True(start >= 0, "2-arg SetCategory was not found.");
        int end = s.IndexOf("}", start, StringComparison.Ordinal);
        Assert.DoesNotContain("IsGiaoDB", s.Substring(start, end - start), StringComparison.Ordinal);

        var svc = Norm(Read("DocQRService.giaodb.source.cs"));
        Assert.Contains("GioMoTaCategoryResolver.IsLoaiGiaoDB(gioMoTa)", svc, StringComparison.Ordinal);
        Assert.Contains("_session.SetCategory(isSp, isOType, isGiaoDb)", svc, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanEngine_HvnTemChecks_UseSessionDocQrTable()
    {
        var engine = Read("DocQRScanEngine.source.cs");
        Assert.Contains("_repo.KiemTraTemMa(maHvn, DocQRTable)", engine, StringComparison.Ordinal);
        Assert.Contains("_repo.KiemTraTemSoLuong(maHvn, slTem, DocQRTable)", engine, StringComparison.Ordinal);
        Assert.DoesNotContain("_repo.KiemTraTemMa(maHvn))", engine, StringComparison.Ordinal);

        var repo = Read("DocQRRepository.source.cs");
        Assert.Contains("KiemTraTemMa(string maHvn, string docQrTable)", repo, StringComparison.Ordinal);
        Assert.Contains("KiemTraTemSoLuong(string maHvn, int slTemHvn, string docQrTable)", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void LuuGiaoDB_UsesGiaoDbQrTable_NotHardCodedDocQrCode()
    {
        var repo = Read("PhieuGiaoDBRepository.giaodb.source.cs");
        Assert.Contains("new PhieuTableSet(tmpTable, ifsTable, docQrTable)", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("new PhieuTableSet(tmpTable, ifsTable, \"DOCQRCODE\")", repo, StringComparison.Ordinal);

        var svc = Read("PhieuGiaoDbService.giaodb.source.cs");
        Assert.Contains("docQrTable: DocQrTableGiaoDB", svc, StringComparison.Ordinal);
    }

    [Fact]
    public void StockUpdateAndHoanThanh_UseGiaoDbTablePair()
    {
        var kho = Norm(Read("PhieuKhoService.giaodb.source.cs"));
        Assert.Contains("_cfg.Delivery.GetTmpTable(isSP, useGiaoDbTables)", kho, StringComparison.Ordinal);
        Assert.Contains("_cfg.Delivery.GetDocQRTable(isSP, useGiaoDbTables)", kho, StringComparison.Ordinal);

        var phieu = Read("PhieuService.giaodb.source.cs");
        Assert.Contains("_khoService.CapNhapKho(gioGiaoFcc, nhaMay, gioMa, _isLoaiSP, _isGiaoDb)", phieu, StringComparison.Ordinal);
        Assert.Contains("_isGiaoDb = false;", phieu, StringComparison.Ordinal); // LoadPhieu (giao thường) xoá cờ

        var presenter = Read("PhieuPresenter.giaodb.source.cs");
        Assert.Contains("_c.Cfg.Delivery.GetTmpTable(isSp, isGiaoDb)", presenter, StringComparison.Ordinal);
        Assert.Contains("_c.Cfg.Delivery.GetDocQRTable(isSp, isGiaoDb)", presenter, StringComparison.Ordinal);
    }

    [Fact]
    public void StoredProcedures_WhitelistGiaoDbTables_AndCreateScriptMatchesDocQrCodeShape()
    {
        var stock = Read("Usp_Qrcode_Update_Stock2405.sql");
        Assert.Contains("N'TMPPHIEUGIAOHANGDB'", stock, StringComparison.Ordinal);
        Assert.Contains("N'DOCQRCODE_DB'", stock, StringComparison.Ordinal);

        var takeLot = Read("Usp_Qrcode_Take_Lot2405.sql");
        Assert.Contains("N'TMPPHIEUGIAOHANGDB'", takeLot, StringComparison.Ordinal);
        Assert.Contains("N'DOCQRCODE_DB'", takeLot, StringComparison.Ordinal);

        var create = Read("PCTP_GiaoDB_DocQrCode_DB.sql");
        Assert.Contains("CREATE TABLE [dbo].[DOCQRCODE_DB]", create, StringComparison.Ordinal);
        foreach (var col in new[] { "LOTFCC", "MAHANGFCC", "SLTEMFCC", "LOTHVN", "MAHANGHVN", "SLTEMHVN",
                                    "STATUS", "MAFCC", "STT", "KETQUA", "CUA", "TRUYEN", "GIO", "STTBAN",
                                    "SUALOTHVN", "TGLUU", "FindTem" })
            Assert.Contains("[" + col + "]", create, StringComparison.Ordinal);
    }
}
