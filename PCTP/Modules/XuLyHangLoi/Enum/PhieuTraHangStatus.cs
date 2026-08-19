using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public enum PhieuTraHangStatus
    {
        Moi = 0,

        /// <summary>
        /// Đã nhập chứng từ khách nhưng chưa tạo phiếu bất thường.
        /// </summary>
        ChoTaoPhieuBatThuong = 10,

        /// <summary>
        /// Đã tạo phiếu bất thường.
        /// </summary>
        DaTaoPhieuBatThuong = 20,

        /// <summary>
        /// Đang chạy QTChung.
        /// </summary>
        DangXuLyQTChung = 30,

        /// <summary>
        /// QC đã xác nhận kết quả cuối.
        /// </summary>
        QCDaXacNhan = 40,

        /// <summary>
        /// Hàng OK đã nhập lại kho.
        /// </summary>
        DaNhapLaiKho = 50,

        /// <summary>
        /// Đang chờ giao bù cho khách.
        /// </summary>
        ChoGiaoBu = 60,

        /// <summary>
        /// Đã giao bù đầy đủ.
        /// </summary>
        DaGiaoBu = 70,

        /// <summary>
        /// Hoàn tất toàn bộ quy trình.
        /// </summary>
        HoanTat = 100,

        /// <summary>
        /// Có lỗi cần xử lý lại.
        /// </summary>
        Loi = 900
    }
}
