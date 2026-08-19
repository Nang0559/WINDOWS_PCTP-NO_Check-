using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{

    public sealed class StockExportResult
    {
        public StockExportStatus Status { get; private set; }

        public bool IsOK =>
            Status == StockExportStatus.Success;

        public string Message { get; private set; }

        /// <summary>
        /// Danh sách LOT/thùng thực tế đã xuất.
        /// Một request có thể tạo nhiều item nếu xuất từ nhiều slot.
        /// </summary>
        public List<StockExportItem> ExportedItems { get; private set; }
            = new List<StockExportItem>();

        private StockExportResult()
        {
        }

        public static StockExportResult Ok(
            List<StockExportItem> exportedItems,
            string message = "")
        {
            return new StockExportResult
            {
                Status = StockExportStatus.Success,
                ExportedItems =
                    exportedItems ?? new List<StockExportItem>(),
                Message = message
            };
        }

        public static StockExportResult Fail(
            string message)
        {
            return new StockExportResult
            {
                Status = StockExportStatus.Failed,
                Message = message
            };
        }

        public static StockExportResult InsufficientStock(
            string message)
        {
            return new StockExportResult
            {
                Status = StockExportStatus.InsufficientStock,
                Message = message
            };
        }

        public static StockExportResult Duplicate(
            string message)
        {
            return new StockExportResult
            {
                Status = StockExportStatus.Duplicate,
                Message = message
            };
        }
    }
}


  
