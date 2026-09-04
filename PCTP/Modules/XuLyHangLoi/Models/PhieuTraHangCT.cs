using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public class PhieuTraHangCT
    {
        public int Id { get; set; }

        /// <summary>
        /// FK tới phiếu trả hàng Header.
        /// </summary>
        public int PhieuTraHangId { get; set; }

        // ============================================================
        // NGUỒN KHO
        // ============================================================

        /// <summary>
        /// Slot chứa hàng tại thời điểm tạo phiếu trả nội bộ.
        ///
        /// Chỉ sử dụng khi:
        ///     PhieuTraHang.Nguon = NguonXuLyBatThuong.TraNoiBo
        ///
        /// Với KhachTra, giá trị này để null.
        /// </summary>
        public int? SlotIdNguon { get; set; }

        // ============================================================
        // THÔNG TIN HÀNG
        // ============================================================

        /// <summary>
        /// Mã hàng.
        /// </summary>
        public string MaHang { get; set; }

        /// <summary>
        /// Tên hàng tại thời điểm lập phiếu.
        /// </summary>
        public string TenHang { get; set; }

        /// <summary>
        /// Số lot của hàng trả.
        /// </summary>
        public string LotNo { get; set; }

        /// <summary>
        /// Số lượng hàng trả của dòng.
        /// </summary>
        public int SoLuong { get; set; }

        /// <summary>
        /// Lý do NG của dòng hàng.
        /// Có thể null đối với trường hợp chưa xác định kết quả xử lý.
        /// </summary>
        public string LyDoNg { get; set; }

        // ============================================================
        // ĐỐI CHIẾU PHIẾU GIAO GỐC
        // ============================================================

        /// <summary>
        /// Khóa định danh phiếu giao gốc được xác định là ứng viên
        /// cho dòng hàng này.
        ///
        /// Giá trị được cập nhật sau bước đối chiếu với
        /// IPhieuGiaoRepository.
        /// </summary>
        public string DinhDanhPhieuGiao { get; set; }

        /// <summary>
        /// Số PO của phiếu giao gốc được xác định.
        /// </summary>
        public string PoNo { get; set; }

        /// <summary>
        /// Ngày giao của phiếu giao gốc được xác định.
        /// </summary>
        public DateTime? NgayGiao { get; set; }

        /// <summary>
        /// Nhà máy nhận hàng của phiếu giao gốc được xác định.
        /// </summary>
        public string NhaMay { get; set; }
    }
}
