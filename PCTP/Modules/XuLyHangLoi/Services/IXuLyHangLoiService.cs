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
    }
}
