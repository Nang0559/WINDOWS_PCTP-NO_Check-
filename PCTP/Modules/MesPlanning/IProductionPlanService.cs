using System.Collections.Generic;
namespace PCTP.Modules.MesPlanning
{
    public interface IProductionPlanService
    {
        System.Data.DataTable GetPlanLines(string siteCode);
        /// <summary>Các Site tài khoản được upload kế hoạch (theo phạm vi dữ liệu MES.PLAN_UPLOAD).</summary>
        IReadOnlyList<string> GetUploadSites();
        /// <summary>Chuyền hợp lệ của Site để chọn trên màn hình upload: LineCode, LotLineCode, DepartmentCode, LineName.</summary>
        System.Data.DataTable GetProductionLines(string siteCode);
        void ImportRows(IList<ProductionPlanImportRow> rows, string sourceFileName, string createdBy);
    }
}