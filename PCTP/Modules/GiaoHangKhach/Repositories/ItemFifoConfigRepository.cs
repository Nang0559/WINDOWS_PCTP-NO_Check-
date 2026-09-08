using PCTP.Modules.GiaoHangKhach.Intefaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class ItemFifoConfigRepository : IItemFifoConfigRepository
    {
        private readonly PhieuSqlExecutor _db;

        public ItemFifoConfigRepository(PhieuSqlExecutor db)
            => _db = db ?? throw new ArgumentNullException(nameof(db));

        public DataTable GetAll()
            => _db.LoadData(@"
                SELECT c.ItemCode, ISNULL(b.Name,'') AS TenHang, c.EnforceFifo
                FROM FVN_ItemFifoConfig c
                LEFT JOIN B20Item b ON b.Code = c.ItemCode
                ORDER BY c.ItemCode");

        public bool GetEnforceFifo(string itemCode)
        {
            object raw = _db.ExecuteScalar(
                "SELECT EnforceFifo FROM FVN_ItemFifoConfig WHERE ItemCode = @ma",
                new SqlParameter("@ma", itemCode));
            return raw != null && raw != DBNull.Value && Convert.ToBoolean(raw);
        }

        public void Upsert(string itemCode, bool enforceFifo)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            _db.ExecuteNonQuery(@"
                MERGE FVN_ItemFifoConfig AS t
                USING (SELECT @ma AS ItemCode) AS s ON t.ItemCode = s.ItemCode
                WHEN MATCHED THEN UPDATE SET EnforceFifo = @ef
                WHEN NOT MATCHED THEN INSERT (ItemCode, EnforceFifo) VALUES (@ma, @ef);",
                new SqlParameter("@ma", itemCode.Trim()),
                new SqlParameter("@ef", enforceFifo));
        }

        public void Delete(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            _db.ExecuteNonQuery(
                "DELETE FROM FVN_ItemFifoConfig WHERE ItemCode = @ma",
                new SqlParameter("@ma", itemCode.Trim()));
        }
    }
}
