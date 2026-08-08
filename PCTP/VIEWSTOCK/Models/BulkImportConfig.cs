using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Models
{
    public static class BulkImportConfig
    {
        public const string WarehouseName = "KHO_AO_NHAP_LOAT";
        public const string RackName = "RACK_AO";
        public const int Capacity = 999999999;
        public static bool IsBulkSlot(Slot slot) =>
        slot != null &&
        string.Equals(slot.whname, WarehouseName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(slot.RackName, RackName, StringComparison.OrdinalIgnoreCase);
    }
}
