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

        /// <summary>Stable idempotency key supplied by the initiating UI command and reused on retry.</summary>
        public Guid OperationId { get; set; }

        public int PhieuXuLyBatThuongId { get; set; }

        public int SlotIdNguon { get; set; }

        public string LotXuat { get; set; }
        public string LoaiXuat { get; set; }// "Rework" | "GiaoBuNG"

        public string MaHang { get; set; }

        public int SoLuongXuat { get; set; }

        /// <summary>
        /// Tồn trước khi xuất.
        /// </summary>
        public int TonTruoc { get; set; }

        /// <summary>
        /// Tồn sau khi xuất.
        /// </summary>
        public int TonSau { get; set; }

        public DateTime NgayXuat { get; set; }

        public string NguoiXuat { get; set; }

        public string LyDo { get; set; }

        public string Note { get; set; }
    }

}
