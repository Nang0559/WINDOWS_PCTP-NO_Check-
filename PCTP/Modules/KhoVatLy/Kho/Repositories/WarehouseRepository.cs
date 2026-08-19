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
        // SAVE WAREHOUSE
        // ============================================================

        public void SaveWarehouse(Warehouse warehouse)
        {
            if (warehouse == null)
                throw new ArgumentNullException(nameof(warehouse));

            if (string.IsNullOrWhiteSpace(warehouse.Name))
                throw new ArgumentException(
                    "Tên Warehouse không được rỗng.",
                    nameof(warehouse));

            const string sql = @"
            INSERT INTO Warehouse
            (
                Name
            )
            VALUES
            (
                @Name
            );";

            int affected = ExecuteNonQuery(
                sql,
                new SqlParameter("@Name", SqlDbType.NVarChar, 100)
                {
                    Value = warehouse.Name.Trim()
                });

            if (affected == 0)
                throw new InvalidOperationException(
                    "Không thể lưu Warehouse.");
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
    }
}
