using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public class InspectionConfig
    {
        public int ConfigId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public int DefaultQty { get; set; } = 1;
        public bool CheckItemCode { get; set; } = true;
        public bool CheckLotNo { get; set; } = true;
        public bool CheckNSX { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
