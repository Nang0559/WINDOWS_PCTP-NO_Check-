using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Models
{
    public sealed class SlotInfo
    {
        public int SlotId { get; set; }

        public string WarehouseName { get; set; }

        public string RackName { get; set; }

        public int SlotNumber { get; set; }

        public int Capacity { get; set; }
    }
}
