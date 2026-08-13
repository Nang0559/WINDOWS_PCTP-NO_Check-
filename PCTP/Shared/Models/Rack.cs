using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class Rack
    {
        public string Name { get; set; }
        public int RowCount { get; set; }    // Số tầng (hàng)
        public int ColumnCount { get; set; } // Số slot mỗi tầng
        public List<Slot> Slots { get; set; }
    }
}

