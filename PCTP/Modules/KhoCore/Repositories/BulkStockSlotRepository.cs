using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.KhoVatLy.Repositories
{
    public sealed class BulkStockSlotRepository : SqlRepositoryBase, IBulkStockSlotRepository, IStockSlotRepository
    {
        public BulkStockSlotRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

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
              FROM Slot s
              JOIN Rack r ON r.RackId = s.RackId
              JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
              WHERE w.Name = @wh AND r.RackName = @rack",
                new SqlParameter("@wh", warehouseName),
                new SqlParameter("@rack", rackName));

            if (existing.Rows.Count > 0)
                return Convert.ToInt32(existing.Rows[0]["SlotId"]);

            int whId = Convert.ToInt32(ExecuteScalar(
                "INSERT INTO Warehouse (Name) OUTPUT INSERTED.WarehouseId VALUES (@n)",
                new SqlParameter("@n", warehouseName)));
            int rackId = Convert.ToInt32(ExecuteScalar(
                "INSERT INTO Rack (WarehouseId, RackName) OUTPUT INSERTED.RackId VALUES (@w,@r)",
                new SqlParameter("@w", whId), new SqlParameter("@r", rackName)));
            return Convert.ToInt32(ExecuteScalar(
                @"INSERT INTO Slot (RackId, SlotNumber, IsOccupied, Capacity, Quantity)
              OUTPUT INSERTED.SlotId
              VALUES (@rk, 1, 0, @cap, 0)",
                new SqlParameter("@rk", rackId), new SqlParameter("@cap", capacity)));
        }

        public void LockSlotForUpdate(int slotId)
        {
            if (!HasTransaction)
                throw new InvalidOperationException("LockSlotForUpdate phải chạy trong transaction (Uow.Begin() trước).");
            ExecuteScalar("SELECT SlotId FROM Slot WITH (UPDLOCK, ROWLOCK) WHERE SlotId = @SlotId", new SqlParameter("@SlotId", slotId));
        }

        public List<LotInfo> GetLots(int slotId)
        {
            DataTable dt = LoadData(
                @"SELECT ItemCode, LotNo, Quantity, TemCode, QrData, MaPhieu, ImportDate
              FROM SlotLot WHERE SlotId = @SlotId ORDER BY LotNo",
                new SqlParameter("@SlotId", slotId));

            var lots = new List<LotInfo>();
            foreach (DataRow row in dt.Rows)
            {
                string lotNo = row["LotNo"] == DBNull.Value ? "" : row["LotNo"].ToString();
                int quantity = Convert.ToInt32(row["Quantity"]);
                string temCode = row["TemCode"] == DBNull.Value ? "" : row["TemCode"].ToString();
                string qrData = row["QrData"] == DBNull.Value ? "" : row["QrData"].ToString();
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
                        ItemCode = row["ItemCode"] == DBNull.Value ? "" : row["ItemCode"].ToString(),
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

        // ================================================================
        // IStockSlotRepository — central KhoCore mutation boundary
        // ================================================================

        public int GetLotQuantity(int slotLotId)
            => Convert.ToInt32(ExecuteScalar("SELECT ISNULL(Quantity,0) FROM SlotLot WHERE SlotLotId=@id", new SqlParameter("@id", slotLotId)) ?? 0);

        public string GetLotNo(int slotLotId)
            => Convert.ToString(ExecuteScalar("SELECT LotNo FROM SlotLot WHERE SlotLotId=@id", new SqlParameter("@id", slotLotId)));

        public string GetItemCode(int slotLotId)
            => Convert.ToString(ExecuteScalar("SELECT ItemCode FROM SlotLot WHERE SlotLotId=@id", new SqlParameter("@id", slotLotId)));

        public int? GetSlotId(int slotLotId)
        {
            object value = ExecuteScalar("SELECT SlotId FROM SlotLot WHERE SlotLotId=@id", new SqlParameter("@id", slotLotId));
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        public void DecreaseLotQuantity(int slotLotId, int quantity)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            int affected = ExecuteNonQuery(
                "UPDATE SlotLot SET Quantity = Quantity - @q WHERE SlotLotId=@id AND Quantity >= @q",
                new SqlParameter("@q", quantity), new SqlParameter("@id", slotLotId));
            if (affected != 1) throw new InvalidOperationException($"SlotLot {slotLotId} không đủ tồn hoặc đã thay đổi.");
            UpdateSlotHeader(slotLotId);
        }

        public void AddQuantity(int slotId, int quantity, string itemCode)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            ExecuteNonQuery(
                "UPDATE Slot SET Quantity = ISNULL(Quantity,0) + @q, IsOccupied = 1 WHERE SlotId=@id",
                new SqlParameter("@q", quantity), new SqlParameter("@id", slotId));
        }

        public void AddLot(int slotId, StockSlotLot lot)
        {
            if (lot == null) throw new ArgumentNullException(nameof(lot));
            if (string.IsNullOrWhiteSpace(lot.LotNo)) throw new ArgumentException("LotNo không được rỗng.", nameof(lot));
            if (lot.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(lot.Quantity));

            ExecuteNonQuery(
                @"INSERT INTO SlotLot (SlotId, ItemCode, LotNo, Quantity, TemCode, QrData, ImportDate, PhieuStatus)
                  VALUES (@slot,@item,@lot,@q,@tem,@qr,@date,0)",
                new SqlParameter("@slot", slotId),
                new SqlParameter("@item", (object)lot.ItemCode ?? DBNull.Value),
                new SqlParameter("@lot", lot.LotNo),
                new SqlParameter("@q", lot.Quantity),
                new SqlParameter("@tem", (object)lot.TemCode ?? DBNull.Value),
                new SqlParameter("@qr", (object)lot.RawQr ?? DBNull.Value),
                new SqlParameter("@date", (object)(lot.ImportDate ?? DateTime.Now)));
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
                  FROM SlotLot WITH (UPDLOCK, ROWLOCK)
                  WHERE SlotId=@slot AND PhieuStatus=0 AND Quantity>0
                    AND LEFT(LotNo,13)=LEFT(@lot,13) AND ItemCode=@item
                  ORDER BY ImportDate ASC, SlotLotId ASC",
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
                    "UPDATE SlotLot SET Quantity = Quantity - @q WHERE SlotLotId=@id AND Quantity >= @q",
                    new SqlParameter("@q", take), new SqlParameter("@id", id));
                if (affected != 1)
                    throw new InvalidOperationException($"SlotLot {id} đã thay đổi trong lúc Pick.");

                exports.Add(new StockSlotLot
                {
                    LotNo = row["LotNo"].ToString(),
                    ItemCode = row["ItemCode"].ToString(),
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
            object slot = ExecuteScalar("SELECT SlotId FROM SlotLot WHERE SlotLotId=@id", new SqlParameter("@id", slotLotId));
            if (slot != null && slot != DBNull.Value)
                UpdateSlotHeaderBySlotId(Convert.ToInt32(slot));
        }

        private void UpdateSlotHeaderBySlotId(int slotId)
        {
            ExecuteNonQuery(
                @"UPDATE s
                  SET s.Quantity = ISNULL(x.TotalQty,0),
                      s.IsOccupied = CASE WHEN ISNULL(x.TotalQty,0) > 0 THEN 1 ELSE 0 END
                  FROM Slot s
                  OUTER APPLY (SELECT SUM(Quantity) AS TotalQty FROM SlotLot WHERE SlotId=@slot) x
                  WHERE s.SlotId=@slot",
                new SqlParameter("@slot", slotId));
        }
    }
}