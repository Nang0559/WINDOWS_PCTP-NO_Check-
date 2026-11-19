using System;
using PCTP.Infrastructure.Security;
using System.Data;

namespace PCTP.Modules.MesPlanning
{
    public sealed class ProductionTraceService : IProductionTraceService
    {
        private readonly IProductionTraceRepository _repository;

        public ProductionTraceService(IProductionTraceRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public DataTable Trace(string lotNo, string direction, string siteCode)
        {
            AuthorizationGuard.DemandActionAndScope("REPORT.TRACEABILITY", "VIEW", "SITE", siteCode, siteCode);
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("Vui lòng nhập LOT cần truy xuất.", nameof(lotNo));
            if (lotNo.Trim().Length > 120)
                throw new ArgumentException("LOT không được vượt quá 120 ký tự.", nameof(lotNo));

            var normalizedDirection = (direction ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedDirection != "UPSTREAM" && normalizedDirection != "DOWNSTREAM")
                throw new ArgumentException("Chiều truy xuất không hợp lệ.", nameof(direction));

            return _repository.GetTrace(lotNo.Trim(), normalizedDirection, string.IsNullOrWhiteSpace(siteCode) ? null : siteCode.Trim());
        }
    }
}
