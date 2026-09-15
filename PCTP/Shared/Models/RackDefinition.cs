using PCTP.Modules.KhoCore.Models;
using System.Collections.Generic;


namespace PCTP.Shared.Models
{
    public class RackDefinition
    {
        public string WarehouseName { get; set; }
        public string RackName { get; set; }
        public int RackId { get; set; }
        public int SlotCount { get; set; }
        public int RowCount { get; set; }     // Số tầng
        public int ColumnCount { get; set; }  // Số cột (slot mỗi tầng)
        public List<Slot> Slots { get; set; } = new List<Slot>();
    }

}
