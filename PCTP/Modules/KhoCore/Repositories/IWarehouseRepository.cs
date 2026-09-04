using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
