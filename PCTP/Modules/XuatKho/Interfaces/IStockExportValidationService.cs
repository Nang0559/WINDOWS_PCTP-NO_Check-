using PCTP.Modules.XuatKho.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Interfaces
{
    public interface IStockExportValidationService
    {
        /// <summary>Validate cho bước 1 (pick khỏi Slot vào ChoGiao) — KHÔNG check STOCKTP.</summary>
        StockExportValidationResult ValidatePickToChoGiao(StockExportRequest request);

        /// <summary>Validate cho xuất trực tiếp (A0 giao thẳng / Rework) — CÓ check STOCKTP.</summary>
        StockExportValidationResult ValidateXuatTrucTiep(StockExportRequest request);
    }
}
