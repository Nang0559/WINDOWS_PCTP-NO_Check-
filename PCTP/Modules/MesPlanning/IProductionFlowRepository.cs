using System;
using System.Data;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionFlowRepository
    {
        DataTable GetDashboard(string siteCode, DateTime? fromDate, DateTime? toDate, string departmentScopeXml);
        DataTable GetExtra(string siteCode, DateTime? fromDate, DateTime? toDate);
        long GetChangeVersion();
    }
}