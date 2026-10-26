using PCTP.Modules.KhoCore.Models;
using PCTP.Shared.Models;
using System.Collections.Generic;


namespace PCTP.Modules.KhoCore.Interfaces
{
    public interface IInspectionService
    {
        /// <summary>
        /// Chạy kiểm tra tem thùng theo config — trả InspectionResult.
        /// Caller (Form/Presenter) tự quyết định hiển thị UI.
        /// </summary>
        InspectionResult Inspect(
            QRCodeInfo temTong,
            InspectionConfig config,
            IReadOnlyList<string> rawBoxScans);  // danh sách QR thùng đã scan

        void SaveLog(
            string inspectionCode,
            QRCodeInfo temTong,
            IReadOnlyList<BoxScanResult> results,
            string finalResult);  // "PASS" | "FAIL"
    }

}
