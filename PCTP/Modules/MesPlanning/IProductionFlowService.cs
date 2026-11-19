using System;
using System.Data;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionFlowService
    {
        DataTable GetDashboard(string siteCode, DateTime? fromDate, DateTime? toDate);
    }
}