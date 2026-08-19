using PCTP.Domain.Entities;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Enum;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public interface IQTChungService
    {
        // ============================================================
        // 1. TẠO PHIẾU XỬ LÝ BẤT THƯỜNG
        // ============================================================

        int TaoPhieuXuLyBatThuong(
            PhieuXuLyBatThuong phieu);

        // ============================================================
        // 2. QC ĐỊNH HƯỚNG REWORK
        // ============================================================

        void QCDinhHuongRework(
            int phieuXuLyId,
            string huongXuLy,
            string nguoiThucHien);

        // ============================================================
        // 3. TÌM TOÀN BỘ LOT NG CÒN TRONG KHO
        // ============================================================

        List<LotInfo> GetLotsCanRework(
            int phieuXuLyId);

        // ============================================================
        // 4. XUẤT KHO ĐI REWORK
        // ============================================================

        ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotId,
            string lotNo,
            int soLuong,
            string nguoiXuat);

        // ============================================================
        // 5. GIAO HÀNG CHO SẢN XUẤT
        // ============================================================

        ScanResult GiaoHangRework(
            int phieuXuLyId,
            List<LotInfo> lots,
            string nguoiGiao);

        // ============================================================
        // 6. SẢN XUẤT BÁO REWORK XONG
        // ============================================================

        void SanXuatBaoReworkXong(
            int phieuXuLyId,
            string ghiChu,
            string nguoiThucHien);

        // ============================================================
        // 7. QC XÁC NHẬN CUỐI
        // ============================================================

        ScanResult QCXacNhanCuoi(
            int phieuXuLyId,
            int soLuongOK,
            int soLuongNG,
            string nguoiQC);

        // ============================================================
        // 8. NHẬP LẠI HÀNG NG
        // ============================================================

        ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuongNG,
            string nguoiNhap);

        // ============================================================
        // 9. HUỶ QT CHUNG
        // ============================================================

        ScanResult HuyQTChung(
            int phieuXuLyId,
            string lyDoHuy,
            string nguoiThucHien);

        // ============================================================
        // 10. TRA CỨU
        // ============================================================

        PhieuXuLyBatThuong GetById(
            int phieuXuLyId);

        QTChungStatus GetTrangThai(
            int phieuXuLyId);

        List<QTChungTimelineItem> GetTimeline(
            int phieuXuLyId);
    }
}
