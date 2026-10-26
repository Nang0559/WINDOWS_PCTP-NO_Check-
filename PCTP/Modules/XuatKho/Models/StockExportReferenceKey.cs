using System;

namespace PCTP.Modules.XuatKho.Models
{
    internal static class StockExportReferenceKey
    {
        public static string Build(StockExportReferenceType? type, int? id)
        {
            return StockExportReferenceFormatter.Format(type, id);
        }
    }
}