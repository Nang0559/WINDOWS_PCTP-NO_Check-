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

        public IReadOnlyList<string> GetUploadSites()
        {
            var session = AuthorizationGuard.Demand("MES.PLAN_UPLOAD", "CREATE");
            return PCTP.Framework.Documents.DocumentSiteResolver.Candidates(session, "MES.PLAN_UPLOAD", _repository.GetActiveSites);
        }

        public System.Data.DataTable GetProductionLines(string siteCode)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("Chưa chọn Site.", "siteCode");
            AuthorizationGuard.DemandScope("MES.PLAN_UPLOAD", "SITE", siteCode.Trim(), siteCode.Trim());
            return _repository.GetProductionLines(siteCode.Trim());
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
            // Validate the composite IFS key (SiteCode + ProductionLineCode). Migration 075 also adds SQL-side mapping validation.
            foreach (var bySite in rows.GroupBy(r => r.SiteCode.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (System.Data.DataRow lr in _repository.GetProductionLines(bySite.Key).Rows)
                    valid.Add(Convert.ToString(lr["LineCode"]).Trim());
                var unknown = bySite.Select(r => r.ProductionLineCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(c => !valid.Contains(c));
                if (unknown != null)
                    throw new ArgumentException("Chuyền IFS \"" + unknown + "\" chưa có mapping LOT đang hoạt động cho Site " + bySite.Key + " (MES.FVN_ProductionLotCodeMap: SiteCode + PlanProductionLineCode).");
            }
            _repository.ImportRows(rows, sourceFileName, createdBy);
        }
    }
}