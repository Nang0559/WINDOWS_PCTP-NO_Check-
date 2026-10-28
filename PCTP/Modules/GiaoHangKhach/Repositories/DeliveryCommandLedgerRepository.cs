using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Shared.Common;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// SQL implementation of the delivery BusinessKey/QR ledger. Tables are created by
    /// SQL/PCTP_DeliveryCommandLedger.sql. Must run inside the handler's open transaction.
    /// </summary>
    public sealed class DeliveryCommandLedgerRepository : SqlRepositoryBase, IDeliveryCommandLedger
    {
        public DeliveryCommandLedgerRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public int? FindCommittedQuantity(string businessKey)
        {
            // UPDLOCK+HOLDLOCK takes a key-range lock even when the row is absent,
            // so two sessions with the same BusinessKey serialize here.
            object v = ExecuteScalar(
                "SELECT TotalQuantity FROM dbo.FVN_DeliveryCommandLedger WITH (UPDLOCK, HOLDLOCK) WHERE BusinessKey = @k",
                new SqlParameter("@k", SqlDbType.NVarChar, 200) { Value = businessKey });
            return v == null || v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
        }

        public int Insert(DeliveryStockCommand c)
        {
            object id = ExecuteScalar(
                @"INSERT INTO dbo.FVN_DeliveryCommandLedger
                    (BusinessKey, CustomerCode, DeliveryReference, PerformedBy, OccurredAt, TotalQuantity, PendingSttCsv)
                  OUTPUT INSERTED.Id
                  VALUES (@k, @c, @r, @p, @t, @q, @s)",
                new SqlParameter("@k", SqlDbType.NVarChar, 200) { Value = c.BusinessKey },
                new SqlParameter("@c", SqlDbType.NVarChar, 50) { Value = c.CustomerCode },
                new SqlParameter("@r", SqlDbType.NVarChar, 100) { Value = c.DeliveryReference },
                new SqlParameter("@p", SqlDbType.NVarChar, 100) { Value = c.PerformedBy },
                new SqlParameter("@t", SqlDbType.DateTime2) { Value = c.OccurredAt },
                new SqlParameter("@q", SqlDbType.Int) { Value = c.TotalQuantity },
                new SqlParameter("@s", SqlDbType.NVarChar, 1000) { Value = string.Join(",", c.PendingStt.Select(x => x.ToString())) });
            return Convert.ToInt32(id);
        }

        public bool TryClaimQr(int ledgerId, string partCode, string qrKey)
        {
            // Atomic claim: insert only when absent; 0 rows affected = already delivered (P8).
            int n = ExecuteNonQuery(
                @"INSERT INTO dbo.FVN_DeliveryCommandQr (LedgerId, PartCode, QrKey)
                  SELECT @l, @p, @q
                  WHERE NOT EXISTS (SELECT 1 FROM dbo.FVN_DeliveryCommandQr WITH (UPDLOCK, HOLDLOCK) WHERE QrKey = @q)",
                new SqlParameter("@l", SqlDbType.Int) { Value = ledgerId },
                new SqlParameter("@p", SqlDbType.NVarChar, 100) { Value = partCode },
                new SqlParameter("@q", SqlDbType.NVarChar, 450) { Value = qrKey });
            return n == 1;
        }
    }
}
