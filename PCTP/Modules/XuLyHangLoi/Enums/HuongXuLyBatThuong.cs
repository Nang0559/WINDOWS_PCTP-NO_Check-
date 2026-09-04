using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Enums
{
    /// <summary>
    /// Kết luận của QC ở bước Định Hướng (IQTChungService.QCDinhHuongRework) —
    /// quyết định QTChungStatus rẽ nhánh nào tiếp theo. Lưu trên PhieuXuLyBatThuong.
    /// </summary>
    public enum HuongXuLyBatThuong
    {
        /// <summary>
        /// Chưa xác định hướng xử lý.
        /// Chỉ hợp lệ trước khi định hướng.
        /// </summary>
        ChuaXacDinh = 0,

        /// <summary>
        /// Chỉ dành cho KhachTra.
        /// Xác định không phải lỗi thật.
        /// Không giao bù, không rework.
        /// </summary>
        TuChoiGiaoBu = 1,

        /// <summary>
        /// Chỉ dành cho KhachTra.
        /// Có lỗi nhưng không cần rework.
        /// Thực hiện giao bù trực tiếp.
        /// </summary>
        ChiGiaoBu = 2,

        /// <summary>
        /// Luồng cần rework.
        /// Áp dụng cho TraNoiBo và KhachTra.
        /// </summary>
        CanRework = 3
    }
}
