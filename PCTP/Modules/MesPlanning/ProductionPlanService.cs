using System;
using PCTP.Infrastructure.Security;
using System.Collections.Generic;
using System.Linq;
namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionPlanService : IProductionPlanService
    {
        private readonly IProductionPlanRepository _repository;
        public ProductionPlanService(IProductionPlanRepository repository)
        { _repository = repository ?? throw new ArgumentNullException(nameof(repository)); }
        public System.Data.DataTable GetPlanLines(string siteCode)
        {
            AuthorizationGuard.DemandScope("MES.PLAN_UPLOAD", "SITE", siteCode, siteCode);
            return _repository.GetPlanLines(siteCode);
        }

        public void ImportRows(IList<ProductionPlanImportRow> rows, string sourceFileName, string createdBy)
        {
            createdBy = AuthorizationGuard.ActorCode;
            AuthorizationGuard.Demand("MES.PLAN_UPLOAD", "CREATE");
            if (rows != null)
                foreach (var row in rows)
                    AuthorizationGuard.DemandActionAndScope("MES.PLAN_UPLOAD", "CREATE", "SITE", row.SiteCode, row.SiteCode);
            if (rows == null || rows.Count == 0) throw new ArgumentException("File không có dữ liệu kế hoạch.");
            var duplicate = rows.GroupBy(x => (x.PlanNo ?? "").Trim() + "|" + x.LineNo).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) throw new ArgumentException("File có PlanNo/LineNo trùng: " + duplicate.Key);
            foreach (var r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.SiteCode) || string.IsNullOrWhiteSpace(r.PlanNo)
                    || string.IsNullOrWhiteSpace(r.ShiftCode) || string.IsNullOrWhiteSpace(r.ProductionLineCode)
                    || string.IsNullOrWhiteSpace(r.ProductPartNo) || string.IsNullOrWhiteSpace(r.UomCode))
                    throw new ArgumentException("Thiếu Site, PlanNo, Shift, ProductionLine, ProductPartNo hoặc UOM.");
                if (r.LineNo <= 0 || r.PlannedQty <= 0) throw new ArgumentException("LineNo và PlannedQty phải lớn hơn 0.");
                if (r.PlannedEnd.HasValue && r.PlannedStart.HasValue && r.PlannedEnd < r.PlannedStart)
                    throw new ArgumentException("Giờ kết thúc phải lớn hơn hoặc bằng giờ bắt đầu.");
            }
            _repository.ImportRows(rows, sourceFileName, createdBy);
        }
    }
}