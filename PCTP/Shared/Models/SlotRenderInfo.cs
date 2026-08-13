using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class SlotRenderInfo
    {
        public Slot Slot;
        public string RackName;
        public string WarehouseName;
        public int Row { get; set; }      // mới
        public int Column { get; set; }   // mới
    }
    //public class SlotRenderInfo
    //{
    //    public string SlotNumber { get; set; }
    //    public string ItemCode { get; set; }
    //    public int Quantity { get; set; }
    //    public bool IsOccupied { get; set; }
    //}

}
