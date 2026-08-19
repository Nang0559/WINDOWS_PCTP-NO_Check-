using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{

    /// <summary>
    /// Một lần xuất hàng khỏi kho để đưa đi rework.
    ///
    /// Quan hệ:
    /// PhieuXuLyBatThuong
    ///      1
    ///      |
    ///      N
    /// TraHangQTChungXuat
    ///
    /// Đây là lịch sử nghiệp vụ, KHÔNG phải tồn kho hiện tại.
    /// Tồn kho thực tế vẫn nằm ở Slot / STOCKTP.
    /// </summary>
    public class TraHangQTChungXuat
    {
        public int Id { get; set; }

        public int PhieuXuLyId { get; set; }

        public int SlotId { get; set; }

        public string LotNo { get; set; }

        public string MaHang { get; set; }

        public int SoLuong { get; set; }

        /// <summary>
        /// Tồn trước khi xuất.
        /// </summary>
        public int TonTruoc { get; set; }

        /// <summary>
        /// Tồn sau khi xuất.
        /// </summary>
        public int TonSau { get; set; }

        public DateTime ThoiGian { get; set; }

        public string NguoiXuat { get; set; }

        public string LyDo { get; set; }

        public string Note { get; set; }
    }

}
