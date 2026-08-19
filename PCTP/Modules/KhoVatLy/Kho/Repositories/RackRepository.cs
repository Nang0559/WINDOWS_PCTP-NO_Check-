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

namespace PCTP.Modules.KhoVatLy.Repositories
{
    public sealed class RackRepository
      : SqlRepositoryBase, IRackRepository
    {
        public RackRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // GET RACK BY ID
        // ============================================================

        public Rack GetById(int rackId)
        {
            if (rackId <= 0)
                return null;

            const string sql = @"
            SELECT
                RackId,
                WarehouseId,
                RackName,
                RackRowCount,
                ColumnCount
            FROM Rack
            WHERE RackId = @RackId;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rackId
                });

            if (dt == null || dt.Rows.Count == 0)
                return null;

            return MapRack(dt.Rows[0]);
        }

        // ============================================================
        // GET RACKS BY WAREHOUSE
        // ============================================================

        public List<Rack> GetRacksByWarehouse(int warehouseId)
        {
            if (warehouseId <= 0)
                return new List<Rack>();

            const string sql = @"
            SELECT
                RackId,
                WarehouseId,
                RackName,
                RackRowCount,
                ColumnCount
            FROM Rack
            WHERE WarehouseId = @WarehouseId
            ORDER BY RackId;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                });

            var racks = new List<Rack>();

            if (dt == null)
                return racks;

            foreach (DataRow row in dt.Rows)
                racks.Add(MapRack(row));

            return racks;
        }

        // ============================================================
        // INSERT
        // ============================================================

        public int Insert(int warehouseId, Rack rack)
        {
            if (warehouseId <= 0)
                throw new ArgumentException(
                    "WarehouseId không hợp lệ.",
                    nameof(warehouseId));

            if (rack == null)
                throw new ArgumentNullException(nameof(rack));

            if (string.IsNullOrWhiteSpace(rack.Name))
                throw new ArgumentException(
                    "Tên Rack không được rỗng.",
                    nameof(rack));

            const string sql = @"
            INSERT INTO Rack
            (
                WarehouseId,
                RackName,
                RackRowCount,
                ColumnCount
            )
            OUTPUT INSERTED.RackId
            VALUES
            (
                @WarehouseId,
                @RackName,
                @RackRowCount,
                @ColumnCount
            );";

            object result = ExecuteScalar(
                sql,
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rack.Name.Trim()
                },
                new SqlParameter("@RackRowCount", SqlDbType.Int)
                {
                    Value = rack.RackRowCount
                },
                new SqlParameter("@ColumnCount", SqlDbType.Int)
                {
                    Value = rack.ColumnCount
                });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // UPDATE
        // ============================================================

        public void Update(Rack rack)
        {
            if (rack == null)
                throw new ArgumentNullException(nameof(rack));

            if (rack.RackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.",
                    nameof(rack));

            if (string.IsNullOrWhiteSpace(rack.Name))
                throw new ArgumentException(
                    "Tên Rack không được rỗng.",
                    nameof(rack));

            const string sql = @"
            UPDATE Rack
            SET
                WarehouseId = @WarehouseId,
                RackName = @RackName,
                RackRowCount = @RackRowCount,
                ColumnCount = @ColumnCount
            WHERE RackId = @RackId;";

            int affected = ExecuteNonQuery(
                sql,
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rack.RackId
                },
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = rack.WarehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rack.Name.Trim()
                },
                new SqlParameter("@RackRowCount", SqlDbType.Int)
                {
                    Value = rack.RackRowCount
                },
                new SqlParameter("@ColumnCount", SqlDbType.Int)
                {
                    Value = rack.ColumnCount
                });

            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy RackId [{rack.RackId}].");
        }

        // ============================================================
        // DELETE
        // ============================================================

        public void Delete(int rackId)
        {
            if (rackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.",
                    nameof(rackId));

            const string sql = @"
            DELETE FROM Rack
            WHERE RackId = @RackId;";

            ExecuteNonQuery(
                sql,
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rackId
                });
        }

        // ============================================================
        // CHECK EXIST
        // ============================================================

        public bool Exists(int warehouseId, string rackName)
        {
            if (warehouseId <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(rackName))
                return false;

            const string sql = @"
            SELECT COUNT(1)
            FROM Rack
            WHERE WarehouseId = @WarehouseId
              AND RackName = @RackName;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rackName.Trim()
                });

            return DbValueHelper.ToInt(result) > 0;
        }

        // ============================================================
        // GET RACK ID
        // ============================================================

        public int GetRackId(
            int warehouseId,
            string rackName)
        {
            if (warehouseId <= 0)
                return 0;

            if (string.IsNullOrWhiteSpace(rackName))
                return 0;

            const string sql = @"
            SELECT TOP (1)
                RackId
            FROM Rack
            WHERE WarehouseId = @WarehouseId
              AND RackName = @RackName
            ORDER BY RackId;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter("@WarehouseId", SqlDbType.Int)
                {
                    Value = warehouseId
                },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100)
                {
                    Value = rackName.Trim()
                });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // UPDATE LAYOUT
        // ============================================================

        public void UpdateLayout(
            int rackId,
            int rowCount,
            int columnCount)
        {
            if (rackId <= 0)
                throw new ArgumentException(
                    "RackId không hợp lệ.",
                    nameof(rackId));

            if (rowCount < 0)
                throw new ArgumentException(
                    "Số dòng không hợp lệ.",
                    nameof(rowCount));

            if (columnCount < 0)
                throw new ArgumentException(
                    "Số cột không hợp lệ.",
                    nameof(columnCount));

            const string sql = @"
            UPDATE Rack
            SET
                RackRowCount = @RackRowCount,
                ColumnCount = @ColumnCount
            WHERE RackId = @RackId;";

            ExecuteNonQuery(
                sql,
                new SqlParameter("@RackId", SqlDbType.Int)
                {
                    Value = rackId
                },
                new SqlParameter("@RackRowCount", SqlDbType.Int)
                {
                    Value = rowCount
                },
                new SqlParameter("@ColumnCount", SqlDbType.Int)
                {
                    Value = columnCount
                });
        }

        // ============================================================
        // MAPPING
        // ============================================================

        private static Rack MapRack(DataRow row)
        {
            return new Rack
            {
                RackId = DbValueHelper.ToInt(row["RackId"]),

                WarehouseId = DbValueHelper.ToInt(row["WarehouseId"]),

                Name = row["RackName"] == DBNull.Value
                    ? string.Empty
                    : row["RackName"].ToString(),

                RackRowCount = DbValueHelper.ToInt(row["RackRowCount"]),

                ColumnCount = DbValueHelper.ToInt(row["ColumnCount"]),

                Slots = new List<Slot>()
            };
        }
    }
}
