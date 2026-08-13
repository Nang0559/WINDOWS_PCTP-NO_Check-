using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class PXuatINModel
    {
        public string LoaiPhieu { get; set; }
        public string Ca { get; set; }
        public string SoThuTuXe { get; set; }
        public string TenSanPham { get; set; }
        public string MaSanPham { get; set; }
        public string LotNo { get; set; }
        public int SoLuong { get; set; }
        public string CheckTem { get; set; }
        public string NguoiThucHien { get; set; }
        public string QrData { get; set; }

        // Thống kê lịch sử quản lý sản xuất phía dưới
        public string Ngay { get; set; }
        public string Gio { get; set; }
        public int SoLuongXuat { get; set; }
        public string NguoiXuat { get; set; }
        public int SoLuongTon { get; set; }
    }
}
