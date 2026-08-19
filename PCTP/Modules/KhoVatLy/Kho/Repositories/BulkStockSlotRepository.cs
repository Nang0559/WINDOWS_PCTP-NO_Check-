using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Fuction;
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
    public sealed class BulkStockSlotRepository : SqlRepositoryBase, IBulkStockSlotRepository
    {
        public BulkStockSlotRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        public int GetOrCreateVirtualSlotId(string warehouseName, string rackName, int capacity)
        {
            if (!HasTransaction)
                throw new InvalidOperationException(
                    "GetOrCreateVirtualSlotId phải chạy trong transaction (Uow.Begin() trước).");

            // ── Khoá tài nguyên logic để 2 transaction không cùng tạo trùng
            // Warehouse/Rack/Slot ảo lần đầu tiên. LockOwner='Transaction' tự
            // giải phóng khi Commit/Rollback — không cần tự gọi sp_releaseapplock.
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

            int lockCode = lockResult == null || lockResult == DBNull.Value
                ? -999 : Convert.ToInt32(lockResult);

            if (lockCode < 0)
                throw new InvalidOperationException(
                    $"Không lấy được khoá tạo Slot ảo A0 (mã lỗi {lockCode}). Thử lại sau.");

            // ── Sau khi có khoá, an toàn để check-then-create ──────────────────
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
                new SqlParameter("@w", whId),
                new SqlParameter("@r", rackName)));

            int slotId = Convert.ToInt32(ExecuteScalar(
                @"INSERT INTO Slot (RackId, SlotNumber, IsOccupied, Capacity, Quantity)
              OUTPUT INSERTED.SlotId
              VALUES (@rk, 1, 0, @cap, 0)",
                new SqlParameter("@rk", rackId),
                new SqlParameter("@cap", capacity)));

            return slotId;
        }

        public void LockSlotForUpdate(int slotId)
        {
            if (!HasTransaction)
                throw new InvalidOperationException(
                    "LockSlotForUpdate phải chạy trong transaction (Uow.Begin() trước).");

            // Bất kỳ transaction nào khác cũng SELECT ... WITH (UPDLOCK, ROWLOCK)
            // trên cùng SlotId sẽ bị BLOCK ở đây cho tới khi transaction hiện
            // tại Commit/Rollback -> serialize hoá đọc-sửa-ghi SlotLot.
            ExecuteScalar(
                "SELECT SlotId FROM Slot WITH (UPDLOCK, ROWLOCK) WHERE SlotId = @SlotId",
                new SqlParameter("@SlotId", slotId));
        }

        public List<LotInfo> GetLots(int slotId)
        {
            DataTable dt = LoadData(
                @"SELECT ItemCode, LotNo, Quantity, TemCode, QrData, MaPhieu, ImportDate
              FROM SlotLot
              WHERE SlotId = @SlotId
              ORDER BY LotNo",
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
                        ImportDate = row["ImportDate"] == DBNull.Value
                            ? (DateTime?)null : Convert.ToDateTime(row["ImportDate"]),
                        RawQr = qrData
                    };
                }
                else
                {
                    qrInfo.Quantity = quantity;
                }

                lots.Add(new LotInfo
                {
                    LotNo = lotNo,
                    Quantity = quantity,
                    TemCode = temCode,
                    RawQr = qrData,
                    QRInfo = qrInfo
                });
            }

            return lots;
        }

        public void SaveLots(int slotId, List<LotInfo> lots)
        {
            lots = lots ?? new List<LotInfo>();

            ExecuteNonQuery(
                "DELETE FROM SlotLot WHERE SlotId = @SlotId",
                new SqlParameter("@SlotId", slotId));

            foreach (var lot in lots)
            {
                ExecuteNonQuery(
                    @"INSERT INTO SlotLot
                    (SlotId, ItemCode, LotNo, Quantity, TemCode, QrData, ImportDate, MaPhieu)
                  VALUES
                    (@SlotId, @ItemCode, @LotNo, @Quantity, @TemCode, @QrData, @ImportDate, @MaPhieu)",
                    new SqlParameter("@SlotId", slotId),
                    new SqlParameter("@ItemCode", (object)lot.QRInfo?.ItemCode ?? DBNull.Value),
                    new SqlParameter("@LotNo", (object)lot.LotNo ?? DBNull.Value),
                    new SqlParameter("@Quantity", lot.Quantity),
                    new SqlParameter("@TemCode", (object)lot.TemCode ?? DBNull.Value),
                    new SqlParameter("@QrData", (object)lot.QRInfo?.RawQr ?? DBNull.Value),
                    new SqlParameter("@ImportDate", (object)lot.QRInfo?.ImportDate ?? DateTime.Now),
                    new SqlParameter("@MaPhieu", (object)lot.QRInfo?.MaPhieu ?? DBNull.Value));
            }
        }

        public void UpdateSlotHeaderFromLots(int slotId, List<LotInfo> lots)
        {
            // Tính lại Header trực tiếp từ SlotLot vừa ghi trong CÙNG transaction —
            // không dựa vào tham số lots truyền vào để tránh lệch nếu caller quên cập nhật.
            ExecuteNonQuery(
                @"UPDATE s
              SET
                  Quantity = (SELECT ISNULL(SUM(sl.Quantity),0) FROM SlotLot sl WHERE sl.SlotId = s.SlotId),
                  ItemCode = (SELECT TOP (1) sl.ItemCode FROM SlotLot sl WHERE sl.SlotId = s.SlotId
                              ORDER BY sl.ImportDate DESC),
                  ImportDate = (SELECT TOP (1) sl.ImportDate FROM SlotLot sl WHERE sl.SlotId = s.SlotId
                              ORDER BY sl.ImportDate DESC),
                  IsOccupied = CASE WHEN EXISTS (SELECT 1 FROM SlotLot sl WHERE sl.SlotId = s.SlotId)
                              THEN 1 ELSE 0 END
              FROM Slot s
              WHERE s.SlotId = @SlotId",
                new SqlParameter("@SlotId", slotId));
        }
    }
}
