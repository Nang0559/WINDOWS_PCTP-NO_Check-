using System.Data;

namespace PCTP.Modules.MesPlanning
{
    public interface IProductionTraceRepository
    {
        DataTable GetTrace(string lotNo, string direction, string siteCode);
    }
}
