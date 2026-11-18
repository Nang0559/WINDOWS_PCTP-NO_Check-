using System.Collections.Generic;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionPlanService
    {
        void ImportRows(IList<ProductionPlanImportRow> rows, string sourceFileName, string createdBy);
    }
}