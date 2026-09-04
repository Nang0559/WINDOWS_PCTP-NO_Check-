using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Models;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Models;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Repositories
{

    public sealed class SlotRepository
     : SqlRepositoryBase, ISlotRepository
    {
        public SlotRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // TÌM SLOT
        // ============================================================

        public int GetSlotId(
            string warehouseName,
            string rackName,
            int slotNumber)
        {
            if (string.IsNullOrWhiteSpace(warehouseName))
                throw new ArgumentException("Tên kho không được rỗng.", nameof(warehouseName));

            if (string.IsNullOrWhiteSpace(rackName))
                throw new ArgumentException("Tên Rack không được rỗng.", nameof(rackName));

            if (slotNumber <= 0)
                throw new ArgumentException("Số Slot không hợp lệ.", nameof(slotNumber));

            const string sql = @"
            SELECT TOP 1 s.SlotId
            FROM Slot s
            INNER JOIN Rack r ON r.RackId = s.RackId
            INNER JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
            WHERE w.Name = @WarehouseName
              AND r.RackName = @RackName
              AND s.SlotNumber = @SlotNumber;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@WarehouseName", SqlDbType.NVarChar, 100) { Value = warehouseName.Trim() },
                new SqlParameter("@RackName", SqlDbType.NVarChar, 100) { Value = rackName.Trim() },
                new SqlParameter("@SlotNumber", SqlDbType.Int) { Value = slotNumber });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // CAPACITY
        // ============================================================

        public int GetCapacity(int slotId)
        {
            if (slotId <= 0) return 0;

            const string sql = "SELECT ISNULL(Capacity, 0) FROM Slot WHERE SlotId = @SlotId;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // QUANTITY
        // ============================================================

        public int GetQuantity(int slotId)
        {
            if (slotId <= 0) return 0;

            const string sql = "SELECT ISNULL(Quantity, 0) FROM Slot WHERE SlotId = @SlotId;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // QUANTITY - LOCK (phải gọi trong UnitOfWork transaction)
        // ============================================================

        public int GetQuantityWithLock(int slotId)
        {
            if (slotId <= 0) return 0;

            if (!HasTransaction)
                throw new InvalidOperationException("GetQuantityWithLock phải được gọi trong transaction.");

            const string sql = @"
            SELECT ISNULL(Quantity, 0)
            FROM Slot WITH (UPDLOCK, ROWLOCK)
            WHERE SlotId = @SlotId;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // ✅ FIX: đổi tên UpdateSlot → AddQuantity — CỘNG DỒN, khớp đúng lời gọi
        // trong NhapTpReceivingService.NhapTpVaoSlot. Đặt tên rõ ràng để không
        // nhầm với UpdateSlotAfterExport/CapNhatSlotHeader (SET tuyệt đối bên dưới).
        // ============================================================
        // Kho/Repositories/SlotRepository.cs — implement (kế thừa SqlRepositoryBase)
        public void LockSlotForUpdate(int slotId)
        {
            if (!HasTransaction)
                throw new InvalidOperationException(
                    "LockSlotForUpdate phải chạy trong transaction (Uow.Begin() trước).");

            ExecuteScalar(
                "SELECT SlotId FROM Slot WITH (UPDLOCK, ROWLOCK) WHERE SlotId = @SlotId",
                new SqlParameter("@SlotId", slotId));
        }
        public void AddQuantity(
        int slotId,
        int quantity,
        string itemCode,
        DateTime? importDate)
        {
            if (slotId <= 0)
                throw new ArgumentException(
                    "SlotId không hợp lệ.",
                    nameof(slotId));

            if (quantity <= 0)
                throw new ArgumentException(
                    "Số lượng phải lớn hơn 0.",
                    nameof(quantity));

            const string sql = @"
        UPDATE Slot
        SET
            Quantity = ISNULL(Quantity, 0) + @Quantity,
            ItemCode = @ItemCode,
            ImportDate = @ImportDate,
            IsOccupied = 1
        WHERE SlotId = @SlotId;";

            int affected = ExecuteNonQuery(
                sql,
                new SqlParameter("@Quantity", SqlDbType.Int)
                {
                    Value = quantity
                },
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100)
                {
                    Value = (object)itemCode ?? DBNull.Value
                },
                new SqlParameter("@ImportDate", SqlDbType.DateTime)
                {
                    Value = (object)importDate ?? DBNull.Value
                },
                new SqlParameter("@SlotId", SqlDbType.Int)
                {
                    Value = slotId
                });

            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy SlotId [{slotId}].");
        }

        // ============================================================
        // CÁC METHOD SET TUYỆT ĐỐI — dùng cho luồng xuất/di chuyển
        // ============================================================


        public void Clear(int slotId)
        {
            if (slotId <= 0)
                throw new ArgumentException("SlotId không hợp lệ.", nameof(slotId));

            const string sql = @"
UPDATE Slot
SET Quantity = 0, ItemCode = NULL, ImportDate = NULL, IsOccupied = 0
WHERE SlotId = @SlotId;";

            ExecuteNonQuery(sql,
                new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId });
        }

        public void UpdateQuantityFromLots(int slotId)
        {
            if (slotId <= 0)
                throw new ArgumentException(
                    "SlotId không hợp lệ.",
                    nameof(slotId));

            const string sql = @"
        UPDATE Slot
        SET Quantity = (
            SELECT ISNULL(SUM(sl.Quantity), 0)
            FROM SlotLot sl
            WHERE sl.SlotId = Slot.SlotId
              AND sl.PhieuStatus = 0
        )
        WHERE SlotId = @SlotId;";

            ExecuteNonQuery(
                sql,
                new SqlParameter("@SlotId", SqlDbType.Int)
                {
                    Value = slotId
                });
        }

        // ============================================================
        // ✅ FIX: implement GetLots — port từ SlotHelper.GetSlotLots cũ,
        // chỉ đổi cách gọi SQL sang LoadData(...) của SqlRepositoryBase.
        // ============================================================

        public List<LotInfo> GetLots(int slotId)
        {
            if (slotId <= 0) return new List<LotInfo>();

            const string sql = @"
            SELECT ItemCode, LotNo, Quantity, TemCode, QrData, MaPhieu, ImportDate
            FROM SlotLot
            WHERE SlotId = @SlotId AND PhieuStatus = 0
            ORDER BY LotNo;";

            DataTable dt = LoadData(sql,
                new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId });

            var lots = new List<LotInfo>();
            if (dt == null) return lots;

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
                else
                {
                    qrInfo.Quantity = quantity;
                    qrInfo.ImportDate = row["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ImportDate"]);
                }

                lots.Add(new LotInfo { LotNo = lotNo, Quantity = quantity, TemCode = temCode, RawQr = qrData, QRInfo = qrInfo });
            }

            return lots;
        }
        public List<SlotLotViewInfo> GetAllActiveSlotLots()
        {
            const string sql = @"
            SELECT s.SlotId, sl.SlotLotId,
                   w.Name AS WarehouseName, r.RackName, s.SlotNumber,
                   sl.ItemCode, sl.LotNo, sl.Quantity, sl.TemCode
            FROM SlotLot sl
            JOIN Slot s      ON s.SlotId = sl.SlotId
            JOIN Rack r      ON r.RackId = s.RackId
            JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
            WHERE sl.Quantity > 0
            ORDER BY w.Name, r.RackName, s.SlotNumber, sl.LotNo";

            DataTable dt = LoadData(sql);
            var list = new List<SlotLotViewInfo>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new SlotLotViewInfo
                {
                    SlotId = Convert.ToInt32(row["SlotId"]),
                    SlotLotId = row["SlotLotId"] == DBNull.Value ? 0 : Convert.ToInt32(row["SlotLotId"]),
                    WarehouseName = row["WarehouseName"] as string,
                    RackName = row["RackName"] as string,
                    SlotNumber = Convert.ToInt32(row["SlotNumber"]),
                    ItemCode = row["ItemCode"] as string,
                    LotNo = row["LotNo"] as string,
                    Quantity = row["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(row["Quantity"]),
                    TemCode = row["TemCode"] as string
                });
            }
            return list;
        }
        public SlotLotInfo GetSlotLotById(int slotLotId)
        {
            DataTable dt = LoadData(
                "SELECT SlotLotId, SlotId, LotNo, ItemCode, Quantity, TemCode " +
                "FROM SlotLot WHERE SlotLotId = @id",
                new SqlParameter("@id", slotLotId));

            if (dt.Rows.Count == 0) return null;

            DataRow r = dt.Rows[0];
            return new SlotLotInfo
            {
                SlotLotId = Convert.ToInt32(r["SlotLotId"]),
                SlotVatLyId = Convert.ToInt32(r["SlotId"]),
                LotNo = r["LotNo"] as string,
                ItemCode = r["ItemCode"] as string,
                Quantity = Convert.ToInt32(r["Quantity"]),
                TemCode = r["TemCode"] as string
            };
        }

        public void UpdateSlotLotQuantity(int slotLotId, int newQuantity)
        {
            int rows = ExecuteNonQuery(
                "UPDATE SlotLot SET Quantity = @qty WHERE SlotLotId = @id",
                new SqlParameter("@qty", newQuantity),
                new SqlParameter("@id", slotLotId));
            if (rows == 0)
                throw new InvalidOperationException($"Không tìm thấy SlotLot Id={slotLotId} để cập nhật.");
        }

        public void DeleteSlotLot(int slotLotId)
        {
            ExecuteNonQuery("DELETE FROM SlotLot WHERE SlotLotId = @id",
                new SqlParameter("@id", slotLotId));
        }
        // ============================================================
        // ✅ FIX: implement SaveLots — xoá cũ, insert lại toàn bộ
        // (giữ hành vi SlotHelper.SaveSlotLots cũ). Không tự UpdateQuantity
        // ở đây — caller (service tầng trên) tự gọi UpdateQuantity nếu cần,
        // để giữ đúng ranh giới transaction do IUnitOfWork quản lý.
        // ============================================================

        public void SaveLots(int slotId, List<LotInfo> lots)
        {
            if (slotId <= 0)
                throw new ArgumentException("SlotId không hợp lệ.", nameof(slotId));

            lots = lots ?? new List<LotInfo>();

            ExecuteNonQuery(
                "DELETE FROM SlotLot WHERE SlotId = @SlotId;",
                new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId });

            const string insertSql = @"
            INSERT INTO SlotLot
                (SlotId, ItemCode, LotNo, Quantity, TemCode, QrData, ImportDate, MaPhieu)
            VALUES
             (@SlotId, @ItemCode, @LotNo, @Quantity, @TemCode, @QrData, @ImportDate, @MaPhieu);";

            foreach (var lot in lots)
            {
                ExecuteNonQuery(insertSql,
                    new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId },
                    new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = (object)lot.QRInfo?.ItemCode ?? DBNull.Value },
                    new SqlParameter("@LotNo", SqlDbType.NVarChar, 200) { Value = (object)lot.LotNo ?? DBNull.Value },
                    new SqlParameter("@Quantity", SqlDbType.Int) { Value = lot.Quantity },
                    new SqlParameter("@TemCode", SqlDbType.NVarChar, 100) { Value = (object)lot.TemCode ?? DBNull.Value },
                    new SqlParameter("@QrData", SqlDbType.NVarChar, -1) { Value = (object)lot.QRInfo?.RawQr ?? DBNull.Value },
                    new SqlParameter("@ImportDate", SqlDbType.DateTime) { Value = (object)lot.QRInfo?.ImportDate ?? DateTime.Now },
                    new SqlParameter("@MaPhieu", SqlDbType.NVarChar, 100) { Value = (object)lot.QRInfo?.MaPhieu ?? DBNull.Value });
            }
        }

        public bool ExistsLot(int slotId, string lotNo)
        {
            if (slotId <= 0 || string.IsNullOrWhiteSpace(lotNo)) return false;

            const string sql = @"
SELECT COUNT(1) FROM SlotLot
WHERE SlotId = @SlotId AND LotNo = @LotNo AND PhieuStatus = 0;";

            object result = ExecuteScalar(sql,
                new SqlParameter("@SlotId", SqlDbType.Int) { Value = slotId },
                new SqlParameter("@LotNo", SqlDbType.NVarChar, 100) { Value = lotNo.Trim() });

            return DbValueHelper.ToInt(result) > 0;
        }

        // ============================================================
        // ✅ FIX: implement GetSlotsChuaLot — dùng LotCodeHelper.BuildLotMatchSql
        // thay vì "=" tuyệt đối, vì đầu vào `lot` có thể ở khoá cũ 13 ký tự
        // hoặc mới 20 ký tự (đúng nguyên tắc so khớp LOT xuyên suốt hệ thống).
        // ============================================================

        public List<SlotChuaLotInfo> GetSlotsChuaLot(string lot)
        {
            if (string.IsNullOrWhiteSpace(lot)) return new List<SlotChuaLotInfo>();

            string match = LotCodeHelper.BuildLotMatchSql("sl.LotNo", "@lot");

            string sql = $@"
SELECT sl.SlotId, sl.LotNo, sl.Quantity, sl.TemCode,
       w.Name AS WarehouseName, r.RackName, s.SlotNumber
FROM SlotLot sl
JOIN Slot s ON s.SlotId = sl.SlotId
JOIN Rack r ON r.RackId = s.RackId
JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
WHERE sl.PhieuStatus = 0 AND sl.Quantity > 0
  AND {match}
ORDER BY w.Name, r.RackName, s.SlotNumber;";

            DataTable dt = LoadData(sql,
                new SqlParameter("@lot", SqlDbType.NVarChar, 200) { Value = lot.Trim() });

            var result = new List<SlotChuaLotInfo>();
            if (dt == null) return result;

            foreach (DataRow row in dt.Rows)
            {
                result.Add(new SlotChuaLotInfo
                {
                    SlotId = Convert.ToInt32(row["SlotId"]),
                    ImportDate = Convert.ToDateTime(row["ImportDate"]),
                    Quantity = Convert.ToInt32(row["Quantity"]),
                    TemCode = row["TemCode"]?.ToString(),
                    WarehouseName = row["WarehouseName"]?.ToString(),
                    RackName = row["RackName"]?.ToString(),
                    SlotNumber = Convert.ToInt32(row["SlotNumber"])
                });
            }

            return result;
        }
        // SlotRepository.cs — thêm 2 method
        public List<string> GetEmptySlots(string itemCode, int soLuongNhap)
        {
            const string sql = @"
        SELECT w.Name AS WarehouseName, r.RackName, s.SlotNumber,
               s.IsOccupied, s.Quantity, s.Capacity,
               STUFF((
                   SELECT ',' + sl.TemCode
                   FROM SlotLot sl
                   WHERE sl.SlotId = s.SlotId AND sl.PhieuStatus = 0
                   FOR XML PATH(''), TYPE
               ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS TemCode
        FROM Slot s
        JOIN Rack r ON s.RackId = r.RackId
        JOIN Warehouse w ON r.WarehouseId = w.WarehouseId
        WHERE w.Name <> @BulkWh
          AND (
                (s.ItemCode = @ItemCode AND (s.Capacity - s.Quantity) >= @SoLuongNhap)
                OR (s.IsOccupied = 0)
          )
        ORDER BY CASE WHEN s.ItemCode = @ItemCode THEN 0 ELSE 1 END,
                 w.Name, r.RackName, s.SlotNumber;";

            DataTable dt = LoadData(sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100) { Value = (object)itemCode ?? DBNull.Value },
                new SqlParameter("@SoLuongNhap", SqlDbType.Int) { Value = soLuongNhap },
                new SqlParameter("@BulkWh", SqlDbType.NVarChar, 100) { Value = BulkImportConfig.WarehouseName });

            var result = new List<string>();
            foreach (DataRow row in dt.Rows)
            {
                string wh = row["WarehouseName"].ToString();
                string rack = row["RackName"].ToString();
                int slotNum = Convert.ToInt32(row["SlotNumber"]);
                int capacity = row["Capacity"] == DBNull.Value ? 0 : Convert.ToInt32(row["Capacity"]);
                bool isOccupied = Convert.ToBoolean(row["IsOccupied"]);

                string display = $"WH : {wh} - Rack : {rack} - Slot : {slotNum} - Capacity : {capacity}";
                if (isOccupied)
                {
                    string temCode = row["TemCode"]?.ToString() ?? "";
                    int qty = row["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(row["Quantity"]);
                    display += $" - TemCode: {temCode} - Qty: {qty}";
                }
                result.Add(display);
            }
            return result;
        }

        public string GetOrCreateNamedSlot(string warehouseName, string rackName, int capacity)
        {
            const string findSql = @"
        SELECT s.SlotNumber, s.Capacity
        FROM Slot s
        JOIN Rack r ON r.RackId = s.RackId
        JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
        WHERE w.Name = @wh AND r.RackName = @rack;";

            DataTable dt = LoadData(findSql,
                new SqlParameter("@wh", SqlDbType.NVarChar, 100) { Value = warehouseName },
                new SqlParameter("@rack", SqlDbType.NVarChar, 100) { Value = rackName });

            if (dt.Rows.Count > 0)
            {
                int slotNo = Convert.ToInt32(dt.Rows[0]["SlotNumber"]);
                int cap = Convert.ToInt32(dt.Rows[0]["Capacity"]);
                return $"WH : {warehouseName} - Rack : {rackName} - Slot : {slotNo} - Capacity : {cap}";
            }

            // Tự tạo mới Warehouse+Rack+Slot trong 1 transaction — giữ atomic vì cả 3
            // bảng đều được ghi trong cùng Uow của repo này (không cross-repo để tránh
            // vấn đề WarehouseRepository/RackRepository hiện KHÔNG kế thừa SqlRepositoryBase,
            // tức KHÔNG chia sẻ transaction — xem ghi chú bên dưới).
            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                int whId = DbValueHelper.ToInt(ExecuteScalar(
                    "INSERT INTO Warehouse (Name) OUTPUT INSERTED.WarehouseId VALUES (@n)",
                    new SqlParameter("@n", SqlDbType.NVarChar, 100) { Value = warehouseName }));

                int rackId = DbValueHelper.ToInt(ExecuteScalar(
                    "INSERT INTO Rack (WarehouseId, RackName) OUTPUT INSERTED.RackId VALUES (@w,@r)",
                    new SqlParameter("@w", SqlDbType.Int) { Value = whId },
                    new SqlParameter("@r", SqlDbType.NVarChar, 100) { Value = rackName }));

                ExecuteNonQuery(
                    "INSERT INTO Slot (RackId, SlotNumber, IsOccupied, Capacity, Quantity) VALUES (@rk, 1, 0, @cap, 0)",
                    new SqlParameter("@rk", SqlDbType.Int) { Value = rackId },
                    new SqlParameter("@cap", SqlDbType.Int) { Value = capacity });

                if (ownTransaction) Uow.Commit();
            }
            catch { if (ownTransaction) Uow.Rollback(); throw; }

            return $"WH : {warehouseName} - Rack : {rackName} - Slot : 1 - Capacity : {capacity}";
        }
        public void UpdateHeader(
        int slotId,
        string itemCode,
        DateTime? importDate,
        int quantity)
        {
            if (slotId <= 0)
                throw new ArgumentException(
                    "SlotId không hợp lệ.",
                    nameof(slotId));

            if (quantity < 0)
                throw new ArgumentException(
                    "Quantity không được âm.",
                    nameof(quantity));

            const string sql = @"
        UPDATE Slot
        SET
            ItemCode = @ItemCode,
            ImportDate = @ImportDate,
            Quantity = @Quantity,
            IsOccupied = CASE
                WHEN @Quantity > 0 THEN 1
                ELSE 0
            END
        WHERE SlotId = @SlotId;";

            int affected = ExecuteNonQuery(
                sql,
                new SqlParameter("@ItemCode", SqlDbType.NVarChar, 100)
                {
                    Value = string.IsNullOrWhiteSpace(itemCode)
                        ? (object)DBNull.Value
                        : itemCode.Trim()
                },
                new SqlParameter("@ImportDate", SqlDbType.DateTime)
                {
                    Value = importDate.HasValue
                        ? (object)importDate.Value
                        : DBNull.Value
                },
                new SqlParameter("@Quantity", SqlDbType.Int)
                {
                    Value = quantity
                },
                new SqlParameter("@SlotId", SqlDbType.Int)
                {
                    Value = slotId
                });

            if (affected == 0)
                throw new InvalidOperationException(
                    $"Không tìm thấy SlotId [{slotId}].");
        }
   
        public int GetSingleSlotIdInRack(string warehouseName, string rackName)
        {
            // Join qua Rack (RackName) — Warehouse chỉ dùng để xác nhận đúng kho ảo,
            // không lọc riêng vì Slot không có cột WarehouseName trực tiếp (whname
            // nằm ở cấp cao hơn theo BulkImportConfig.IsBulkSlot).
            DataTable dt = LoadData(
                "SELECT s.SlotId " +
                "FROM Slot s " +
                "INNER JOIN Rack r ON r.RackId = s.RackId " +
                "WHERE r.RackName = @rack",
                new SqlParameter("@rack", rackName));

            if (dt.Rows.Count != 1)
                return 0; // 0 hoặc >1 dòng đều coi là không xác định được — không đoán bừa

            return Convert.ToInt32(dt.Rows[0]["SlotId"]);
        }
        public void MoveLot(int fromSlotId, int toSlotId, string lotNo)
        {
            if (fromSlotId <= 0)
                throw new ArgumentException("SlotId nguồn không hợp lệ.", nameof(fromSlotId));
            if (toSlotId <= 0)
                throw new ArgumentException("SlotId đích không hợp lệ.", nameof(toSlotId));
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
            if (fromSlotId == toSlotId)
                throw new ArgumentException("Slot nguồn và đích không được trùng nhau.");

            // Giống pattern GetOrCreateNamedSlot: tự mở transaction nếu chưa có sẵn,
            // để MoveLot dùng độc lập được (không bắt buộc caller phải Uow.Begin() trước).
            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                // Khoá cả 2 Slot trước khi đọc — tránh race condition khi 2 luồng
                // cùng thao tác trên 1 trong 2 Slot này song song.
                LockSlotForUpdate(fromSlotId);
                LockSlotForUpdate(toSlotId);

                var sourceLots = GetLots(fromSlotId);
                var moving = sourceLots.FirstOrDefault(x =>
                    string.Equals(x.LotNo, lotNo, StringComparison.OrdinalIgnoreCase));

                if (moving == null)
                    throw new InvalidOperationException(
                        $"Không tìm thấy LOT [{lotNo}] trong Slot nguồn [{fromSlotId}].");

                // ── Ghi lại Slot nguồn (bỏ LOT vừa dời) ──────────────────────────
                var remaining = sourceLots
                    .Where(x => !string.Equals(x.LotNo, lotNo, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                SaveLots(fromSlotId, remaining);
                RecomputeHeader(fromSlotId, remaining);

                // ── Ghi lại Slot đích (merge nếu đích đã có cùng LotNo) ──────────
                var destLots = GetLots(toSlotId);
                var existing = destLots.FirstOrDefault(x =>
                    string.Equals(x.LotNo, lotNo, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                    existing.Quantity += moving.Quantity;
                else
                    destLots.Add(moving);

                SaveLots(toSlotId, destLots);
                RecomputeHeader(toSlotId, destLots);

                if (ownTransaction) Uow.Commit();
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }

        /// <summary>Tính lại header (Quantity/ItemCode/ImportDate) từ danh sách LOT hiện có
        /// rồi ghi bằng UpdateHeader — dùng nội bộ cho MoveLot để đồng bộ cả 2 Slot.</summary>
        private void RecomputeHeader(int slotId, List<LotInfo> lots)
        {
            int quantity = lots.Sum(x => x.Quantity);
            var latest = lots.Where(x => x.ImportDate.HasValue)
                              .OrderByDescending(x => x.ImportDate)
                              .FirstOrDefault();

            UpdateHeader(
                slotId,
                latest?.ItemCode,
                quantity > 0 ? latest?.ImportDate : null,
                quantity);
        }
    }
}
