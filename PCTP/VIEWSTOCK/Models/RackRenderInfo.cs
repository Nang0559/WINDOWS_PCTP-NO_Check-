using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    
    public class RackRenderInfo
    {
        public int RackId { get; set; }
        public string WarehouseName { get; set; }
        public string RackName { get; set; }
        public int RowCount { get; set; }        // thêm dòng này
        public int ColumnCount { get; set; }     // thêm dòng này
        public int SlotCount { get; set; }
        public int EmptySlotCount { get; set; }
        public List<SlotRenderInfo> Slots { get; set; } = new List<SlotRenderInfo>();
        public Dictionary<string, (int Count, int TotalQty)> ItemSummary { get; set; } = new Dictionary<string, (int Count, int TotalQty)>();
    }

}
