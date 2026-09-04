using PCTP.Modules.KhoCore.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoCore.Repositories
{
    // ── Interface Repository ─────────────────────────────────────────────
    public interface IInspectionLogRepository
    {
        void SaveLog(InspectionLogEntry entry);
        List<InspectionLogEntry> GetByInspectionCode(string inspectionCode);
        // ── Dùng riêng cho FormInspectionHistory ────────────────────────────
        DataTable GetHistoryMaster(DateTime from, DateTime to, string itemCode, string result);
        DataTable GetHistoryDetail(string inspectionCode);
    }
}
