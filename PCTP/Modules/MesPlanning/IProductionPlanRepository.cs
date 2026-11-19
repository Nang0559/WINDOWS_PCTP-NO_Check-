using System.Collections.Generic;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionPlanRepository
    {
        System.Data.DataTable GetPlanLines(string siteCode);
        void ImportRows(IList<ProductionPlanImportRow> rows, string sourceFileName, string createdBy);
    }
}