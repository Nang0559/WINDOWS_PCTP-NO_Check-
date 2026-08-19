using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{
    public enum StockExportStatus
    {
        Success = 1,
        Failed = 2,
        InsufficientStock = 3,
        Duplicate = 4,
        OverCapacityAtDestination = 5
    }
}
