using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security;
using System.Xml;
using PCTP.Infrastructure.Security;
using PCTP.Security;
namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// Read-only Control Tower boundary. It deliberately exposes no transition or posting methods.
    /// SQL receives only the effective department allow-list for this signed-in session.
    /// </summary>
    public sealed class ProductionFlowService : IProductionFlowService
    {
        private readonly IProductionFlowRepository _repository;
        public ProductionFlowService(IProductionFlowRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public DataTable GetDashboard(string siteCode, DateTime? fromDate, DateTime? toDate)
        {
            siteCode = (siteCode ?? string.Empty).Trim();
            if (siteCode.Length == 0) throw new ArgumentException("SiteCode là bắt buộc.");
            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
                throw new ArgumentException("Từ ngày không được lớn hơn đến ngày.");

            var session = AuthorizationGuard.DemandScope(
                "MES.PRODUCTION_FLOW", "SITE", siteCode, siteCode);
            string scopeXml = BuildDepartmentScope(session, siteCode);
            return _repository.GetDashboard(siteCode, fromDate, toDate, scopeXml);
        }

        private static string BuildDepartmentScope(UserSession session, string siteCode)
        {
            var candidates = session.DataScopes ?? new List<UserDataScope>();
            bool hasUserPolicy = candidates.Any(s =>
                string.Equals(s.ScopeSource, "USER", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(s.FunctionCode) ||
                 string.Equals(s.FunctionCode, "MES.PRODUCTION_FLOW", StringComparison.OrdinalIgnoreCase)));

            var applicable = candidates.Where(s =>
                (string.IsNullOrWhiteSpace(s.FunctionCode) ||
                 string.Equals(s.FunctionCode, "MES.PRODUCTION_FLOW", StringComparison.OrdinalIgnoreCase)) &&
                (!hasUserPolicy || string.Equals(s.ScopeSource, "USER", StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(s.SiteCode) ||
                 string.Equals(s.SiteCode, siteCode, StringComparison.OrdinalIgnoreCase))).ToList();

            bool siteWide = applicable.Any(s => s.IsAllowed &&
                (string.Equals(s.ScopeType, "ALL", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(s.ScopeType, "SITE", StringComparison.OrdinalIgnoreCase)) &&
                session.HasDataScope("MES.PRODUCTION_FLOW", "SITE", siteCode, siteCode));

            var departments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!siteWide)
            {
                foreach (var item in applicable.Where(s =>
                    string.Equals(s.ScopeType, "DEPARTMENT", StringComparison.OrdinalIgnoreCase) &&
                    s.IsAllowed && !string.IsNullOrWhiteSpace(s.ScopeValue) &&
                    !string.Equals(s.ScopeValue, "*", StringComparison.Ordinal)))
                {
                    if (session.HasDataScope("MES.PRODUCTION_FLOW", "DEPARTMENT",
                        item.ScopeValue.Trim(), siteCode))
                        departments.Add(item.ScopeValue.Trim());
                }
            }

            if (!siteWide && departments.Count == 0)
                throw new UnauthorizedAccessException("Bạn chưa được cấp phạm vi phòng ban để theo dõi dòng chảy MES.");

            var document = new XmlDocument();
            var root = document.CreateElement("Scopes");
            document.AppendChild(root);
            if (siteWide)
            {
                var all = document.CreateElement("Department");
                all.InnerText = "*";
                root.AppendChild(all);
            }
            else
            {
                foreach (var code in departments.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var node = document.CreateElement("Department");
                    node.InnerText = code;
                    root.AppendChild(node);
                }
            }
            return document.OuterXml;
        }
    }
}