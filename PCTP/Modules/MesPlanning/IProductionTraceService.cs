using System.Data;

namespace PCTP.Modules.MesPlanning
{
    public interface IProductionTraceService
    {
        DataTable Trace(string lotNo, string direction, string siteCode);
    }
}
