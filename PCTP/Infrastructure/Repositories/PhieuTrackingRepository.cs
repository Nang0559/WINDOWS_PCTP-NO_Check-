using PCTP.ClassSQL;
using PCTP.Models;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    public class PhieuTrackingRepository : IPhieuTrackingRepository
    {
        private readonly SQLPROVIDER _sql;
        public PhieuTrackingRepository(SQLPROVIDER sql) => _sql = sql;

        public List<PhieuLocationInfo> GetPhieuTheoLot(string lotNo)
        {
            string sql = @"
                SELECT sl.SlotId, sl.LotNo, sl.Quantity, sl.MaPhieu,
                       sl.ParentSoPhieu, sl.SoPhieuTong, sl.PhieuStatus,
                       sl.ImportDate,
                       s.SlotNumber, r.RackName, w.Name AS WarehouseName
                FROM SlotLot sl
                JOIN Slot s ON s.SlotId = sl.SlotId
                JOIN Rack r ON r.RackId = s.RackId
                JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
                WHERE sl.LotNo = @LotNo
                ORDER BY sl.PhieuStatus, sl.ImportDate";

            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdbb, sql,
                new SqlParameter("@LotNo", lotNo));

            var list = new List<PhieuLocationInfo>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new PhieuLocationInfo
                {
                    LotNo = r["LotNo"] as string,
                    MaPhieu = r["MaPhieu"] as string,
                    ParentSoPhieu = r["ParentSoPhieu"] as string,
                    SoPhieuTong = r["SoPhieuTong"] as string,
                    Status = (PhieuStatus)Convert.ToByte(r["PhieuStatus"] == DBNull.Value ? 0 : r["PhieuStatus"]),
                    Quantity = Convert.ToInt32(r["Quantity"]),
                    SlotId = Convert.ToInt32(r["SlotId"]),
                    SlotNumber = Convert.ToInt32(r["SlotNumber"]),
                    RackName = r["RackName"] as string,
                    WarehouseName = r["WarehouseName"] as string,
                    ImportDate = r["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ImportDate"])
                });
            }
            return list;
        }

        public int GetTongSlActiveTheoLot(string lotNo)
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdbb,
                "SELECT ISNULL(SUM(Quantity),0) FROM SlotLot WHERE LotNo = @LotNo AND PhieuStatus = 0",
                new[] { new SqlParameter("@LotNo", lotNo) });
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public bool ExistsQrData(string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData)) return false;
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdbb,
                "SELECT COUNT(*) FROM SlotLot WHERE QrData = @Qr",
                new[] { new SqlParameter("@Qr", qrData) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public void InsertPhieuMoi(SqlConnection conn, SqlTransaction tran,
            int slotId, string itemCode, string lotNo, int quantity,
            string temCode, string qrData, DateTime? importDate,
            string ngaySX, string soPhieuTong, string maPhieuMoi,
            string parentSoPhieu, PhieuStatus status)
        {
            const string sql = @"
                INSERT INTO SlotLot
                    (SlotId, ItemCode, LotNo, Quantity, TemCode, QrData,
                     ImportDate, CreatedDate, NgaySX, SoPhieuTong,
                     MaPhieu, ParentSoPhieu, PhieuStatus)
                VALUES
                    (@SlotId, @ItemCode, @LotNo, @Quantity, @TemCode, @QrData,
                     @ImportDate, GETDATE(), @NgaySX, @SoPhieuTong,
                     @MaPhieu, @ParentSoPhieu, @PhieuStatus)";

            _sql.ExecuteNonQuery(conn, tran, sql,
                new SqlParameter("@SlotId", slotId),
                new SqlParameter("@ItemCode", (object)itemCode ?? DBNull.Value),
                new SqlParameter("@LotNo", (object)lotNo ?? DBNull.Value),
                new SqlParameter("@Quantity", quantity),
                new SqlParameter("@TemCode", (object)temCode ?? DBNull.Value),
                new SqlParameter("@QrData", (object)qrData ?? DBNull.Value),
                new SqlParameter("@ImportDate", (object)importDate ?? DateTime.Now),
                new SqlParameter("@NgaySX", (object)ngaySX ?? DBNull.Value),
                new SqlParameter("@SoPhieuTong", (object)soPhieuTong ?? DBNull.Value),
                new SqlParameter("@MaPhieu", (object)maPhieuMoi ?? DBNull.Value),
                new SqlParameter("@ParentSoPhieu", (object)parentSoPhieu ?? DBNull.Value),
                new SqlParameter("@PhieuStatus", (byte)status));
        }

        public void CapNhatTrangThai(SqlConnection conn, SqlTransaction tran,
            string maPhieu, PhieuStatus status)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "UPDATE SlotLot SET PhieuStatus = @Status WHERE MaPhieu = @MaPhieu",
                new SqlParameter("@Status", (byte)status),
                new SqlParameter("@MaPhieu", maPhieu));
        }

        public void CapNhatQuantity(SqlConnection conn, SqlTransaction tran,
            string maPhieu, int quantityMoi)
        {
            _sql.ExecuteNonQuery(conn, tran,
                "UPDATE SlotLot SET Quantity = @Qty WHERE MaPhieu = @MaPhieu",
                new SqlParameter("@Qty", quantityMoi),
                new SqlParameter("@MaPhieu", maPhieu));
        }
    }
}
