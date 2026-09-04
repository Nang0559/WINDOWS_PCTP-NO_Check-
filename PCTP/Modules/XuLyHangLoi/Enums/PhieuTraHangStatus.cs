using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Enums
{
    public enum PhieuTraHangStatus
    {
        /// <summary>
        /// Header mới được tạo.
        /// Chưa bắt đầu quy trình xử lý.
        /// </summary>
        Moi = 0,

        /// <summary>
        /// Header đã sẵn sàng để tạo PhieuXuLyBatThuong
        /// cho các dòng PhieuTraHangCT.
        /// </summary>
        ChoTaoPhieuBatThuong = 10,

        /// <summary>
        /// Có ít nhất một dòng PhieuTraHangCT
        /// đã được liên kết với PhieuXuLyBatThuong.
        ///
        /// Đây là trạng thái roll-up ở cấp Header,
        /// không mô tả bước xử lý chi tiết.
        /// </summary>
        DaTaoPhieuBatThuong = 20,

        /// <summary>
        /// Có ít nhất một PhieuXuLyBatThuong
        /// đang thực hiện QTChung và toàn bộ Header
        /// chưa hoàn tất.
        ///
        /// Chi tiết đang ở bước nào phải xem QTChungStatus.
        /// </summary>
        DangXuLyQTChung = 30,

        /// <summary>
        /// Chỉ áp dụng cho Nguon = TraNoiBo.
        ///
        /// QTChung đã hoàn tất phần xử lý hàng,
        /// còn hàng OK cần giao lại cho bộ phận nhận.
        /// </summary>
        ChoGiaoLaiBoPhan = 75,

        /// <summary>
        /// Chỉ áp dụng cho Nguon = TraNoiBo.
        ///
        /// Hàng OK đã được giao lại cho bộ phận nhận.
        /// </summary>
        DaGiaoLaiBoPhan = 80,

        /// <summary>
        /// Toàn bộ các dòng xử lý thuộc Header
        /// đã kết thúc.
        /// </summary>
        HoanTat = 100,

        /// <summary>
        /// Quy trình đang ở trạng thái lỗi nghiệp vụ
        /// và cần xử lý/retry.
        /// </summary>
        Loi = 900
    }
}
