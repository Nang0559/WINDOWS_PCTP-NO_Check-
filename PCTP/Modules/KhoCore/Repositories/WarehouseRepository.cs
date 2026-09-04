using PCTP.Models;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy.Repository
{
    public sealed class WarehouseRepository
        : SqlRepositoryBase, IWarehouseRepository
    {
        public WarehouseRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // PRODUCT
        // ============================================================

        public string GetProductNameByCode(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                return string.Empty;

            const string sql = @"
            SELECT TOP (1)
                Name
            FROM B7R2_FCC.dbo.vB20Item
            WHERE Code = @ItemCode;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100)
                {
                    Value = itemCode.Trim()
                });

            if (result == null || result == DBNull.Value)
                return "Không tìm thấy tên SP";

            return result.ToString();
        }

        // ============================================================
        // INSPECTION CONFIG
        // ============================================================

        public InspectionConfig GetInspectionConfig(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                return null;

            const string sql = @"
            SELECT
                ConfigId,
                ItemCode,
                DefaultQty,
                CheckItemCode,
                CheckLotNo,
                CheckNSX
            FROM InspectionConfig
            WHERE ItemCode = @ItemCode
              AND IsActive = 1;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100)
                {
                    Value = itemCode.Trim()
                });

            if (dt == null || dt.Rows.Count == 0)
                return null;

            DataRow row = dt.Rows[0];

            return new InspectionConfig
            {
                ConfigId = DbValueHelper.ToInt(row["ConfigId"]),

                ItemCode = row["ItemCode"] == DBNull.Value
                    ? string.Empty
                    : row["ItemCode"].ToString(),

                DefaultQty = DbValueHelper.ToInt(row["DefaultQty"]),

                CheckItemCode = row["CheckItemCode"] != DBNull.Value
                    && Convert.ToBoolean(row["CheckItemCode"]),

                CheckLotNo = row["CheckLotNo"] != DBNull.Value
                    && Convert.ToBoolean(row["CheckLotNo"]),

                CheckNSX = row["CheckNSX"] != DBNull.Value
                    && Convert.ToBoolean(row["CheckNSX"])
            };
        }

        // ============================================================
        // Đọc
        // ============================================================

        public bool Exists(string warehouseName)
        {
            if (string.IsNullOrWhiteSpace(warehouseName)) return false;

            const string sql = "SELECT COUNT(1) FROM Warehouse WHERE Name = @Name;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = warehouseName.Trim() });

            return DbValueHelper.ToInt(result) > 0;
        }

        public int GetIdByName(string warehouseName)
        {
            if (string.IsNullOrWhiteSpace(warehouseName)) return 0;

            const string sql = "SELECT TOP (1) WarehouseId FROM Warehouse WHERE Name = @Name;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = warehouseName.Trim() });

            return DbValueHelper.ToInt(result);
        }

        public List<string> GetRackNames(string warehouseName)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(warehouseName)) return result;

            const string sql = @"
        SELECT r.RackName
        FROM Rack r
        INNER JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
        WHERE w.Name = @Name
        ORDER BY r.RackName;";

            DataTable dt = LoadData(sql,
                new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = warehouseName.Trim() });

            if (dt == null) return result;

            foreach (DataRow row in dt.Rows)
                result.Add(row["RackName"]?.ToString() ?? "");

            return result;
        }


        // ============================================================
        // GET ALL WAREHOUSES
        // ============================================================

        public List<Warehouse> GetAllWarehouses()
        {
            const string sql = @"
            SELECT
                WarehouseId,
                Name
            FROM Warehouse
            ORDER BY WarehouseId;";

            DataTable dt = LoadData(sql);

            var warehouses = new List<Warehouse>();

            if (dt == null)
                return warehouses;

            foreach (DataRow row in dt.Rows)
            {
                warehouses.Add(new Warehouse
                {
                    // Nếu model có WarehouseId:
                    // WarehouseId = DbValueHelper.ToInt(row["WarehouseId"]),

                    Name = row["Name"] == DBNull.Value
                        ? string.Empty
                        : row["Name"].ToString(),

                    Racks = new List<Rack>()
                });
            }

            return warehouses;
        }
        public DataTable GetActiveItemList()
        {
            const string sql = @"
            SELECT Code, Name 
            FROM   B20Item
            WHERE  IsActive = 1 AND IsGroup = 0
            ORDER  BY Code";
            return LoadData(sql);
        }
        public int Insert(string warehouseName)
        {
            if (string.IsNullOrWhiteSpace(warehouseName))
                throw new ArgumentException("Tên Warehouse không được rỗng.", nameof(warehouseName));

            const string sql = @"
        INSERT INTO Warehouse (Name)
        OUTPUT INSERTED.WarehouseId
        VALUES (@Name);";

            object result = ExecuteScalar(sql,
                new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = warehouseName.Trim() });

            int newId = DbValueHelper.ToInt(result);
            if (newId <= 0)
                throw new InvalidOperationException("Không thể tạo Warehouse mới.");

            return newId;
        }
    }
}
