
using System.Collections.Generic;


namespace PCTP.Modules.KhoCore.Models
{
    public class Rack
    {
        public int RackId { get; set; }

        public int WarehouseId { get; set; }

        public string Name { get; set; }

        public int RackRowCount { get; set; }

        public int ColumnCount { get; set; }

        public List<Slot> Slots { get; set; }
            = new List<Slot>();
    }
}

