using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{
    internal static class StockExportReferenceKey
    {
        public static string Build(StockExportReferenceType? type, int? id)
            => type.HasValue && id.HasValue ? $"{(int)type.Value}#{id.Value}" : null;
    }
}
