using System;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Shared.Common;

namespace PCTP.Infrastructure.Stock
{
    /// <summary>
    /// IStockBalanceRepository trên MES ledger của FVN_PCTP.
    /// Chỉ đọc MES.FVN_InventoryBalance và ghi qua dbo.usp_FVN_PCTP_PostStockMovement;
    /// KHÔNG cập nhật FVN_STOCKTP.SLCONLAI / FVN_SlotLot.Quantity như nguồn tồn.
    /// Chạy trong UnitOfWork của caller để ledger + chứng từ nghiệp vụ cùng commit/rollback.
    /// </summary>
    public sealed class MesStockBalanceRepository : SqlRepositoryBase, IStockBalanceRepository
    {
        private const int MaxIdempotencyKeyLength = 180;
        private readonly ISiteContext _site;

        public MesStockBalanceRepository(PhieuSqlExecutor db, IUnitOfWork uow, ISiteContext site = null)
            : base(db, uow)
        {
            _site = site ?? AppSettingsSiteContext.Default;
        }

        public string SiteCode { get { return _site.SiteCode; } }

        public StockLotKey ResolveLot(string itemCode, string lotNo, bool createIfMissing)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) throw new ArgumentException("ItemCode (PartNo) là bắt buộc.", nameof(itemCode));
            if (string.IsNullOrWhiteSpace(lotNo)) throw new ArgumentException("LotNo là bắt buộc.", nameof(lotNo));
            if (createIfMissing && !HasTransaction)
                throw new InvalidOperationException("Tạo MES LOT phải chạy trong UnitOfWork transaction.");

            string site = SiteCode;
            string part = itemCode.Trim();
            string lot = lotNo.Trim();

            object id = ExecuteScalar(@"
DECLARE @id bigint;
SELECT @id = MaterialLotId
FROM MES.FVN_MaterialLot WITH (UPDLOCK, HOLDLOCK)
WHERE SiteCode = @site AND PartNo = @part AND LotNo = @lot;
IF @id IS NULL AND @create = 1
BEGIN
    INSERT INTO MES.FVN_MaterialLot (SiteCode, PartNo, LotNo, LotType, LotStatus, SourceSystem, SourceReference)
    VALUES (@site, @part, @lot, 'FG', 'AVAILABLE', 'PCTP', N'PCTP receiving');
    SET @id = SCOPE_IDENTITY();
END
SELECT @id;",
                new SqlParameter("@site", SqlDbType.NVarChar, 30) { Value = site },
                new SqlParameter("@part", SqlDbType.NVarChar, 100) { Value = part },
                new SqlParameter("@lot", SqlDbType.NVarChar, 120) { Value = lot },
                new SqlParameter("@create", SqlDbType.Bit) { Value = createIfMissing });

            if (id == null || id == DBNull.Value) return null;
            return new StockLotKey(site, part, lot, Convert.ToInt64(id));
        }

        public decimal GetAvailableQuantity(StockLotKey lot)
        {
            if (lot == null) throw new ArgumentNullException(nameof(lot));
            object value = ExecuteScalar(@"
SELECT ISNULL(SUM(OnHandQty - ReservedQty), 0)
FROM MES.FVN_InventoryBalance
WHERE SiteCode = @site AND PartNo = @part AND MaterialLotId = @lotId AND StockState = 'AVAILABLE';",
                new SqlParameter("@site", SqlDbType.NVarChar, 30) { Value = lot.SiteCode },
                new SqlParameter("@part", SqlDbType.NVarChar, 100) { Value = lot.PartNo },
                new SqlParameter("@lotId", SqlDbType.BigInt) { Value = lot.MaterialLotId });
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        public decimal GetAvailableQuantity(StockLotKey lot, int slotId)
        {
            if (lot == null) throw new ArgumentNullException(nameof(lot));
            object value = ExecuteScalar(@"
SELECT ISNULL(SUM(b.OnHandQty - b.ReservedQty), 0)
FROM MES.FVN_InventoryBalance AS b
JOIN dbo.FVN_SlotMesLocation AS m
  ON m.SiteCode = b.SiteCode AND m.LocationNo = b.LocationNo
 AND m.SlotId = @slotId AND m.IsPrimary = 1 AND m.IsActive = 1
WHERE b.SiteCode = @site AND b.PartNo = @part AND b.MaterialLotId = @lotId AND b.StockState = 'AVAILABLE';",
                new SqlParameter("@site", SqlDbType.NVarChar, 30) { Value = lot.SiteCode },
                new SqlParameter("@part", SqlDbType.NVarChar, 100) { Value = lot.PartNo },
                new SqlParameter("@lotId", SqlDbType.BigInt) { Value = lot.MaterialLotId },
                new SqlParameter("@slotId", SqlDbType.Int) { Value = slotId });
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        public long Post(StockLedgerEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.Lot == null) throw new ArgumentException("Lot (SiteCode, PartNo, MaterialLotId) là bắt buộc.", nameof(entry));
            if (entry.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(entry), "Quantity phải lớn hơn 0.");
            if (string.IsNullOrWhiteSpace(entry.MovementType)) throw new ArgumentException("MovementType là bắt buộc.", nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.ReferenceType)) throw new ArgumentException("ReferenceType là bắt buộc.", nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.IdempotencyKey)) throw new ArgumentException("IdempotencyKey là bắt buộc.", nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.ActorCode)) throw new ArgumentException("ActorCode là bắt buộc.", nameof(entry));
            if (!HasTransaction)
                throw new InvalidOperationException("Ghi MES ledger phải chạy trong UnitOfWork transaction cùng chứng từ nghiệp vụ.");
            if (!string.Equals(entry.Lot.SiteCode, SiteCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SiteCode của LOT [" + entry.Lot.SiteCode + "] khác site đang chạy [" + SiteCode + "].");

            var qty = new SqlParameter("@qty", SqlDbType.Decimal) { Precision = 19, Scale = 6, Value = entry.Quantity };
            object result = ExecuteScalar(@"
DECLARE @txn bigint;
EXEC dbo.usp_FVN_PCTP_PostStockMovement
    @SiteCode = @site, @PartNo = @part, @MaterialLotId = @lotId,
    @MovementType = @type, @Quantity = @qty, @ReferenceType = @refType,
    @IdempotencyKey = @idem, @ActorCode = @actor, @InventoryTransactionId = @txn OUTPUT,
    @FromSlotId = @fromSlot, @ToSlotId = @toSlot,
    @FromStockState = @fromState, @ToStockState = @toState,
    @ReferenceNo = @refNo, @ReferenceLineNo = @refLine, @TransactionAtUtc = @atUtc;
SELECT @txn;",
                new SqlParameter("@site", SqlDbType.NVarChar, 30) { Value = entry.Lot.SiteCode },
                new SqlParameter("@part", SqlDbType.NVarChar, 100) { Value = entry.Lot.PartNo },
                new SqlParameter("@lotId", SqlDbType.BigInt) { Value = entry.Lot.MaterialLotId },
                new SqlParameter("@type", SqlDbType.VarChar, 20) { Value = entry.MovementType },
                qty,
                new SqlParameter("@refType", SqlDbType.VarChar, 30) { Value = entry.ReferenceType },
                new SqlParameter("@idem", SqlDbType.NVarChar, MaxIdempotencyKeyLength) { Value = NormalizeKey(entry.IdempotencyKey) },
                new SqlParameter("@actor", SqlDbType.NVarChar, 100) { Value = entry.ActorCode.Trim() },
                new SqlParameter("@fromSlot", SqlDbType.Int) { Value = (object)entry.FromSlotId ?? DBNull.Value },
                new SqlParameter("@toSlot", SqlDbType.Int) { Value = (object)entry.ToSlotId ?? DBNull.Value },
                new SqlParameter("@fromState", SqlDbType.VarChar, 16) { Value = (object)entry.FromStockState ?? DBNull.Value },
                new SqlParameter("@toState", SqlDbType.VarChar, 16) { Value = (object)entry.ToStockState ?? DBNull.Value },
                new SqlParameter("@refNo", SqlDbType.NVarChar, 100) { Value = (object)entry.ReferenceNo ?? DBNull.Value },
                new SqlParameter("@refLine", SqlDbType.Int) { Value = (object)entry.ReferenceLineNo ?? DBNull.Value },
                new SqlParameter("@atUtc", SqlDbType.DateTime2) { Value = (object)entry.OccurredAtUtc ?? DBNull.Value });

            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("usp_FVN_PCTP_PostStockMovement không trả InventoryTransactionId.");
            return Convert.ToInt64(result);
        }

        /// <summary>Khoá quá 180 ký tự được rút gọn bằng SHA-256 để vẫn ổn định và duy nhất.</summary>
        internal static string NormalizeKey(string key)
        {
            string k = key.Trim();
            if (k.Length <= MaxIdempotencyKeyLength) return k;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(k));
                var sb = new StringBuilder(64);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return k.Substring(0, MaxIdempotencyKeyLength - 65) + "#" + sb;
            }
        }
    }
}
