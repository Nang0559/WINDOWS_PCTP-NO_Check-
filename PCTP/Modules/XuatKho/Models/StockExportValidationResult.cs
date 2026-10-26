using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Models
{
    public sealed class StockExportValidationResult
    {
        public bool IsValid { get; }
        public StockExportStatus? FailureStatus { get; }
        public string Message { get; }

        private StockExportValidationResult(bool isValid, StockExportStatus? status, string message)
        {
            IsValid = isValid;
            FailureStatus = status;
            Message = message;
        }

        public static StockExportValidationResult Ok()
            => new StockExportValidationResult(true, null, "");

        public static StockExportValidationResult Fail(StockExportStatus status, string message)
            => new StockExportValidationResult(false, status, message);
    }
}
