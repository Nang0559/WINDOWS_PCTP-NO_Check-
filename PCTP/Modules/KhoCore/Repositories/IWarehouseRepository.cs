using PCTP.Modules.KhoCore.Models;
using System.Collections.Generic;
using System.Data;


namespace PCTP.Modules.KhoVatLy.Repository
{
    public interface IWarehouseRepository
    {
        string GetProductNameByCode(string itemCode);
        InspectionConfig GetInspectionConfig(string itemCode);

        // ── Ghi ──────────────────────────────────────────────────────
        /// <summary>Insert Warehouse mới, trả về WarehouseId vừa tạo.</summary>
        int Insert(string warehouseName);
        // ── Đọc ──────────────────────────────────────────────────────
        List<Warehouse> GetAllWarehouses();
        bool Exists(string warehouseName);
        int GetIdByName(string warehouseName);
        List<string> GetRackNames(string warehouseName);
        DataTable GetActiveItemList();

    }
}
