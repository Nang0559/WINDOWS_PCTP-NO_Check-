using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class Warehouse
    {
        public string Name { get; set; }  // Đảm bảo thuộc tính Name có mặt ở đây
        public List<Rack> Racks { get; set; }
    }
}
