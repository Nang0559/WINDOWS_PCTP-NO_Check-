using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public class PhieuKhachTraItem
    {
        public int Id { get; set; }

        public int PhieuKhachTraId { get; set; }

        public string MaHang { get; set; }

        public string TenHang { get; set; }

        public string LotNo { get; set; }

        public int SoLuong { get; set; }

        public string NoiDungLoi { get; set; }

        // Phiếu giao hàng gốc được xác định là ứng viên
        public string DinhDanhPhieuGiao { get; set; }

        // Thông tin giao hàng tại thời điểm đối chiếu
        public string PoNo { get; set; }

        public DateTime? NgayGiao { get; set; }

        public string NhaMay { get; set; }
    }
}
