
using System.Collections.Generic;

namespace PCTP.Modules.KhoCore.Models
{
    public class Warehouse
    {
        public string Name { get; set; }  // Đảm bảo thuộc tính Name có mặt ở đây
        public List<Rack> Racks { get; set; }
    }
}
