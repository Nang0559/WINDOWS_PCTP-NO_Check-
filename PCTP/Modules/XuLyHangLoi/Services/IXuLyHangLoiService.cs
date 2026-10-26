using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{

    /// <summary>
    /// Hành vi chung cho các service xử lý hàng lỗi:
    ///     - KhachTra
    ///     - TraNoiBo
    ///
    /// Base chỉ xử lý state machine ở cấp HEADER:
    ///
    ///     PhieuTraHangStatus
    ///
    /// Không xử lý QTChungStatus.
    ///
    /// QTChungStatus thuộc PhieuXuLyBatThuong và do QTChungService
    /// chịu trách nhiệm.
    /// </summary>
    public interface IXuLyHangLoiService
    {
        /// <summary>
        /// Lấy phiếu theo Id.
        ///
        /// Chỉ trả về phiếu thuộc đúng Nguon của service.
        /// </summary>
        PhieuTraHang GetById(int id);

        /// <summary>
        /// Lấy các phiếu chưa hoàn tất thuộc đúng Nguon.
        /// </summary>
        List<PhieuTraHang> GetChoXuLy();

        /// <summary>
        /// Chuyển trạng thái Header theo state machine
        /// PhieuTraHangStatusTransition.
        ///
        /// Repository chỉ persistence.
        /// Service chịu trách nhiệm validate transition.
        ///
        /// Nếu status hiện tại == status yêu cầu:
        ///     không làm gì (idempotent).
        /// </summary>
        void CapNhatTrangThai(
            int id,
            PhieuTraHangStatus status,
            string nguoiThucHien);

        /// <summary>
        /// true nếu phiếu đã đóng (HoanTat) → form chỉ được xem.
        /// </summary>
        bool LaPhieuDaDong(PhieuTraHang phieu);

        /// <summary>
        /// true nếu đã có PhieuXuLyBatThuong sinh từ phiếu này
        /// → chỉ được sửa header, không sửa/xóa dòng chi tiết và không xóa phiếu.
        /// </summary>
        bool CoPhieuXuLy(int phieuTraHangId);

        /// <summary>
        /// Lưu thay đổi phiếu đã tạo. Header luôn được cập nhật;
        /// ChiTiet chỉ được thay thế khi phieu.ChiTiet != null
        /// và phiếu chưa có PhieuXuLyBatThuong.
        /// Ném lỗi nếu phiếu đã đóng.
        /// </summary>
        void CapNhatPhieu(PhieuTraHang phieu, string nguoiThucHien);

        /// <summary>
        /// Xóa phiếu (header + chi tiết). Ném lỗi nếu phiếu đã đóng
        /// hoặc đã có PhieuXuLyBatThuong.
        /// </summary>
        void XoaPhieu(int id, string nguoiThucHien);
    }
}
