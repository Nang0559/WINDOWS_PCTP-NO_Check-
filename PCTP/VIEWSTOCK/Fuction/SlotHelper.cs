using DevExpress.CodeParser;
using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace PCTP.VIEWSTOCK.Fuction
{
    public class SlotHelper
    {
        private SQLPROVIDER sqlpr = new SQLPROVIDER();
        public bool UpdateSlotInfo(
        string selectedSlot,
        string itemCode,
        DateTime importDate,
        int quantity)
        {
            ParseSlotString(
                selectedSlot,
                out string wh,
                out string rack,
                out int slot,
                out _);

            int slotId = GetSlotID(wh, rack, slot);

            return UpdateSlotInfo(
                slotId,
                itemCode,
                importDate,
                quantity);
        }
        public bool UpdateSlotInfo(
            int slotId,
            string itemCode,
            DateTime importDate,
            int quantity)
        {
            string sql = @"
        UPDATE Slot
        SET
            IsOccupied = CASE WHEN @Quantity > 0 THEN 1 ELSE 0 END,
            ItemCode   = @ItemCode,
            Quantity   = @Quantity,
            ImportDate = @ImportDate
        WHERE SlotId = @SlotId";

            SqlParameter[] p =
            {
                new SqlParameter("@ItemCode", (object)itemCode ?? DBNull.Value),
                new SqlParameter("@Quantity", quantity),
                new SqlParameter("@ImportDate",
                    quantity > 0 ? (object)importDate : DBNull.Value),
                new SqlParameter("@SlotId", slotId)
             };

            return sqlpr.ExecuteNonQuery(
                sqlpr.B7R2_FCCdbb,
                sql,
                p) > 0;
        }
        public void SaveSlotLots(int slotId, List<LotInfo> lots)
        {
            if (lots == null)
                lots = new List<LotInfo>();

            using (SqlConnection conn = new SqlConnection(sqlpr.B7R2_FCCdbb))
            {
                conn.Open();

                SqlTransaction tran = conn.BeginTransaction();

                try
                {
                    // Xóa toàn bộ Lot cũ của Slot
                    SqlCommand cmdDelete = new SqlCommand(
                        "DELETE FROM SlotLot WHERE SlotId=@SlotId",
                        conn,
                        tran);

                    cmdDelete.Parameters.AddWithValue("@SlotId", slotId);
                    cmdDelete.ExecuteNonQuery();

                    // Insert lại toàn bộ
                    foreach (var lot in lots)
                    {
                        SqlCommand cmdInsert = new SqlCommand(@"
                       INSERT INTO SlotLot
                        (
                            SlotId,
                            ItemCode,
                            LotNo,
                            Quantity,
                            TemCode,
                            QrData,
                            ImportDate,
                            MaPhieu
                        )
                        VALUES
                        (
                            @SlotId,
                            @ItemCode,
                            @LotNo,
                            @Quantity,
                            @TemCode,
                            @QrData,
                            @ImportDate,
                            @MaPhieu
                        )", conn, tran);

                        cmdInsert.Parameters.AddWithValue("@SlotId", slotId);
                        cmdInsert.Parameters.AddWithValue("@LotNo",
                            (object)lot.LotNo ?? DBNull.Value);



                        cmdInsert.Parameters.AddWithValue("@Quantity",
                            lot.Quantity);

                        cmdInsert.Parameters.AddWithValue("@TemCode",
                            (object)lot.TemCode ?? DBNull.Value);

                        cmdInsert.Parameters.AddWithValue("@QrData",
                            (object)lot.QRInfo?.RawQr ?? DBNull.Value);

                        cmdInsert.Parameters.AddWithValue("@MaPhieu",
                            (object)lot.QRInfo?.MaPhieu ?? DBNull.Value);
                        cmdInsert.Parameters.AddWithValue(
                            "@ItemCode",
                            (object)lot.QRInfo?.ItemCode ?? DBNull.Value);

                        cmdInsert.Parameters.AddWithValue(
                            "@ImportDate",
                            (object)lot.QRInfo?.ImportDate ?? DateTime.Now);

                        cmdInsert.ExecuteNonQuery();
                    }

                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }
            }
        }
        public void SaveSlotLots(
            int slotId,
            List<LotInfo> lots,
            bool updateSlot)
        {
            SaveSlotLots(slotId, lots);

            if (updateSlot)
                UpdateSlotQuantity(slotId);
        }
        public void ClearSlot(int slotId)
        {
            string query = @"
                DELETE FROM SlotLot
                WHERE SlotId=@SlotId;
 
                UPDATE Slot
                SET
                    ItemCode=NULL,
                    Quantity=0,
                    ImportDate=NULL,
                    IsOccupied=0
                WHERE SlotId=@SlotId;";

            SqlParameter[] parameters = new[] { new SqlParameter("@SlotId", slotId) };
            sqlpr.ExecuteScalar(sqlpr.B7R2_FCCdbb, query, parameters);
        }
        public int GetSlotID(string whName, string rackNameNew, int slotnber)
        {
            // Lấy SlotId của slot đích
            string getSlotQuery = @"
            SELECT s.SlotId
            FROM Slot s
            JOIN Rack r ON r.RackId = s.RackId
            JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
            WHERE w.Name = @whName AND r.RackName = @rackName AND s.SlotNumber = @slotNumber";
            var parameters = new[]
            {
            new SqlParameter("@whName", whName),
            new SqlParameter("@rackName", rackNameNew),
            new SqlParameter("@slotNumber", slotnber),
              };

            DataTable dt = sqlpr.LoadData1(sqlpr.B7R2_FCCdbb, getSlotQuery, parameters);
            return (int)dt.Rows[0]["SlotId"];
        }

        public int GetSlotCapacityById(int slotId)
        {
            string query = "SELECT Capacity FROM Slot WHERE SlotId = @SlotId";
            var parameters = new[] { new SqlParameter("@SlotId", slotId) };

            using (SqlConnection conn = new SqlConnection(sqlpr.B7R2_FCCdbb))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddRange(parameters);
                conn.Open();

                var result = cmd.ExecuteScalar();
                if (result != null && int.TryParse(result.ToString(), out int capacity))
                {
                    return capacity;
                }
            }

            return 0; // nếu không tìm thấy
        }

        public bool ExistsLot(
            int slotId,
            string lotNo)
        {
            string sql = @"
            SELECT COUNT(*)
            FROM SlotLot
            WHERE SlotId=@SlotId
              AND LotNo=@LotNo";

            object result =
                sqlpr.ExecuteScalar(
                    sqlpr.B7R2_FCCdbb,
                    sql,
                    new[]
                    {
                new SqlParameter("@SlotId",slotId),
                new SqlParameter("@LotNo",lotNo)
                    });

            return Convert.ToInt32(result) > 0;
        }

        public int GetSlotIDFromString(string slotText)
        {
            if (string.IsNullOrWhiteSpace(slotText))
                return -1;

            var parts = slotText.Split('-');
            if (parts.Length != 3)
                return -1;

            string wh = parts[0].Replace("WH :", "").Trim();
            string rack = parts[1].Replace("Rack :", "").Trim();
            string slotStr = parts[2].Replace("Slot :", "").Trim();

            if (!int.TryParse(slotStr, out int slotNumber))
                return -1;

            return GetSlotID(wh, rack, slotNumber);
        }

        public void UpdateSlotQuantity(int slotId)
        {
            string sql = @"
            UPDATE s
            SET
                Quantity =
                (
                    SELECT ISNULL(SUM(sl.Quantity),0)
                    FROM SlotLot sl
                    WHERE sl.SlotId = s.SlotId
                ),
 
                ItemCode =
                (
                    SELECT TOP (1) sl.ItemCode
                    FROM SlotLot sl
                    WHERE sl.SlotId = s.SlotId
                    ORDER BY sl.ImportDate DESC, sl.CreatedDate DESC
                ),
 
                ImportDate =
                (
                    SELECT TOP (1) sl.ImportDate
                    FROM SlotLot sl
                    WHERE sl.SlotId = s.SlotId
                    ORDER BY sl.ImportDate DESC, sl.CreatedDate DESC
                ),
 
                IsOccupied =
                CASE
                    WHEN EXISTS
                    (
                        SELECT 1
                        FROM SlotLot sl
                        WHERE sl.SlotId = s.SlotId
                    )
                    THEN 1
                    ELSE 0
                END
 
            FROM Slot s
            WHERE s.SlotId = @SlotId;";

            sqlpr.ExecuteNonQuery(
                sqlpr.B7R2_FCCdbb,
                sql,
                new[]
                {
            new SqlParameter("@SlotId", slotId)
                });
        }


        public List<LotInfo> GetSlotLots(int slotId)
        {
            string sql = @"
            SELECT
                ItemCode,
                LotNo,
                Quantity,
                TemCode,
                QrData,
                MaPhieu,
                ImportDate
            FROM SlotLot
            WHERE SlotId=@SlotId
            ORDER BY LotNo";

            SqlParameter[] p =
            {
            new SqlParameter("@SlotId",slotId)
            };

            DataTable dt =
                sqlpr.LoadData1(sqlpr.B7R2_FCCdbb, sql, p);

            List<LotInfo> lots = new List<LotInfo>();

            foreach (DataRow row in dt.Rows)
            {
                string lotNo = row["LotNo"] == DBNull.Value ? "" : row["LotNo"].ToString();
                int quantity = Convert.ToInt32(row["Quantity"]);
                string temCode = row["TemCode"] == DBNull.Value ? "" : row["TemCode"].ToString();
                string qrData = row["QrData"] == DBNull.Value ? "" : row["QrData"].ToString();

                // ✅ FIX CHÍNH: parse lại QRInfo TRỰC TIẾP từ QrData đã lưu (luôn là raw QR gốc
                // đầy đủ — đã xác nhận qua dữ liệu thực tế trong SlotLot), thay vì tự dựng
                // QRCodeInfo thủ công thiếu LotNo/NgaySX/SoPhieuTong/IsTongPhieu như code cũ.
                // Cách này tự động fix được luôn cả các dòng dữ liệu CŨ đã lưu trước đây, vì
                // QrData chưa bao giờ bị lưu thiếu — chỉ có bước PARSE LẠI là bị thiếu field.
                QRCodeInfo qrInfo = null;
                if (!string.IsNullOrWhiteSpace(qrData))
                {
                    try
                    {
                        qrInfo = QRCodeParser.ParseQRCode(qrData);
                    }
                    catch (FormatException)
                    {
                        qrInfo = null; // QrData cũ bị lỗi định dạng (hiếm) -> rơi xuống fallback bên dưới
                    }
                }

                if (qrInfo == null)
                {
                    // Fallback khi không có/không parse được QrData -> dựng tối thiểu từ các cột rời rạc
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
                    // Đồng bộ Quantity theo đúng cột Quantity hiện tại của SlotLot (nguồn số liệu
                    // chính thức), phòng trường hợp QrData bị lệch so với Quantity đã cập nhật.
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
        public static void ParseSlotString(string text, out string wh, out string rack, out int slot, out int capacity)
        {
            string[] parts = text.Split('-');
            wh = parts[0].Replace("WH :", "").Trim();
            rack = parts[1].Replace("Rack :", "").Trim();
            slot = int.Parse(parts[2].Replace("Slot :", "").Trim());
            capacity = int.Parse(parts[3].Replace("Capacity :", "").Trim());
        }
        public static void ClearSlotTemporarily(Slot slot)
        {
            slot.ItemCode = "";
            slot.Quantity = 0;
            slot.ImportDate = null;
            slot.IsOccupied = false;

            slot.Lots.Clear();
        }

        public static void RestoreSlot(
        Slot slot,
        Slot backup)
        {
            slot.ItemCode = backup.ItemCode;
            slot.Quantity = backup.Quantity;
            slot.ImportDate = backup.ImportDate;
            slot.IsOccupied = backup.IsOccupied;

            slot.Lots = backup.Lots
                .Select(x => new LotInfo
                {
                    LotNo = x.LotNo,
                    Quantity = x.Quantity,
                    TemCode = x.TemCode,
                    RawQr = x.RawQr,
                    QRInfo = x.QRInfo
                })
                .ToList();
        }
        public static void BackupSlot(
            Slot slot,
            out Slot backup)
        {
            backup = new Slot
            {
                SlotId = slot.SlotId,
                ItemCode = slot.ItemCode,
                Quantity = slot.Quantity,
                ImportDate = slot.ImportDate,
                IsOccupied = slot.IsOccupied,

                Lots = slot.Lots
                    .Select(x => new LotInfo
                    {
                        LotNo = x.LotNo,
                        Quantity = x.Quantity,
                        TemCode = x.TemCode,
                        RawQr = x.RawQr,
                        QRInfo = x.QRInfo
                    })
                    .ToList()
            };
        }
        public static void SaveHistory(
            string actionType,
            string itemCode,
            LotInfo lot,
            int? fromSlotId,
            int? toSlotId = null,
            string performedBy = null)
        {
            var provider = new SQLPROVIDER();

            using (var conn = new SqlConnection(provider.B7R2_FCCdbb))
            {
                conn.Open();

                using (var cmd = new SqlCommand(@"
                INSERT INTO StockHistory
                (
                    ActionType,
                    ItemCode,
                    TemCode,
                    LotNo,
                    Quantity,
                    Date,
                    FromSlotId,
                    ToSlotId,
                    QrData,
                    MaPhieu,
                    PerformedBy
                )
                VALUES
                (
                    @ActionType,
                    @ItemCode,
                    @TemCode,
                    @LotNo,
                    @Quantity,
                    GETDATE(),
                    @FromSlotId,
                    @ToSlotId,
                    @QrData,
                    @MaPhieu,
                    @PerformedBy
                )", conn))
                {
                    cmd.Parameters.Add("@ActionType", SqlDbType.NVarChar, 20)
                        .Value = actionType;

                    cmd.Parameters.Add("@ItemCode", SqlDbType.NVarChar, 50)
                        .Value = (object)itemCode ?? DBNull.Value;

                    cmd.Parameters.Add("@TemCode", SqlDbType.NVarChar)
                        .Value = (object)lot?.TemCode ?? DBNull.Value;

                    cmd.Parameters.Add("@LotNo", SqlDbType.NVarChar)
                        .Value = (object)lot?.LotNo ?? DBNull.Value;

                    cmd.Parameters.Add("@Quantity", SqlDbType.Int)
                        .Value = lot?.Quantity ?? 0;

                    cmd.Parameters.Add("@FromSlotId", SqlDbType.Int)
                        .Value = (object)fromSlotId ?? DBNull.Value;

                    cmd.Parameters.Add("@ToSlotId", SqlDbType.Int)
                        .Value = (object)toSlotId ?? DBNull.Value;

                    cmd.Parameters.Add("@QrData", SqlDbType.NVarChar)
                        .Value = (object)lot?.QRInfo?.RawQr ?? DBNull.Value;

                    cmd.Parameters.Add("@MaPhieu", SqlDbType.NVarChar, 100)
                        .Value = (object)lot?.QRInfo?.MaPhieu ?? DBNull.Value;

                    cmd.Parameters.Add("@PerformedBy", SqlDbType.NVarChar, 50)
                    .Value = !string.IsNullOrEmpty(performedBy)
                    ? (object)performedBy
                    : Environment.UserName;

                    cmd.ExecuteNonQuery();
                }
            }
        }

    }

}
