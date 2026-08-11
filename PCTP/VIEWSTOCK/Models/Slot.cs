using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class Slot
    {
        public int SlotId { get; set; }

        public string whname { get; set; }

        public string RackName { get; set; }

        public int Rackid { get; set; }

        public int SlotNumber { get; set; }

        public int RowIndex { get; set; }

        public int ColumnIndex { get; set; }

        public bool IsOccupied { get; set; }

        public string ItemCode { get; set; }

        public int Quantity { get; set; }

        public int Capacity { get; set; }

        public DateTime? ImportDate { get; set; }

        // Danh sách Lot đang nằm trong Slot
        public List<LotInfo> Lots { get; set; } = new List<LotInfo>();
        public string TemCode =>
            Lots != null && Lots.Count > 0
                ? string.Join(",", Lots.Where(l => !string.IsNullOrWhiteSpace(l.TemCode)).Select(l => l.TemCode))
                : "";

        public string LotNo =>
            Lots != null && Lots.Count > 0
                ? string.Join(",", Lots.Where(l => !string.IsNullOrWhiteSpace(l.LotNo)).Select(l => l.LotNo))
                : "";
    }
    public class SlotChuaLotInfo
    {
        public int SlotId { get; set; }
        public string WarehouseName { get; set; }
        public string RackName { get; set; }
        public int SlotNumber { get; set; }
        public int Quantity { get; set; }
        public string TemCode { get; set; }
        public DateTime? ImportDate { get; set; }
    }
}
