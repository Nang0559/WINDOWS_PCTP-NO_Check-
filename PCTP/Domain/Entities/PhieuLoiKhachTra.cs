using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Entities
{
    public enum NguonKhachTra { HVN = 1, YMVN = 2 }

    public class PhieuLoiKhachTra
    {
        public int Id { get; set; }
        public NguonKhachTra Nguon { get; set; }
        public string SoPhieuKhach { get; set; }   // "NG 4357/0021" hoặc "Slip No 18883"
        public DateTime NgayPhatHanh { get; set; }
        public string SlipNo { get; set; }
        public string Ca { get; set; }
        public string NguoiTao { get; set; }
        public DateTime NgayTao { get; set; } = DateTime.Now;
        public List<PhieuLoiKhachTraCT> ChiTiet { get; set; } = new List<PhieuLoiKhachTraCT>();
    }

    public class PhieuLoiKhachTraCT
    {
        public int Id { get; set; }
        public int PhieuLoiKhachTraId { get; set; }
        public int Stt { get; set; }
        public string Model { get; set; }
        public string MaHang { get; set; }
        public string TenHang { get; set; }
        public string SoLo { get; set; }
        public int SoLuong { get; set; }
        public string NoiDungLoi { get; set; }
        public bool CoPhieuLoi { get; set; }   // cột "PC ~ Có/Không" trên phiếu ảnh 3
        public string GhiChu { get; set; }

        // Liên kết sang phiếu xử lý bất thường (1 dòng CT có thể sinh 1 PhieuXuLyBatThuong)
        public int? PhieuXuLyBatThuongId { get; set; }
    }
}
