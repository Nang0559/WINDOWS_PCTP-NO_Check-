using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Enums
{
    /// <summary>
    /// State machine con — gắn với PhieuXuLyBatThuong, mô tả chi tiết từng bước
    /// QC/Rework. Khác cấp với PhieuTraHangStatus (gắn PhieuKhachTra, chỉ có 1 mốc
    /// "DangXuLyQTChung" bao trùm toàn bộ enum này).
    /// </summary>
    public enum QTChungStatus
    {
        /// <summary>
        /// Phiếu xử lý bất thường mới được tạo.
        /// Chưa bắt đầu xử lý.
        /// </summary>
        Moi = 0,

        /// <summary>
        /// Phiếu xử lý bất thường đã được tạo và liên kết.
        /// </summary>
        DaTaoPhieuBatThuong = 10,

        /// <summary>
        /// Đã xác định hướng xử lý:
        /// TuChoiGiaoBu / ChiGiaoBu / CanRework.
        /// </summary>
        DaDinhHuong = 20,

        // ============================================================
        // NHÁNH 1: TỪ CHỐI GIAO BÙ
        // ============================================================

        /// <summary>
        /// Xác định không phải lỗi thật.
        /// Không giao bù, không rework.
        /// </summary>
        TuChoiGiaoBu = 25,

        // ============================================================
        // NHÁNH 2: CHỈ GIAO BÙ
        // ============================================================

        /// <summary>
        /// Đã tạo yêu cầu giao bù.
        /// Đang chờ giao bù hoàn tất.
        /// </summary>
        ChoGiaoBu = 30,

        /// <summary>
        /// Giao bù đã hoàn tất.
        /// </summary>
        DaGiaoBu = 35,

        // ============================================================
        // NHÁNH 3: REWORK
        // ============================================================

        /// <summary>
        /// Hàng đã được xuất khỏi kho để đưa đi rework.
        /// </summary>
        DaXuatKhoRework = 40,

        /// <summary>
        /// Đã ghi nhận giao hàng cho sản xuất/rework.
        /// Không thay đổi tồn kho.
        /// </summary>
        DaGiaoSanXuat = 50,

        /// <summary>
        /// QC đã xác nhận kết quả cuối:
        /// OK / NG.
        /// </summary>
        DaQCXacNhanCuoi = 60,

        /// <summary>
        /// Hàng NG sau QC đã được nhập lại kho.
        /// Chỉ xuất hiện khi SoLuongNG > 0.
        /// </summary>
        DaNhapLaiKho = 70,

        // ============================================================
        // KẾT THÚC
        // ============================================================

        /// <summary>
        /// Phiếu xử lý bất thường đã hoàn tất.
        /// </summary>
        HoanTat = 100,

        /// <summary>
        /// Phiếu bị hủy.
        /// </summary>
        Huy = 900
    }
}
