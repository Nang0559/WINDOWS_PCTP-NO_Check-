using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Helpers;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public interface IQTChungService
    {
        int TaoPhieuXuLyBatThuong(int phieuTraHangCTId, string model, string phanLoaiXuLy, string boPhanPhatHanh, string nguoiThucHien);
        ScanResult QCDinhHuong(int phieuXuLyId, HuongXuLyBatThuong huong, string nguoiThucHien);
        List<LotInfo> GetLotsCanRework(int phieuXuLyId);
        ScanResult XuatKhoRework(int phieuXuLyId, int slotId, string lotNo, int soLuong, string nguoiXuat);
        ScanResult GiaoHangRework(int phieuXuLyId, List<LotInfo> lots, string ngayGiao, string nguoiNhan, string boPhanNhan);
        void GhiNhanDangRework(int phieuXuLyId, string ghiChu, string nguoiThucHien);
        ScanResult QCXacNhanCuoi(int phieuXuLyId, int soLuongOK, int soLuongNG, string nguoiQC, int? slotIdOK = null, int? slotIdNG = null, string lotNo = null);
        void GhiNhanKiemTraTem(int qcId, bool daKiemTra);
        ScanResult NhapLaiHangNG(int phieuXuLyId, string lotNo, int soLuongNG, int? slotIdOK, int? slotIdNG, string nguoiNhap);
        ScanResult HoanTat(int phieuXuLyId, string nguoiThucHien);
        ScanResult XacNhanChoGiaoBu(int phieuXuLyId, string nguoiThucHien);
        ScanResult DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien);
        ScanResult GiaoLaiBoPhanPhatHien(int phieuXuLyId, string boPhanNhan, int soLuongGiaoLai, string nguoiThucHien);
        ScanResult HuyQTChung(int phieuXuLyId, string lyDoHuy, string nguoiThucHien);
        PhieuXuLyBatThuong GetById(int phieuXuLyId);
        IReadOnlyList<QTChungStatus> GetAllowedNext(int phieuXuLyId);
        List<QTChungTimelineItem> GetTimeline(int phieuXuLyId);
    }
}