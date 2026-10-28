using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using System;
using System.Data.SqlClient;

namespace PCTP.Modules.KhoVatLy.Repositories
{
    /// <summary>
    /// Writes quarantine-release audit rows in the caller-owned UnitOfWork.
    /// The table is created by SQL/PCTPQuarantineRelease.sql and is deliberately
    /// separate from legacy stock-history tables so release authorization is traceable.
    /// </summary>
    public sealed class QuarantineReleaseAuditRepository : SqlRepositoryBase, IQuarantineReleaseAuditRepository
    {
        public QuarantineReleaseAuditRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow) { }

        public void RecordRelease(int slotLotId, int slotId, string lotNo, string itemCode, int quantity,
            string performedBy, string referenceId, string reason)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("Quarantine release audit must run inside the stock UnitOfWork transaction.");
            if (slotLotId <= 0 || slotId <= 0 || quantity <= 0)
                throw new ArgumentException("Invalid quarantine release identity/quantity.");
            if (string.IsNullOrWhiteSpace(performedBy))
                throw new ArgumentException("PerformedBy is required.", nameof(performedBy));

            ExecuteNonQuery(@"
INSERT INTO dbo.FVN_PCTPQuarantineReleaseAudit
    (SlotLotId, SlotId, LotNo, ItemCode, Quantity, PerformedBy, ReferenceId, Reason, ReleasedAtUtc)
VALUES
    (@slotLotId, @slotId, @lotNo, @itemCode, @quantity, @performedBy, @referenceId, @reason, SYSUTCDATETIME());",
                new SqlParameter("@slotLotId", slotLotId),
                new SqlParameter("@slotId", slotId),
                new SqlParameter("@lotNo", (object)(lotNo ?? string.Empty)),
                new SqlParameter("@itemCode", (object)(itemCode ?? string.Empty)),
                new SqlParameter("@quantity", quantity),
                new SqlParameter("@performedBy", performedBy.Trim()),
                new SqlParameter("@referenceId", (object)(referenceId ?? string.Empty)),
                new SqlParameter("@reason", (object)(reason ?? string.Empty)));
        }
    }
}
