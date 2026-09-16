using PCTP.Domain.Entities;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Facade nghiệp vụ cho phiếu giao hàng — API tương thích với UI/Presenter.
    /// Implementation: <see cref="PhieuService"/>.
    /// </summary>
    public interface IPhieuService
    {
        void SetTrangThaiBan(bool isBanQR, bool isLoaiSP);
        void LoadPhieu(string ngayGiao, string nhaMay, string gioFcc, string gioFccMoTa, int addNm, bool isMayBanQR, bool isBanQR, List<string> checkedGios = null, bool isLoaiSP = false);
        void SyncIfsPhieuChoDocQR(string ngayGiao, string nhaMay, string gioFcc, string gioFccMoTa, int addNm);
        bool KiemTraMaTrongPhieu(string maHang);
        bool CheckCoLotChuaCNK(DataTable donHang);
        bool CheckCanCapNhapKho(DataTable donHang);
        bool CheckCoMaNG();
        DataTable TinhLechIFS(DataTable donHangBangRieng, string ngayXuatIFS);
        TrangThaiBan GetTrangThaiDangBan();
        TrangThaiBan GetTrangThaiDangBanSP();
        bool XoaDocQRCode(bool isSP = false);
        DataTable GetDonHangHienTai(string tenbang);
        DataTable GetDonHangChuaLot(bool isSP = false);
        DataTable LoadGhepLot();
        void LayLaiLotNo(int stt, bool isSP = false);
        DataTable GetDanhSachMaHangGiaoDB();
        int TaoPhieuVaChiTietGiaoDB(string ten, DateTime ngayLap, int nhaMay, string nhaMayName, string note, DataTable chiTiet);
        void LuuGiaoDB(DataTable donHang, GioXuat gioXuat, int addNm);
        DataTable LoadTmpPhieuGiaoDB(DateTime ngayGiao, int addNm);
        void XuLySauUploadGiaoDB();
        int LuuPhieuSP(string nhaMay, string ngayGiao, string gioGiaoFcc, string loaiPhieu);
        void CapNhapTTPHIEU(string nhaMay, string ngayGiao, string gioGiaoFcc, int stt, string ghiChu);
        void CapNhapKho(string gioGiaoFcc, string nhaMay, string gioMa = "");
        void CapNhapKhoYMVN(string ngayGiao, string gioXuat, string nhaMay, DataTable donHang);
        void HoanThanhYMVN(bool isLoaiSP = false);
        List<string> GetDanhSachGioYMVN(string ngayXuatMDY);
        List<string> GetGioDaGiao(string nhaMay, string ngayGiao);
        void UploadMilkrunSP(DataTable donHang, string ngayGiao);
        void SyncPhieuTuBangRiengChoDocQR(DataTable donHang, string ngayGiao, List<string> checkedGios = null);
        DataTable GetDanhSachLotTuKho(string maHang);
        void NhapLotThuCong(int stt, string lotNo, string tenbang);
    }
}