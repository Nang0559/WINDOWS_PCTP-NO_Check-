using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
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
