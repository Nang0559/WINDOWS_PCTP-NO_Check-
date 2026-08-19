using PCTP.ClassSQL;
using PCTP.Models;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
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
    public sealed class PhieuTrackingRepository
    : SqlRepositoryBase,
      IPhieuTrackingRepository
    {
        public PhieuTrackingRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // ĐỌC - PHIẾU THEO LOT
        // ============================================================

        public List<PhieuLocationInfo> GetPhieuTheoLot(
            string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return new List<PhieuLocationInfo>();

            const string sql = @"
SELECT
    sl.SlotId,
    sl.LotNo,
    sl.Quantity,
    sl.MaPhieu,
    sl.ParentSoPhieu,
    sl.SoPhieuTong,
    sl.PhieuStatus,
    sl.ImportDate,
    s.SlotNumber,
    r.RackName,
    w.Name AS WarehouseName
FROM SlotLot sl
INNER JOIN Slot s
    ON s.SlotId = sl.SlotId
INNER JOIN Rack r
    ON r.RackId = s.RackId
INNER JOIN Warehouse w
    ON w.WarehouseId = r.WarehouseId
WHERE sl.LotNo = @LotNo
ORDER BY
    sl.PhieuStatus,
    sl.ImportDate;";

            DataTable dt = LoadData(
                sql,
                new SqlParameter(
                    "@LotNo",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = lotNo.Trim()
                });

            var list = new List<PhieuLocationInfo>();

            if (dt == null || dt.Rows.Count == 0)
                return list;

            foreach (DataRow r in dt.Rows)
            {
                list.Add(new PhieuLocationInfo
                {
                    LotNo = r["LotNo"] as string,

                    MaPhieu = r["MaPhieu"] as string,

                    ParentSoPhieu =
                        r["ParentSoPhieu"] as string,

                    SoPhieuTong =
                        r["SoPhieuTong"] as string,

                    Status =
                        (PhieuStatus)DbValueHelper.ToInt(
                            r["PhieuStatus"]),

                    Quantity =
                        DbValueHelper.ToInt(
                            r["Quantity"]),

                    SlotId =
                        DbValueHelper.ToInt(
                            r["SlotId"]),

                    SlotNumber =
                        DbValueHelper.ToInt(
                            r["SlotNumber"]),

                    RackName =
                        r["RackName"] as string,

                    WarehouseName =
                        r["WarehouseName"] as string,

                    ImportDate =
                        r["ImportDate"] == DBNull.Value
                            ? (DateTime?)null
                            : Convert.ToDateTime(
                                r["ImportDate"])
                });
            }

            return list;
        }

        // ============================================================
        // TỔNG SỐ LƯỢNG ACTIVE THEO LOT
        // ============================================================

        public int GetTongSlActiveTheoLot(
            string lotNo)
        {
            if (string.IsNullOrWhiteSpace(lotNo))
                return 0;

            const string sql = @"
SELECT ISNULL(SUM(Quantity), 0)
FROM SlotLot
WHERE LotNo = @LotNo
  AND PhieuStatus = 0;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter(
                    "@LotNo",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = lotNo.Trim()
                });

            return DbValueHelper.ToInt(result);
        }

        // ============================================================
        // KIỂM TRA QR ĐÃ TỒN TẠI
        // ============================================================

        public bool ExistsQrData(
            string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData))
                return false;

            const string sql = @"
SELECT COUNT(*)
FROM SlotLot
WHERE QrData = @QrData;";

            object result = ExecuteScalar(
                sql,
                new SqlParameter(
                    "@QrData",
                    SqlDbType.NVarChar,
                    -1)
                {
                    Value = qrData.Trim()
                });

            return DbValueHelper.ToInt(result) > 0;
        }

        // ============================================================
        // INSERT PHIẾU
        // ============================================================

        public void InsertPhieuMoi(
            int slotId,
            string itemCode,
            string lotNo,
            int quantity,
            string temCode,
            string qrData,
            DateTime? importDate,
            string ngaySX,
            string soPhieuTong,
            string maPhieuMoi,
            string parentSoPhieu,
            PhieuStatus status)
        {
            if (slotId <= 0)
                throw new ArgumentException(
                    "SlotId không hợp lệ.",
                    nameof(slotId));

            if (quantity <= 0)
                throw new ArgumentException(
                    "Quantity phải lớn hơn 0.",
                    nameof(quantity));

            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException(
                    "LOT không được rỗng.",
                    nameof(lotNo));

            const string sql = @"
INSERT INTO SlotLot
(
    SlotId,
    ItemCode,
    LotNo,
    Quantity,
    TemCode,
    QrData,
    ImportDate,
    CreatedDate,
    NgaySX,
    SoPhieuTong,
    MaPhieu,
    ParentSoPhieu,
    PhieuStatus
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
    GETDATE(),
    @NgaySX,
    @SoPhieuTong,
    @MaPhieu,
    @ParentSoPhieu,
    @PhieuStatus
);";

            ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@SlotId",
                    SqlDbType.Int)
                {
                    Value = slotId
                },

                new SqlParameter(
                    "@ItemCode",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)itemCode ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@LotNo",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)lotNo.Trim() ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@Quantity",
                    SqlDbType.Int)
                {
                    Value = quantity
                },

                new SqlParameter(
                    "@TemCode",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)temCode ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@QrData",
                    SqlDbType.NVarChar,
                    -1)
                {
                    Value =
                        (object)qrData ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@ImportDate",
                    SqlDbType.DateTime)
                {
                    Value =
                        (object)importDate ??
                        DateTime.Now
                },

                new SqlParameter(
                    "@NgaySX",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)ngaySX ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@SoPhieuTong",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)soPhieuTong ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@MaPhieu",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)maPhieuMoi ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@ParentSoPhieu",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value =
                        (object)parentSoPhieu ??
                        DBNull.Value
                },

                new SqlParameter(
                    "@PhieuStatus",
                    SqlDbType.TinyInt)
                {
                    Value = (byte)status
                });
        }

        // ============================================================
        // CẬP NHẬT TRẠNG THÁI
        // ============================================================

        public void CapNhatTrangThai(
            string maPhieu,
            PhieuStatus status)
        {
            if (string.IsNullOrWhiteSpace(maPhieu))
                throw new ArgumentException(
                    "Mã phiếu không được rỗng.",
                    nameof(maPhieu));

            const string sql = @"
UPDATE SlotLot
SET PhieuStatus = @Status
WHERE MaPhieu = @MaPhieu;";

            int affected = ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Status",
                    SqlDbType.TinyInt)
                {
                    Value = (byte)status
                },

                new SqlParameter(
                    "@MaPhieu",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = maPhieu.Trim()
                });

            if (affected == 0)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu [{maPhieu}].");
            }
        }

        // ============================================================
        // CẬP NHẬT QUANTITY
        // ============================================================

        public void CapNhatQuantity(
            string maPhieu,
            int quantityMoi)
        {
            if (string.IsNullOrWhiteSpace(maPhieu))
                throw new ArgumentException(
                    "Mã phiếu không được rỗng.",
                    nameof(maPhieu));

            if (quantityMoi < 0)
                throw new ArgumentException(
                    "Quantity không được âm.",
                    nameof(quantityMoi));

            const string sql = @"
UPDATE SlotLot
SET Quantity = @Quantity
WHERE MaPhieu = @MaPhieu;";

            int affected = ExecuteNonQuery(
                sql,

                new SqlParameter(
                    "@Quantity",
                    SqlDbType.Int)
                {
                    Value = quantityMoi
                },

                new SqlParameter(
                    "@MaPhieu",
                    SqlDbType.NVarChar,
                    100)
                {
                    Value = maPhieu.Trim()
                });

            if (affected == 0)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy phiếu [{maPhieu}].");
            }
        }
    }
}
