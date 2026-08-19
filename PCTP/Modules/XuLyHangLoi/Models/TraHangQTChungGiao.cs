using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{

    /// <summary>
    /// Một dòng hàng giao cho sản xuất để thực hiện rework.
    ///
    /// Không phải giao khách.
    /// Đây là giao nội bộ:
    ///
    /// KHO → SẢN XUẤT
    /// </summary>
    public class TraHangQTChungGiao
    {
        public int Id { get; set; }

        public int PhieuXuLyId { get; set; }

        public string LotNo { get; set; }

        public string MaHang { get; set; }

        public int SoLuong { get; set; }

        public DateTime ThoiGian { get; set; }

        public string NguoiGiao { get; set; }

        public string NguoiNhan { get; set; }

        public string BoPhanNhan { get; set; }

        /// <summary>
        /// Số phiếu giao nhận nội bộ.
        /// </summary>
        public string SoPhieuGiaoNhan { get; set; }

        public string Note { get; set; }
    }

}
