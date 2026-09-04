using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Helpers;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public interface IQTChungService
    {
        // ============================================================
        // 1. TẠO PHIẾU XỬ LÝ BẤT THƯỜNG
        //
        // QTChung:
        //
        // Moi
        //   ↓
        // DaTaoPhieuBatThuong
        //
        // Repository:
        //     IPhieuXuLyBatThuongRepository.Insert(...)
        //
        // Service chịu trách nhiệm nghiệp vụ.
        // ============================================================

        int TaoPhieuXuLyBatThuong(
            int phieuTraHangCTId,
            string model,
            string phanLoaiXuLy,
            string boPhanPhatHanh,
            string nguoiThucHien);


        // ============================================================
        // 2. QC ĐỊNH HƯỚNG
        //
        // QTChung:
        //
        // DaTaoPhieuBatThuong
        //          ↓
        //     DaDinhHuong
        //
        // Sau DaDinhHuong mới xác định branch:
        //
        // TuChoiGiaoBu
        // ChiGiaoBu
        // CanRework
        //
        // QUAN TRỌNG:
        // Không có DaDinhHuongRework.
        // ============================================================

        ScanResult QCDinhHuong(
            int phieuXuLyId,
            HuongXuLyBatThuong huong,
            string nguoiThucHien);


        // ============================================================
        // 3. TRA CỨU LOT CÓ THỂ REWORK
        //
        // Không thay đổi QTChungStatus.
        //
        // Chỉ áp dụng nghiệp vụ cho:
        //
        // HuongXuLyBatThuong.CanRework
        // ============================================================

        List<LotInfo> GetLotsCanRework(
            int phieuXuLyId);


        // ============================================================
        // 4. XUẤT KHO ĐI REWORK
        //
        // QTChung:
        //
        // DaDinhHuong
        //      ↓
        // DaXuatKhoRework
        //
        // Chỉ áp dụng:
        //
        // HuongXuLyBatThuong.CanRework
        // ============================================================

        ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotId,
            string lotNo,
            int soLuong,
            string nguoiXuat);


        // ============================================================
        // 5. GIAO HÀNG CHO SẢN XUẤT / REWORK
        //
        // QTChung:
        //
        // DaXuatKhoRework
        //      ↓
        // DaGiaoSanXuat
        // ============================================================

        ScanResult GiaoHangRework(
            int phieuXuLyId,
            List<LotInfo> lots,
            string ngayGiao,
            string nguoiNhan,
            string boPhanNhan);


        // ============================================================
        // 6. GHI NHẬN ĐANG REWORK
        //
        // Đây là nghiệp vụ ghi nhận thông tin.
        //
        // KHÔNG phải state trong QTChungStatus.
        //
        // Không được tạo thêm một enum state "DangRework".
        // Không thay đổi QTChungStatus.
        // ============================================================

        void GhiNhanDangRework(
            int phieuXuLyId,
            string ghiChu,
            string nguoiThucHien);


        // ============================================================
        // 7. QC XÁC NHẬN CUỐI
        //
        // QTChung:
        //
        // DaGiaoSanXuat
        //      ↓
        // DaQCXacNhanCuoi
        //
        // Sau khi QC xác nhận:
        //
        // SoLuongNG = 0
        //      ↓
        // HoanTat
        //
        // SoLuongNG > 0
        //      ↓
        // DaNhapLaiKho
        //      ↓
        // HoanTat
        //
        // Việc chọn nhánh phải được Service quyết định
        // dựa trên kết quả QC.
        // ============================================================

        ScanResult QCXacNhanCuoi(
            int phieuXuLyId,
            int soLuongOK,
            int soLuongNG,
            string nguoiQC,
            int? slotIdOK = null,   // ✅ bắt buộc nếu soLuongOK > 0
            int? slotIdNG = null,   // ✅ bắt buộc nếu soLuongNG > 0
            string lotNo = null);  // ✅ LOT nhập lại
        


            // ============================================================
            // 8. GHI NHẬN KIỂM TRA TEM
            //
            // FormInspection / QC.
            //
            // Không phải transition của QTChungStatus.
            // ============================================================

            void GhiNhanKiemTraTem(
            int qcId,
            bool daKiemTra);


        // ============================================================
        // 9. NHẬP LẠI HÀNG NG
        //
        // Chỉ được thực hiện khi:
        //
        // QTChungStatus = DaQCXacNhanCuoi
        //
        // và:
        //
        // SoLuongNG > 0
        //
        // Transition:
        //
        // DaQCXacNhanCuoi
        //      ↓
        // DaNhapLaiKho
        // ============================================================

        ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuongNG,
            int? slotIdOK,
            int? slotIdNG,
            string nguoiNhap);


        // ============================================================
        // 10. HOÀN TẤT QT CHUNG
        //
        // Các transition hợp lệ theo QTChungStatusTransition:
        //
        // TuChoiGiaoBu
        //      ↓
        // HoanTat
        //
        // DaGiaoBu
        //      ↓
        // HoanTat
        //
        // DaQCXacNhanCuoi
        //      ↓
        // HoanTat
        //
        // DaNhapLaiKho
        //      ↓
        // HoanTat
        //
        // Service phải kiểm tra bằng:
        //
        // QTChungStatusTransition.IsValidTransition(...)
        //
        // Không repository tự quyết định.
        // ============================================================

        ScanResult HoanTat(
            int phieuXuLyId,
            string nguoiThucHien);


        // ============================================================
        // 11. GIAO LẠI BỘ PHẬN PHÁT HIỆN
        //
        // CHỈ ÁP DỤNG CHO:
        //
        // Nguon = TraNoiBo
        //
        // LƯU Ý QUAN TRỌNG:
        //
        // Đây KHÔNG phải transition của QTChungStatus.
        //
        // QTChung của TraNoiBo có thể đã:
        //
        // ... → DaNhapLaiKho → HoanTat
        //
        // Việc Header:
        //
        // DangXuLyQTChung
        //      ↓
        // ChoGiaoLaiBoPhan
        //      ↓
        // DaGiaoLaiBoPhan
        //      ↓
        // HoanTat
        //
        // thuộc PhieuTraHangStatus và phải do
        // TraNoiBoService / XuLyHangLoiServiceBase xử lý.
        //
        // Vì vậy method này chỉ thực hiện nghiệp vụ
        // giao lại bộ phận ở cấp QTChungService nếu kiến trúc
        // hiện tại của bạn vẫn đặt thao tác này tại đây.
        // ============================================================
        ScanResult XacNhanChoGiaoBu(int phieuXuLyId, string nguoiThucHien);

        ScanResult DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien);
        ScanResult GiaoLaiBoPhanPhatHien(
            int phieuXuLyId,
            string boPhanNhan,
            int soLuongGiaoLai,
            string nguoiThucHien);


        // ============================================================
        // 12. HUỶ QT CHUNG
        //
        // Transition được phép tùy theo from-state trong:
        //
        // QTChungStatusTransition
        //
        // Service bắt buộc validate:
        //
        // IsValidTransition(...)
        //
        // Không tự gán Status = Huy.
        // ============================================================

        ScanResult HuyQTChung(
            int phieuXuLyId,
            string lyDoHuy,
            string nguoiThucHien);


        // ============================================================
        // 13. TRA CỨU
        // ============================================================

        PhieuXuLyBatThuong GetById(
            int phieuXuLyId);


        //QTChungStatus GetTrangThai(
        //    int phieuXuLyId);


        // ============================================================
        // 14. LẤY CÁC STATE KẾ TIẾP
        //
        // Dùng cho UI / Service.
        //
        // Kết quả lấy từ:
        //
        // QTChungStatusTransition.GetAllowedNext(...)
        // ============================================================

        IReadOnlyList<QTChungStatus> GetAllowedNext(
            int phieuXuLyId);


        // ============================================================
        // 15. TIMELINE
        // ============================================================

        List<QTChungTimelineItem> GetTimeline(
            int phieuXuLyId);


    }
}