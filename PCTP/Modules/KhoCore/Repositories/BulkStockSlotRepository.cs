using PCTP.Infrastructure.Stock;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Common;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

// This legacy application project targets C# 7.3; its null-returning repository contracts
// are intentionally consumed by the nullable-enabled integration harness.
#pragma warning disable CS8625, CS8600, CS8601, CS8603

namespace PCTP.Modules.KhoVatLy.Repositories
{
    public sealed class BulkStockSlotRepository : SqlRepositoryBase, IBulkStockSlotRepository, IStockSlotRepository
    {
        private readonly string _movementGuardTable = string.Empty;
        private readonly bool _useQcTempTables;
        private readonly string _qcTablePrefix = string.Empty;
        private readonly ISiteContext _site;

        private SqlParameter SiteParam()
        {
            return new SqlParameter("@site", SqlDbType.NVarChar, 30) { Value = _site.SiteCode };
        }

        public BulkStockSlotRepository(PhieuSqlExecutor db, IUnitOfWork uow, string movementGuardTable = "dbo.FVN_StockMovementGuard", bool useQcTempTables = false, string qcTablePrefix = null, ISiteContext site = null)
            : base(db, uow)
        {
            _site = site ?? AppSettingsSiteContext.Default;
            _useQcTempTables = useQcTempTables;
            if (!string.IsNullOrWhiteSpace(qcTablePrefix) &&
                !System.Text.RegularExpressions.Regex.IsMatch(qcTablePrefix, @"^##PCTP_QC_[A-Fa-f0-9]{32}_$"))
                throw new ArgumentException("QC table prefix must be a unique ##PCTP_QC_<32-hex>_ prefix.", nameof(qcTablePrefix));
            if (!string.IsNullOrWhiteSpace(qcTablePrefix) && !useQcTempTables)
                throw new ArgumentException("QC table prefix is valid only when useQcTempTables is enabled.", nameof(qcTablePrefix));
            _qcTablePrefix = qcTablePrefix ?? string.Empty;
            if (string.Equals(movementGuardTable, "dbo.FVN_StockMovementGuard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(movementGuardTable, "#PCTPStockMovementGuard", StringComparison.OrdinalIgnoreCase))
                _movementGuardTable = movementGuardTable;
            else if (System.Text.RegularExpressions.Regex.IsMatch(
                movementGuardTable ?? string.Empty,
                @"^##PCTPStockMovementGuard_[A-Fa-f0-9]{32}$"))
                _movementGuardTable = movementGuardTable;
            else
                throw new ArgumentException("Movement guard table must be dbo.FVN_StockMovementGuard or a QC temporary table.", nameof(movementGuardTable));
        }

        // SQL integration runs the real repository against session-local fixtures. This flag is
        // opt-in and leaves every production query/table name unchanged by default.
        private string RewriteTableNames(string sql)
        {
            if (!_useQcTempTables || string.IsNullOrEmpty(sql)) return sql;
            return System.Text.RegularExpressions.Regex.Replace(
                sql,
                @"(?<![@#\w])dbo\.FVN_(Warehouse|Rack|SlotLot|Slot)\b",
                match => (string.IsNullOrEmpty(_qcTablePrefix) ? "#" : _qcTablePrefix) + match.Groups[1].Value,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private new DataTable LoadData(string sql, params SqlParameter[] parameters)
            => base.LoadData(RewriteTableNames(sql), parameters);

        private new object ExecuteScalar(string sql, params SqlParameter[] parameters)
            => base.ExecuteScalar(RewriteTableNames(sql), parameters);

        private new int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
            => base.ExecuteNonQuery(RewriteTableNames(sql), parameters);

        public int GetOrCreateVirtualSlotId(string warehouseName, string rackName, int capacity)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("GetOrCreateVirtualSlotId phải chạy trong transaction (Uow.Begin() trước).");

            string resource = $"BULK_SLOT_{warehouseName}_{rackName}";
            object lockResult = ExecuteScalar(
                @"DECLARE @res INT;
              EXEC @res = sp_getapplock
                    @Resource = @Resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
              SELECT @res;",
                new SqlParameter("@Resource", resource));

            int lockCode = lockResult == null || lockResult == DBNull.Value ? -999 : Convert.ToInt32(lockResult);
            if (lockCode < 0)
                throw new InvalidOperationException($"Không lấy được khoá tạo Slot ảo A0 (mã lỗi {lockCode}). Thử lại sau.");

            DataTable existing = LoadData(
                @"SELECT TOP 1 s.SlotId, s.Capacity
              FROM dbo.FVN_Slot s
              JOIN dbo.FVN_Rack r ON r.SiteCode = s.SiteCode AND r.RackId = s.RackId
              JOIN dbo.FVN_Warehouse w ON w.SiteCode = r.SiteCode AND w.WarehouseId = r.WarehouseId
              WHERE s.SiteCode = @site AND w.Name = @wh AND r.RackName = @rack",
                SiteParam(),
                new SqlParameter("@wh", warehouseName),
                new SqlParameter("@rack", rackName));

            if (existing.Rows.Count > 0)
                return Convert.ToInt32(existing.Rows[0]["SlotId"]);

            int whId = Convert.ToInt32(ExecuteScalar(
                "INSERT INTO dbo.FVN_Warehouse (SiteCode, Name) OUTPUT INSERTED.WarehouseId VALUES (@site, @n)",
                SiteParam(), new SqlParameter("@n", warehouseName)));
            int rackId = Convert.ToInt32(ExecuteScalar(
                "INSERT INTO dbo.FVN_Rack (SiteCode, WarehouseId, RackName) OUTPUT INSERTED.RackId VALUES (@site, @w, @r)",
                SiteParam(), new SqlParameter("@w", whId), new SqlParameter("@r", rackName)));
            int newSlotId = Convert.ToInt32(ExecuteScalar(
                @"INSERT INTO dbo.FVN_Slot (SiteCode, RackId, SlotNumber, IsOccupied, Capacity, Quantity)
              OUTPUT INSERTED.SlotId
              VALUES (@site, @rk, 1, 0, @cap, 0)",
                SiteParam(), new SqlParameter("@rk", rackId), new SqlParameter("@cap", capacity)));

            // Slot ảo chỉ ghi được tồn khi đã có ánh xạ MES location chính (FVN_SlotMesLocation).
            // Mã không thể tự tạo location IFS/MES, nên báo rõ để quản trị viên cấp mapping.
            return newSlotId;
        }

        public void LockSlotForUpdate(int slotId)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("LockSlotForUpdate phải chạy trong transaction (Uow.Begin() trước).");
            ExecuteScalar("SELECT SlotId FROM dbo.FVN_Slot WITH (UPDLOCK, ROWLOCK) WHERE SiteCode = @site AND SlotId = @SlotId", SiteParam(), new SqlParameter("@SlotId", slotId));
        }

        public List<LotInfo> GetLots(int slotId)
        {
            DataTable dt = LoadData(
                @"SELECT ItemCode, LotNo, Quantity, TemCode, QrData, MaPhieu, ImportDate
              FROM dbo.FVN_SlotLot WHERE SiteCode = @site AND SlotId = @SlotId AND PhieuStatus = 0 ORDER BY LotNo",
                SiteParam(), new SqlParameter("@SlotId", slotId));

            var lots = new List<LotInfo>();
            foreach (DataRow row in dt.Rows)
            {
                string lotNo = row["LotNo"] == DBNull.Value ? string.Empty : row["LotNo"].ToString() ?? string.Empty;
                int quantity = Convert.ToInt32(row["Quantity"]);
                string temCode = row["TemCode"] == DBNull.Value ? string.Empty : row["TemCode"].ToString() ?? string.Empty;
                string qrData = row["QrData"] == DBNull.Value ? string.Empty : row["QrData"].ToString() ?? string.Empty;
                QRCodeInfo qrInfo = null;
                if (!string.IsNullOrWhiteSpace(qrData))
                {
                    try { qrInfo = QRCodeParser.ParseQRCode(qrData); }
                    catch (FormatException) { qrInfo = null; }
                }
                if (qrInfo == null)
                {
                    qrInfo = new QRCodeInfo
                    {
                        LotNo = lotNo,
                        ItemCode = row["ItemCode"] == DBNull.Value ? string.Empty : row["ItemCode"].ToString() ?? string.Empty,
                        Quantity = quantity,
                        MaPhieu = row["MaPhieu"] == DBNull.Value ? "" : row["MaPhieu"].ToString(),
                        ImportDate = row["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ImportDate"]),
                        RawQr = qrData
                    };
                }
                else qrInfo.Quantity = quantity;

                lots.Add(new LotInfo
                {
                    LotNo = lotNo,
                    Quantity = quantity,
                    TemCode = temCode,
                    RawQr = qrData,
                    QRInfo = qrInfo,
                    ItemCode = row["ItemCode"] == DBNull.Value ? "" : row["ItemCode"].ToString()
                });
            }
            return lots;
        }

        public List<BulkSlotLotLocation> FindSlotLotsByLot(string lotNo, int excludeSlotId)
        {
            if (string.IsNullOrWhiteSpace(lotNo)) return new List<BulkSlotLotLocation>();
            if (!HasTransaction)
                throw new InvalidOperationException("FindSlotLotsByLot phải chạy trong transaction.");

            DataTable dt = LoadData(
                @"SELECT SlotId, ItemCode, SUM(Quantity) AS Qty, MIN(ImportDate) AS FirstImport
                  FROM dbo.FVN_SlotLot WITH (UPDLOCK, ROWLOCK)
                  WHERE SiteCode = @site AND PhieuStatus = 0 AND Quantity > 0 AND SlotId <> @ex
                    AND " + LotCodeHelper.BuildLotMatchSql("LotNo", "@lot") + @"
                  GROUP BY SlotId, ItemCode
                  ORDER BY MIN(ImportDate) ASC, SlotId ASC",
                SiteParam(),
                new SqlParameter("@ex", excludeSlotId),
                new SqlParameter("@lot", lotNo.Trim()));

            var list = new List<BulkSlotLotLocation>();
            foreach (DataRow r in dt.Rows)
            {
                string item = r["ItemCode"] == DBNull.Value ? string.Empty : r["ItemCode"].ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(item)) continue;
                list.Add(new BulkSlotLotLocation
                {
                    SlotId = Convert.ToInt32(r["SlotId"]),
                    ItemCode = item.Trim(),
                    Quantity = Convert.ToInt32(r["Qty"])
                });
            }
            return list;
        }

        // ================================================================
        // IStockSlotRepository — central KhoCore mutation boundary
        // ================================================================

        public int GetLotQuantity(int slotLotId)
            => Convert.ToInt32(ExecuteScalar("SELECT ISNULL(Quantity,0) FROM dbo.FVN_SlotLot WHERE SiteCode=@site AND SlotLotId=@id", SiteParam(), new SqlParameter("@id", slotLotId)) ?? 0);

        public byte GetPhieuStatus(int slotLotId)
        {
            object value = ExecuteScalar("SELECT PhieuStatus FROM dbo.FVN_SlotLot WHERE SiteCode=@site AND SlotLotId=@id", SiteParam(), new SqlParameter("@id", slotLotId));
            return value == null || value == DBNull.Value ? (byte)255 : Convert.ToByte(value);
        }

        public StockSlotLot GetLotDetails(int slotLotId)
        {
            DataTable dt = LoadData(
                @"SELECT LotNo, ItemCode, Quantity, TemCode, QrData, ImportDate, PhieuStatus FROM dbo.FVN_SlotLot WHERE SiteCode=@site AND SlotLotId=@id",
                SiteParam(), new SqlParameter("@id", slotLotId));
            if (dt.Rows.Count == 0) return null;
            DataRow r = dt.Rows[0];
            return new StockSlotLot
            {
                LotNo = Convert.ToString(r["LotNo"]),
                ItemCode = Convert.ToString(r["ItemCode"]),
                Quantity = Convert.ToInt32(r["Quantity"]),
                TemCode = r["TemCode"] == DBNull.Value ? null : Convert.ToString(r["TemCode"]),
                RawQr = r["QrData"] == DBNull.Value ? null : Convert.ToString(r["QrData"]),
                ImportDate = r["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ImportDate"]),
                PhieuStatus = r["PhieuStatus"] == DBNull.Value ? (byte)255 : Convert.ToByte(r["PhieuStatus"])
            };
        }

        public string GetLotNo(int slotLotId)
            => Convert.ToString(ExecuteScalar("SELECT LotNo FROM dbo.FVN_SlotLot WHERE SiteCode=@site AND SlotLotId=@id", SiteParam(), new SqlParameter("@id", slotLotId)));

        public string GetItemCode(int slotLotId)
            => Convert.ToString(ExecuteScalar("SELECT ItemCode FROM dbo.FVN_SlotLot WHERE SiteCode=@site AND SlotLotId=@id", SiteParam(), new SqlParameter("@id", slotLotId)));

        public int? GetSlotId(int slotLotId)
        {
            object value = ExecuteScalar("SELECT SlotId FROM dbo.FVN_SlotLot WHERE SiteCode=@site AND SlotLotId=@id", SiteParam(), new SqlParameter("@id", slotLotId));
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        public void DecreaseLotQuantity(int slotLotId, int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            int affected = ExecuteNonQuery(
                "UPDATE dbo.FVN_SlotLot SET Quantity = Quantity - @q WHERE SiteCode=@site AND SlotLotId=@id AND Quantity >= @q",
                SiteParam(), new SqlParameter("@q", quantity), new SqlParameter("@id", slotLotId));
            if (affected != 1) throw new InvalidOperationException($"SlotLot {slotLotId} không đủ tồn hoặc đã thay đổi.");
            UpdateSlotHeader(slotLotId);
        }

        public void AddQuantity(int slotId, int quantity, string itemCode)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            ExecuteNonQuery(
                "UPDATE dbo.FVN_Slot SET Quantity = ISNULL(Quantity,0) + @q, IsOccupied = 1 WHERE SiteCode=@site AND SlotId=@id",
                SiteParam(), new SqlParameter("@q", quantity), new SqlParameter("@id", slotId));
        }

        public bool TryRegisterMovement(string referenceType, string referenceId, string qrData, string temCode, bool checkExistingQr)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("TryRegisterMovement phải chạy trong cùng UnitOfWork transaction với thao tác tồn kho.");

            bool hasType = !string.IsNullOrWhiteSpace(referenceType);
            bool hasId = !string.IsNullOrWhiteSpace(referenceId);
            if (hasType != hasId)
                throw new ArgumentException("ReferenceType và ReferenceId phải cùng được cung cấp hoặc cùng để trống.");

            string qr = string.IsNullOrWhiteSpace(qrData) ? null : qrData.Trim();
            string tem = string.IsNullOrWhiteSpace(temCode) ? null : temCode.Trim();
            if (!hasType && qr == null && tem == null)
                return true;

            if (checkExistingQr && (qr != null || tem != null))
            {
                object existing = ExecuteScalar(@"
SELECT TOP (1) 1
FROM dbo.FVN_SlotLot WITH (UPDLOCK, HOLDLOCK)
WHERE SiteCode = @site
  AND ((@qr IS NOT NULL AND QrData = @qr)
    OR (@tem IS NOT NULL AND TemCode = @tem));",
                    SiteParam(),
                    new SqlParameter("@qr", (object)qr ?? DBNull.Value),
                    new SqlParameter("@tem", (object)tem ?? DBNull.Value));
                if (existing != null && existing != DBNull.Value)
                    return false;
            }

            try
            {
                ExecuteNonQuery($@"
INSERT INTO {_movementGuardTable}
    (ReferenceType, ReferenceId, QrHash, TemHash, CreatedAt)
VALUES
    (@referenceType, @referenceId,
     CASE WHEN @qr IS NULL THEN NULL ELSE HASHBYTES('SHA2_256', CONVERT(varbinary(max), @qr)) END,
     CASE WHEN @tem IS NULL THEN NULL ELSE HASHBYTES('SHA2_256', CONVERT(varbinary(max), @tem)) END,
     SYSUTCDATETIME());",
                    new SqlParameter("@referenceType", hasType ? (object)referenceType.Trim() : DBNull.Value),
                    new SqlParameter("@referenceId", hasId ? (object)referenceId.Trim() : DBNull.Value),
                    new SqlParameter("@qr", (object)qr ?? DBNull.Value),
                    new SqlParameter("@tem", (object)tem ?? DBNull.Value));
                return true;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return false;
            }
        }

        public void AddLot(int slotId, StockSlotLot lot)
        {
            if (lot == null) throw new ArgumentNullException(nameof(lot));
            if (string.IsNullOrWhiteSpace(lot.LotNo)) throw new ArgumentException("LotNo không được rỗng.", nameof(lot));
            if (lot.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(lot.Quantity));
            if (!HasTransaction) throw new InvalidOperationException("AddLot phải chạy trong transaction để SlotLot và Slot header được cập nhật nguyên tử.");

            ExecuteNonQuery(
                @"INSERT INTO dbo.FVN_SlotLot
                  (SiteCode, SlotId, ItemCode, LotNo, Quantity, TemCode, QrData, ImportDate, CreatedDate,
                   NgaySX, SoPhieuTong, MaPhieu, ParentSoPhieu, PhieuStatus, MaterialLotId)
                  VALUES
                  (@site,@slot,@item,@lot,@q,@tem,@qr,@date,GETDATE(),
                   @ngaySx,@soPhieuTong,@maPhieu,@parentSoPhieu,@status,
                   (SELECT ml.MaterialLotId FROM MES.FVN_MaterialLot ml
                     WHERE ml.SiteCode=@site AND ml.PartNo=@item AND ml.LotNo=@lot))",
                SiteParam(),
                new SqlParameter("@slot", slotId),
                new SqlParameter("@item", (object)lot.ItemCode ?? DBNull.Value),
                new SqlParameter("@lot", lot.LotNo),
                new SqlParameter("@q", lot.Quantity),
                new SqlParameter("@tem", (object)lot.TemCode ?? DBNull.Value),
                new SqlParameter("@qr", (object)lot.RawQr ?? DBNull.Value),
                new SqlParameter("@date", (object)(lot.ImportDate ?? DateTime.Now)),
                new SqlParameter("@ngaySx", (object)lot.NgaySX ?? DBNull.Value),
                new SqlParameter("@soPhieuTong", (object)lot.SoPhieuTong ?? DBNull.Value),
                new SqlParameter("@maPhieu", (object)lot.MaPhieu ?? DBNull.Value),
                new SqlParameter("@parentSoPhieu", (object)lot.ParentSoPhieu ?? DBNull.Value),
                new SqlParameter("@status", SqlDbType.TinyInt) { Value = lot.PhieuStatus });
            UpdateSlotHeaderBySlotId(slotId);
        }

        public StockSlotTakeResult TakeLot(int slotId, string lotNo, string itemCode, int quantity)
        {
            if (slotId <= 0) throw new ArgumentOutOfRangeException(nameof(slotId));
            if (string.IsNullOrWhiteSpace(lotNo)) throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
            if (string.IsNullOrWhiteSpace(itemCode)) throw new ArgumentException("ItemCode không được rỗng.", nameof(itemCode));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (!HasTransaction) throw new InvalidOperationException("TakeLot phải chạy trong transaction.");

            DataTable rows = LoadData(
                @"SELECT SlotLotId, LotNo, ItemCode, Quantity, TemCode, QrData, ImportDate
                  FROM dbo.FVN_SlotLot WITH (UPDLOCK, ROWLOCK)
                  WHERE SiteCode=@site AND SlotId=@slot AND PhieuStatus=0 AND Quantity>0
                    AND " + LotCodeHelper.BuildLotMatchSql("LotNo", "@lot") + @" AND ItemCode=@item
                  ORDER BY ImportDate ASC, SlotLotId ASC",
                SiteParam(),
                new SqlParameter("@slot", slotId),
                new SqlParameter("@lot", lotNo),
                new SqlParameter("@item", itemCode));

            int remain = quantity;
            var exports = new List<StockSlotLot>();
            foreach (DataRow row in rows.Rows)
            {
                if (remain <= 0) break;
                int id = Convert.ToInt32(row["SlotLotId"]);
                int available = Convert.ToInt32(row["Quantity"]);
                int take = Math.Min(remain, available);

                int affected = ExecuteNonQuery(
                    "UPDATE dbo.FVN_SlotLot SET Quantity = Quantity - @q WHERE SiteCode=@site AND SlotLotId=@id AND Quantity >= @q",
                    SiteParam(), new SqlParameter("@q", take), new SqlParameter("@id", id));
                if (affected != 1)
                    throw new InvalidOperationException($"SlotLot {id} đã thay đổi trong lúc Pick.");

                exports.Add(new StockSlotLot
                {
                    LotNo = row["LotNo"].ToString() ?? string.Empty,
                    ItemCode = row["ItemCode"].ToString() ?? string.Empty,
                    Quantity = take,
                    TemCode = row["TemCode"] == DBNull.Value ? null : row["TemCode"].ToString(),
                    RawQr = row["QrData"] == DBNull.Value ? null : row["QrData"].ToString(),
                    ImportDate = row["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ImportDate"])
                });
                remain -= take;
            }

            if (remain > 0)
                throw new InvalidOperationException($"LOT [{lotNo}] / Item [{itemCode}] trong Slot {slotId} không đủ {quantity}.");

            UpdateSlotHeaderBySlotId(slotId);
            return new StockSlotTakeResult { Quantity = quantity, ExportLots = exports };
        }

        private void UpdateSlotHeader(int slotLotId)
        {
            object slot = ExecuteScalar("SELECT SlotId FROM dbo.FVN_SlotLot WHERE SiteCode=@site AND SlotLotId=@id", SiteParam(), new SqlParameter("@id", slotLotId));
            if (slot != null && slot != DBNull.Value)
                UpdateSlotHeaderBySlotId(Convert.ToInt32(slot));
        }

        private void UpdateSlotHeaderBySlotId(int slotId)
        {
            ExecuteNonQuery(
                @"UPDATE s
                  SET s.Quantity = ISNULL(x.TotalQty,0),
                      s.IsOccupied = CASE WHEN ISNULL(x.TotalQty,0) > 0 THEN 1 ELSE 0 END
                  FROM dbo.FVN_Slot s
                  OUTER APPLY (SELECT SUM(Quantity) AS TotalQty FROM dbo.FVN_SlotLot
                               WHERE SiteCode=@site AND SlotId=@slot AND PhieuStatus = 0) x
                  WHERE s.SiteCode=@site AND s.SlotId=@slot",
                SiteParam(), new SqlParameter("@slot", slotId));
        }

        /// <summary>
        /// Reconciles one ItemCode + LOT to an exact target in the caller-owned transaction.
        /// FVN_PCTP: the authority is MES.FVN_InventoryBalance. The difference is posted to the MES ledger
        /// (ADJUST_IN / ADJUST_OUT via dbo.usp_FVN_PCTP_PostStockMovement), then the dbo.FVN_SlotLot projection
        /// rows are brought to the same target (FIFO decrease, separate adjustment row on increase).
        /// FVN_STOCKTP.SLCONLAI is never written.
        /// </summary>
        public void AdjustLotToTarget(string itemCode, string lotNo, int targetQuantity,
            int? destinationSlotId, string reason, string actor, Guid requestId)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) throw new ArgumentException("ItemCode không được rỗng.", nameof(itemCode));
            if (string.IsNullOrWhiteSpace(lotNo)) throw new ArgumentException("LOT không được rỗng.", nameof(lotNo));
            if (targetQuantity < 0) throw new ArgumentOutOfRangeException(nameof(targetQuantity));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Phải nhập lý do điều chỉnh.", nameof(reason));
            if (requestId == Guid.Empty) throw new ArgumentException("RequestId không hợp lệ.", nameof(requestId));
            if (!HasTransaction) throw new InvalidOperationException("Điều chỉnh LOT phải chạy trong UnitOfWork transaction.");

            string item = itemCode.Trim();
            string lot = lotNo.Trim();
            string user = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor.Trim();
            string requestKey = requestId.ToString("N");

            object duplicate = ExecuteScalar(
                "SELECT COUNT(1) FROM dbo.FVN_StockLotAdjustmentHistory WITH (UPDLOCK,HOLDLOCK) WHERE RequestId=@id",
                new SqlParameter("@id", requestId));
            if (Convert.ToInt32(duplicate) > 0)
                throw new InvalidOperationException("RequestId này đã được xử lý; không chạy lặp lại.");

            // LOT phải có sẵn trong MES (không tự tạo LOT mới từ màn điều chỉnh, giữ đúng hành vi cũ:
            // trước đây STOCKTP phải có đúng một dòng cho LOT).
            var balance = new MesStockBalanceRepository(Db, Uow, _site);
            StockLotKey key = balance.ResolveLot(item, lot, false);
            if (key == null)
                throw new InvalidOperationException("LOT [" + lot + "] của mã [" + item + "] chưa có trong MES.FVN_MaterialLot tại site "
                    + _site.SiteCode + "; từ chối điều chỉnh chéo mã hàng/LOT lạ.");

            int stockBefore = ReadMesOnHand(key);

            object invalidIdentityRows = ExecuteScalar(
                @"SELECT COUNT(1) FROM dbo.FVN_SlotLot WITH (UPDLOCK,HOLDLOCK)
                  WHERE SiteCode=@site AND PhieuStatus=0 AND Quantity>0
                    AND " + LotCodeHelper.BuildLotMatchSql("LotNo", "@lot") + @"
                    AND (ItemCode IS NULL OR UPPER(LTRIM(RTRIM(ItemCode)))<>UPPER(@item))",
                SiteParam(), new SqlParameter("@lot", lot), new SqlParameter("@item", item));
            if (Convert.ToInt32(invalidIdentityRows) > 0)
                throw new InvalidOperationException("Có SlotLot của LOT này thiếu ItemCode hoặc thuộc mã hàng khác; từ chối điều chỉnh.");

            List<BulkSlotLotLocation> locations = FindSlotLotsByLot(lot, -1);
            foreach (BulkSlotLotLocation location in locations)
                if (!string.Equals(location.ItemCode, item, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("LOT đang xuất hiện với ItemCode khác tại Slot " + location.SlotId + ".");
            int slotBefore = locations.Sum(x => x.Quantity);
            int slotDelta = targetQuantity - slotBefore;
            int stockDelta = targetQuantity - stockBefore;
            int? actualDestination = null;
            int ledgerSeq = 0;

            // ---- 1) MES ledger (nguồn tồn) ----
            if (stockDelta < 0)
            {
                int remainingOut = -stockDelta;
                DataTable bySlot = LoadData(
                    @"SELECT m.SlotId, SUM(b.OnHandQty - b.ReservedQty) AS Qty
                      FROM MES.FVN_InventoryBalance b WITH (UPDLOCK, HOLDLOCK)
                      JOIN dbo.FVN_SlotMesLocation m
                        ON m.SiteCode = b.SiteCode AND m.LocationNo = b.LocationNo AND m.IsPrimary = 1 AND m.IsActive = 1
                      WHERE b.SiteCode=@site AND b.PartNo=@item AND b.MaterialLotId=@lotId AND b.StockState='AVAILABLE'
                      GROUP BY m.SlotId
                      HAVING SUM(b.OnHandQty - b.ReservedQty) > 0
                      ORDER BY m.SlotId",
                    SiteParam(), new SqlParameter("@item", item), new SqlParameter("@lotId", key.MaterialLotId));
                foreach (DataRow row in bySlot.Rows)
                {
                    if (remainingOut == 0) break;
                    int take = (int)Math.Min((decimal)remainingOut, Convert.ToDecimal(row["Qty"]));
                    if (take <= 0) continue;
                    balance.Post(new StockLedgerEntry
                    {
                        MovementType = "ADJUST_OUT",
                        Lot = key,
                        Quantity = take,
                        FromSlotId = Convert.ToInt32(row["SlotId"]),
                        ReferenceType = "PCTP_LOT_ADJUST",
                        ReferenceNo = requestKey,
                        ReferenceLineNo = ++ledgerSeq,
                        IdempotencyKey = "LOTADJ:" + _site.SiteCode + ":" + requestKey + ":" + ledgerSeq,
                        ActorCode = user
                    });
                    remainingOut -= take;
                }
                if (remainingOut != 0)
                    throw new InvalidOperationException("MES không đủ tồn AVAILABLE (chưa giữ chỗ) để giảm " + (-stockDelta) + "; rollback toàn bộ.");
            }
            else if (stockDelta > 0)
            {
                int ledgerSlot = ResolveAdjustmentDestination(destinationSlotId);
                LockSlotForUpdate(ledgerSlot);
                balance.Post(new StockLedgerEntry
                {
                    MovementType = "ADJUST_IN",
                    Lot = key,
                    Quantity = stockDelta,
                    ToSlotId = ledgerSlot,
                    ReferenceType = "PCTP_LOT_ADJUST",
                    ReferenceNo = requestKey,
                    ReferenceLineNo = ++ledgerSeq,
                    IdempotencyKey = "LOTADJ:" + _site.SiteCode + ":" + requestKey + ":" + ledgerSeq,
                    ActorCode = user
                });
            }

            // ---- 2) FVN_SlotLot projection ----
            if (slotDelta < 0)
            {
                int remaining = -slotDelta;
                foreach (BulkSlotLotLocation location in locations)
                {
                    if (remaining == 0) break;
                    int take = Math.Min(remaining, location.Quantity);
                    LockSlotForUpdate(location.SlotId);
                    TakeLot(location.SlotId, lot, item, take);
                    remaining -= take;
                }
                if (remaining != 0)
                    throw new InvalidOperationException("Không thể giảm đủ SlotLot theo FIFO; rollback toàn bộ.");
            }
            else if (slotDelta > 0)
            {
                bool useDefaultA0 = !destinationSlotId.HasValue || destinationSlotId.Value <= 0;
                int targetSlot = ResolveAdjustmentDestination(destinationSlotId);
                LockSlotForUpdate(targetSlot);

                DataTable slotInfo = LoadData(
                    @"SELECT s.Capacity, ISNULL(x.TotalQty,0) AS CurrentQty,
                             ISNULL(x.OtherItemCount,0) AS OtherItemCount
                      FROM dbo.FVN_Slot s WITH (UPDLOCK,ROWLOCK)
                      OUTER APPLY (
                          SELECT SUM(Quantity) AS TotalQty,
                                 COUNT(DISTINCT CASE WHEN UPPER(LTRIM(RTRIM(ItemCode)))<>UPPER(@item) THEN ItemCode END) AS OtherItemCount
                          FROM dbo.FVN_SlotLot WHERE SiteCode=s.SiteCode AND SlotId=s.SlotId AND PhieuStatus=0 AND Quantity>0
                      ) x WHERE s.SiteCode=@site AND s.SlotId=@slot",
                    SiteParam(), new SqlParameter("@item", item), new SqlParameter("@slot", targetSlot));
                if (slotInfo.Rows.Count != 1) throw new InvalidOperationException("Không tìm thấy Slot đích " + targetSlot + ".");
                int capacity = Convert.ToInt32(slotInfo.Rows[0]["Capacity"]);
                int current = Convert.ToInt32(slotInfo.Rows[0]["CurrentQty"]);
                // A0 is the shared virtual bulk slot and intentionally contains many ItemCodes.
                // A manually selected physical Slot remains single-item.
                if (!useDefaultA0 && Convert.ToInt32(slotInfo.Rows[0]["OtherItemCount"]) > 0)
                    throw new InvalidOperationException("Slot đích đang chứa mã hàng khác.");
                if ((long)current + slotDelta > capacity)
                    throw new InvalidOperationException("Slot đích không đủ sức chứa.");
                AddLot(targetSlot, new StockSlotLot {
                    LotNo = lot, ItemCode = item, Quantity = slotDelta,
                    TemCode = null, RawQr = null, ImportDate = DateTime.Now, PhieuStatus = 0
                });
                actualDestination = targetSlot;
            }

            // ---- 3) Invariant: MES on-hand == FVN_SlotLot projection == target ----
            int slotAfter = Convert.ToInt32(ExecuteScalar(
                @"SELECT ISNULL(SUM(Quantity),0) FROM dbo.FVN_SlotLot WITH (UPDLOCK,HOLDLOCK)
                  WHERE SiteCode=@site AND PhieuStatus=0 AND Quantity>0 AND " + LotCodeHelper.BuildLotMatchSql("LotNo", "@lot") + @"
                    AND UPPER(LTRIM(RTRIM(ItemCode)))=UPPER(@item)",
                SiteParam(), new SqlParameter("@lot", lot), new SqlParameter("@item", item)));
            int stockAfter = ReadMesOnHand(key);
            if (stockAfter != targetQuantity || slotAfter != targetQuantity)
                throw new InvalidOperationException("Invariant không đạt: MES=" + stockAfter + ", SlotLot=" + slotAfter + ", target=" + targetQuantity + ".");

            ExecuteNonQuery(
                @"INSERT INTO dbo.FVN_StockLotAdjustmentHistory
                    (RequestId,SiteCode,ItemCode,LotNo,MaterialLotId,StockBefore,StockAfter,SlotBefore,SlotAfter,
                     StockDelta,SlotDelta,DestinationSlotId,Reason,Actor,CreatedAt)
                  VALUES (@id,@site,@item,@lot,@mlot,@sb,@sa,@lb,@la,@sd,@ld,@dest,@reason,@actor,SYSUTCDATETIME())",
                SiteParam(),
                new SqlParameter("@id", requestId), new SqlParameter("@item", item), new SqlParameter("@lot", lot),
                new SqlParameter("@mlot", SqlDbType.BigInt) { Value = key.MaterialLotId },
                new SqlParameter("@sb", stockBefore), new SqlParameter("@sa", stockAfter),
                new SqlParameter("@lb", slotBefore), new SqlParameter("@la", slotAfter),
                new SqlParameter("@sd", stockDelta), new SqlParameter("@ld", slotDelta),
                new SqlParameter("@dest", (object)actualDestination ?? DBNull.Value),
                new SqlParameter("@reason", reason.Trim()), new SqlParameter("@actor", user));
        }

        private int ReadMesOnHand(StockLotKey key)
        {
            object v = ExecuteScalar(
                @"SELECT ISNULL(SUM(OnHandQty),0) FROM MES.FVN_InventoryBalance WITH (UPDLOCK,HOLDLOCK)
                  WHERE SiteCode=@site AND PartNo=@item AND MaterialLotId=@lotId",
                SiteParam(), new SqlParameter("@item", key.PartNo),
                new SqlParameter("@lotId", SqlDbType.BigInt) { Value = key.MaterialLotId });
            return Convert.ToInt32(Convert.ToDecimal(v));
        }

        /// <summary>Slot đích của điều chỉnh tăng: Slot được chọn, hoặc Slot ảo A0. Phải có MES location chính.</summary>
        private int ResolveAdjustmentDestination(int? destinationSlotId)
        {
            bool useDefaultA0 = !destinationSlotId.HasValue || destinationSlotId.Value <= 0;
            int slotId = useDefaultA0
                ? GetOrCreateVirtualSlotId("KHO_AO_NHAP_LOAT", "RACK_AO", 999999999)
                : destinationSlotId.Value;
            object mapped = ExecuteScalar(
                @"SELECT COUNT(1) FROM dbo.FVN_SlotMesLocation
                  WHERE SiteCode=@site AND SlotId=@slot AND IsPrimary=1 AND IsActive=1",
                SiteParam(), new SqlParameter("@slot", slotId));
            if (Convert.ToInt32(mapped) == 0)
                throw new InvalidOperationException("Slot " + slotId + " chưa có ánh xạ MES location chính (dbo.FVN_SlotMesLocation); "
                    + "quản trị viên cần cấp mapping trước khi điều chỉnh tăng vào Slot này.");
            return slotId;
        }

    }
}