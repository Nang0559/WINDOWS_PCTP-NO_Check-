using System.Collections.Generic;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionPlanRepository
    {
        System.Data.DataTable GetPlanLines(string siteCode);
        /// <summary>Các Site đang hoạt động (dùng khi tài khoản có phạm vi ALL).</summary>
        IList<string> GetActiveSites();
        /// <summary>Chuyền hợp lệ của Site (MES.FVN_ProductionLotCodeMap đang hoạt động): LineCode, LotLineCode, DepartmentCode, LineName.</summary>
        System.Data.DataTable GetProductionLines(string siteCode);
        void ImportRows(IList<ProductionPlanImportRow> rows, string sourceFileName, string createdBy);
    }
}