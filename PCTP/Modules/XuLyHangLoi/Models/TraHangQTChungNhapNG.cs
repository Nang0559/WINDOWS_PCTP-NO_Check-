using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{

    /// <summary>
    /// Một lần nhập lại hàng NG vào kho sau khi QC xác nhận cuối.
    ///
    /// QTChung:
    ///
    /// QC xác nhận NG
    ///       ↓
    /// Nhập NG vào kho
    /// </summary>
    public class TraHangQTChungNhapNG
    {
        public int Id { get; set; }

        public int PhieuXuLyId { get; set; }

        public string LotNo { get; set; }

        public string MaHang { get; set; }

        public int SoLuongNG { get; set; }

        public int? SlotIdNhap { get; set; }

        public DateTime ThoiGian { get; set; }

        public string NguoiNhap { get; set; }

        public string LyDo { get; set; }

        public string Note { get; set; }
    }

}
