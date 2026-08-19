using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
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

