using System.Collections.Generic;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionPlanRepository
    {
        void ImportRows(IList<ProductionPlanImportRow> rows, string sourceFileName, string createdBy);
    }
}