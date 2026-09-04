using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public class PhieuTraHang
    {
        public int Id { get; set; }

        /// <summary>
        /// Nguồn phát sinh phiếu:
        /// KhachTra hoặc TraNoiBo.
        /// </summary>
        public NguonXuLyBatThuong Nguon { get; set; }

        /// <summary>
        /// Số phiếu trả hàng nội bộ của hệ thống.
        /// </summary>
        public string SoPhieu { get; set; }

        /// <summary>
        /// Khách hàng trả hàng.
        /// Chỉ có giá trị khi Nguon = KhachTra.
        /// </summary>
        public NguonKhachTra? NguonKhachTra { get; set; }

        /// <summary>
        /// Số phiếu/chứng từ do khách cung cấp.
        /// Chỉ dùng cho KhachTra.
        /// </summary>
        public string SoPhieuKhach { get; set; }

        public DateTime? NgayPhatHanh { get; set; }

        public string SlipNo { get; set; }

        /// <summary>
        /// Ca trả hàng.
        /// Chủ yếu dùng cho KhachTra.
        /// </summary>
        public string Ca { get; set; }

        /// <summary>
        /// Phòng ban đề xuất trả hàng.
        /// Chủ yếu dùng cho TraNoiBo.
        /// </summary>
        public string PhongBan { get; set; }

        public string LyDo { get; set; }

        public string TenKhachHang { get; set; }

        public string BoPhanPhatHienLoi { get; set; }

        public string XacNhanBPPhatHienLoi { get; set; }

        public string XacNhanQCKhach { get; set; }

        public string XacNhanNhaCungCap { get; set; }

        public DateTime? NgayNhanKho { get; set; }

        public int TongSoLuongNhan { get; set; }

        // ============================================================
        // STATE
        // ============================================================

        /// <summary>
        /// State machine cấp Header.
        /// Đây là nguồn sự thật duy nhất về trạng thái tổng thể.
        /// </summary>
        public PhieuTraHangStatus Status { get; set; }

        // ============================================================
        // GIAO LẠI BỘ PHẬN - chỉ dùng cho TraNoiBo
        // ============================================================

        public string BoPhanNhanLai { get; set; }

        public int? SoLuongGiaoLai { get; set; }

        public DateTime? NgayGiaoLaiBoPhan { get; set; }

        public string NguoiGiaoLaiBoPhan { get; set; }

        public string Note { get; set; }

        // ============================================================
        // AUDIT
        // ============================================================

        public DateTime CreatedAt { get; set; }

        public string CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedBy { get; set; }

        // ============================================================
        // DETAIL
        // ============================================================

        public List<PhieuTraHangCT> ChiTiet { get; set; }
            = new List<PhieuTraHangCT>();
    }
}
