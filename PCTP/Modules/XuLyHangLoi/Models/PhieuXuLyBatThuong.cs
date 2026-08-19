using PCTP.Modules.XuLyHangLoi.Enum;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public class PhieuXuLyBatThuong
    {
        public int Id { get; set; }

        public NguonXuLyBatThuong Nguon { get; set; }

        public string SoPhieu { get; set; }

        public int? PhieuKhachTraId { get; set; }

        public string Model { get; set; }

        public string MaSanPham { get; set; }

        public string SoLo { get; set; }

        public string SoLoLoi { get; set; }

        public int SoLuongLoi { get; set; }

        public string NoiDungBatThuong { get; set; }

        public string PhanLoaiXuLy { get; set; }

        public string BoPhanPhatHanh { get; set; }

        // State machine của QTChung
        public QTChungStatus Status { get; set; }

        public string HuongXuLy { get; set; }

        public DateTime? NgayDinhHuong { get; set; }

        public string NguoiDinhHuong { get; set; }

        public DateTime CreatedAt { get; set; }

        public string CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedBy { get; set; }
    }
}
