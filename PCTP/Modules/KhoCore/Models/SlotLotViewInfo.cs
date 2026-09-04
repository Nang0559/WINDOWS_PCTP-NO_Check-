using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Models
{
    /// <summary>Dòng hiển thị 1 SlotLot kèm toạ độ vật lý — dùng cho các màn hình
    /// chọn Slot/LOT nguồn (VD: FormChonSlotNoiBo).</summary>
    public sealed class SlotLotViewInfo
    {
        public int SlotId { get; set; }
        public int SlotLotId { get; set; }
        public string WarehouseName { get; set; }
        public string RackName { get; set; }
        public int SlotNumber { get; set; }
        public string ItemCode { get; set; }
        public string LotNo { get; set; }
        public int Quantity { get; set; }
        public string TemCode { get; set; }
    }
}
