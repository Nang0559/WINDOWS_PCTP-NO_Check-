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

namespace PCTP.Modules.KhoCore.Repositories
{
    public sealed class InspectionConfigRepository
    : SqlRepositoryBase, IInspectionConfigRepository
    {
        public InspectionConfigRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow) { }

        public List<InspectionConfig> GetAll()
        {
            const string sql = @"
            SELECT c.ConfigId, c.ItemCode,
                   c.DefaultQty, c.CheckItemCode, c.CheckLotNo,
                   c.CheckNSX, c.IsActive, c.Note,
                   ISNULL(i.Name, '') AS ItemName
            FROM InspectionConfig c
            LEFT JOIN vB20Item i ON i.Code = c.ItemCode
            ORDER BY c.ItemCode;";

            DataTable dt = LoadData(sql);
            return MapList(dt);
        }

        public InspectionConfig GetByItemCode(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return null;

            const string sql = @"
            SELECT c.ConfigId, c.ItemCode,
                   c.DefaultQty, c.CheckItemCode, c.CheckLotNo,
                   c.CheckNSX, c.IsActive, c.Note,
                   ISNULL(i.Name, '') AS ItemName
            FROM InspectionConfig c
            LEFT JOIN vB20Item i ON i.Code = c.ItemCode
            WHERE c.ItemCode = @ItemCode AND c.IsActive = 1;";

            DataTable dt = LoadData(sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100)
                { Value = itemCode.Trim() });

            if (dt == null || dt.Rows.Count == 0) return null;
            return MapOne(dt.Rows[0]);
        }

        public int Insert(InspectionConfig cfg)
        {
            const string sql = @"
            INSERT INTO InspectionConfig
                (ItemCode, DefaultQty, CheckItemCode, CheckLotNo,
                 CheckNSX, IsActive, Note)
            OUTPUT INSERTED.ConfigId
            VALUES
                (@ItemCode, @DefaultQty, @CheckItemCode, @CheckLotNo,
                 @CheckNSX, @IsActive, @Note);";

            object result = ExecuteScalar(sql, BuildParams(cfg));
            return DbValueHelper.ToInt(result);
        }

        public void Update(InspectionConfig cfg)
        {
            const string sql = @"
            UPDATE InspectionConfig SET
                DefaultQty    = @DefaultQty,
                CheckItemCode = @CheckItemCode,
                CheckLotNo    = @CheckLotNo,
                CheckNSX      = @CheckNSX,
                IsActive      = @IsActive,
                Note          = @Note
            WHERE ItemCode = @ItemCode;";

            int affected = ExecuteNonQuery(sql, BuildParams(cfg));
            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy InspectionConfig cho [{cfg.ItemCode}].");
        }

        public void Delete(int configId)
        {
            if (configId <= 0)
                throw new ArgumentException("ConfigId không hợp lệ.");

            ExecuteNonQuery(
                "DELETE FROM InspectionConfig WHERE ConfigId = @Id;",
                new SqlParameter("@Id", SqlDbType.Int) { Value = configId });
        }

        // ── Helpers ────────────────────────────────────────────────────
        private static SqlParameter[] BuildParams(InspectionConfig cfg) => new[]
        {
        new SqlParameter("@ItemCode",      SqlDbType.NVarChar, 100)
            { Value = cfg.ItemCode.Trim() },
        new SqlParameter("@DefaultQty",    SqlDbType.Int)
            { Value = cfg.DefaultQty > 0 ? cfg.DefaultQty : 1 },
        new SqlParameter("@CheckItemCode", SqlDbType.Bit)
            { Value = cfg.CheckItemCode },
        new SqlParameter("@CheckLotNo",    SqlDbType.Bit)
            { Value = cfg.CheckLotNo },
        new SqlParameter("@CheckNSX",      SqlDbType.Bit)
            { Value = cfg.CheckNSX },
        new SqlParameter("@IsActive",      SqlDbType.Bit)
            { Value = cfg.IsActive },
        new SqlParameter("@Note",          SqlDbType.NVarChar, 500)
            { Value = string.IsNullOrWhiteSpace(cfg.Note)
                ? (object)DBNull.Value : cfg.Note.Trim() }
    };

        private static List<InspectionConfig> MapList(DataTable dt)
        {
            var list = new List<InspectionConfig>();
            if (dt == null) return list;
            foreach (DataRow row in dt.Rows)
                list.Add(MapOne(row));
            return list;
        }

        private static InspectionConfig MapOne(DataRow row) => new InspectionConfig
        {
            ConfigId = DbValueHelper.ToInt(row["ConfigId"]),
            ItemCode = row["ItemCode"]?.ToString() ?? "",
            ItemName = row["ItemName"]?.ToString() ?? "",
            DefaultQty = DbValueHelper.ToInt(row["DefaultQty"]),
            CheckItemCode = row["CheckItemCode"] != DBNull.Value
                            && Convert.ToBoolean(row["CheckItemCode"]),
            CheckLotNo = row["CheckLotNo"] != DBNull.Value
                            && Convert.ToBoolean(row["CheckLotNo"]),
            CheckNSX = row["CheckNSX"] != DBNull.Value
                            && Convert.ToBoolean(row["CheckNSX"]),
            IsActive = row["IsActive"] != DBNull.Value
                            && Convert.ToBoolean(row["IsActive"]),
            Note = row["Note"] == DBNull.Value
                            ? "" : row["Note"].ToString()
        };
    }
}
