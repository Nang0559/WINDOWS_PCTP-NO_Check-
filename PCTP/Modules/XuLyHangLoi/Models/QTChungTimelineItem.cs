using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{

    /// <summary>
    /// Một sự kiện trong timeline của QTChung.
    ///
    /// Đây là DTO/read model, KHÔNG map trực tiếp
    /// vào một bảng duy nhất.
    /// </summary>
    public class QTChungTimelineItem
    {
        public int PhieuXuLyId { get; set; }

        /// <summary>
        /// XUAT / GIAO / QC / NHAP_NG
        /// </summary>
        public string Buoc { get; set; }

        public int? RefId { get; set; }

        public DateTime ThoiGian { get; set; }

        public string LotNo { get; set; }

        public string MaHang { get; set; }

        public int? SoLuong { get; set; }

        public string NguoiThucHien { get; set; }

        public string NoiDung { get; set; }

        public string Note { get; set; }
    }

}
