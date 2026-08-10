using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Models
{
    public class LichSuGiaoHangInfo
    {
        public string Lot { get; set; }
        public string MaHang { get; set; }
        public string TenHang { get; set; }
        public int SoLuong { get; set; }
        public System.DateTime? NgayGiao { get; set; }
        public string GioGiao { get; set; }
        public string NhaMay { get; set; }
        public string Cua { get; set; }
        public string Truyen { get; set; }
    }

    public class LichSuQrCodeInfo
    {
        public string LotFcc { get; set; }
        public string MaHangFcc { get; set; }
        public int SlTemFcc { get; set; }
        public string LotHvn { get; set; }
        public string MaHangHvn { get; set; }
        public int SlTemHvn { get; set; }
        public string KetQua { get; set; }
        public System.DateTime? NgayXuat { get; set; }
        public string GioXuat { get; set; }
        public string NhaMay { get; set; }
    }

    /// <summary>Kết quả tìm LOT theo Mã hàng + khoảng ngày giao, gộp theo LOT.</summary>
    public class LotUngVienInfo
    {
        public string Lot { get; set; }
        public string MaHang { get; set; }
        public System.DateTime? NgayGiao { get; set; }
        public int TongSlDaGiaoTheoLot { get; set; }
        public int SoPhieuGiao { get; set; }
    }
}
