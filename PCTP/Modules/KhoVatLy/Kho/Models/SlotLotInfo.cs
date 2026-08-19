using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Kho.Models
{
    /// <summary>
    /// Đại diện 1 dòng SlotLot cụ thể (theo SlotLotId) — dùng cho các nghiệp vụ
    /// cần thao tác trên ĐÚNG 1 dòng LOT-trong-Slot thay vì toàn bộ Slot (rework,
    /// xuất một phần LOT...). Khác với LotInfo (không có SlotLotId/SlotId, dùng
    /// cho các luồng thao tác cả Slot).
    /// </summary>
    public sealed class SlotLotInfo
    {
        public int SlotLotId { get; set; }
        public int SlotVatLyId { get; set; }   // = SlotLot.SlotId — Slot vật lý chứa dòng này
        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
        public string TemCode { get; set; }
    }
}
