using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Entities
{
    public class GhepLotItem
    {
        public string MaHang { get; set; }
        public int GioXuat { get; set; }
        public string Lot { get; set; }
        /// <summary>Tên Gear (B20Gear.Name) của dòng ghép — chỉ YMVN (CoGear) có; khách khác để rỗng.</summary>
        public string GearName { get; set; }
    }
}
