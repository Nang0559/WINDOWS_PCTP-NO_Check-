using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{
    public static class StockExportReferenceFormatter
    {
        public static string Format(StockExportReferenceType? type, int? id)
        {
            if (type == null || id == null) return null;
            switch (type.Value)
            {
                case StockExportReferenceType.PhieuGiao: return $"PGH#{id}";
                case StockExportReferenceType.ChoGiaoBu: return $"CGB#{id}";
                case StockExportReferenceType.PhieuXuLyBatThuong: return $"XLBT#{id}";
                case StockExportReferenceType.PhieuKhachTra: return $"KTR#{id}";
                default: return id.ToString();
            }
        }
    }
}
