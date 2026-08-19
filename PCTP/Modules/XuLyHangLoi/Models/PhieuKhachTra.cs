using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public class PhieuKhachTra
    {
        public int Id { get; set; }

        /// <summary>
        /// Nguồn chứng từ:
        /// KhachTra hoặc TraNoiBo.
        /// </summary>
        public NguonXuLyBatThuong Nguon { get; set; }

        /// <summary>
        /// Số phiếu.
        /// Khách trả: do khách cung cấp.
        /// Nội bộ: hệ thống tự sinh.
        /// </summary>
        public string SoPhieu { get; set; }

        public DateTime? NgayPhatHanh { get; set; }

        public string SlipNo { get; set; }

        public string TenKhachHang { get; set; }

        public string BoPhanPhatHienLoi { get; set; }

        public string XacNhanBPPhatHienLoi { get; set; }

        public string XacNhanQCKhach { get; set; }

        public string XacNhanNhaCungCap { get; set; }

        public DateTime? NgayNhanKho { get; set; }

        public int TongSoLuongNhan { get; set; }

        public PhieuTraHangStatus Status { get; set; }

        public bool DaTaoPhieuBatThuong { get; set; }

        public bool DaHoanTatQTChung { get; set; }

        public bool DaGiaoBu { get; set; }

        public string Note { get; set; }

        public DateTime CreatedAt { get; set; }

        public string CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedBy { get; set; }

        public List<PhieuKhachTraItem> Items { get; set; }
            = new List<PhieuKhachTraItem>();
    }
}
