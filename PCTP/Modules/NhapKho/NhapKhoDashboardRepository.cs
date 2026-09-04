using PCTP.ClassSQL;
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
    public sealed class NhapKhoDashboardRepository
         : SqlRepositoryBase,
           INhapKhoDashboardRepository
    {
        public NhapKhoDashboardRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ── Chuẩn hoá khoá LOT ngay trong SQL — tương đương LotCodeHelper.TrimTo:
        // nếu LOT đủ 20 ký tự (LEN_HEAD_FIXED) thì lấy 20, ngắn hơn thì lấy tối đa 13
        // (LEN_LEGACY_KEY) — tự chuẩn hoá từng phía độc lập rồi so bằng nhau.
        // Đây là bản rút gọn phù hợp cho JOIN tập hợp (aggregate), không hoàn toàn
        // giống AreLotKeysEquivalent (vốn so theo CẶP cụ thể), nhưng đủ dùng cho
        // dashboard cảnh báo — không dùng kết quả này để ghi/trừ kho.
        private const string NORMALIZE_LOT_EXPR =
            "CASE WHEN LEN({0}) >= 20 THEN LEFT({0},20) ELSE LEFT({0},13) END";

        private string LotKeyStockTp => string.Format(NORMALIZE_LOT_EXPR, "s.LOT");
        private string LotKeySlotLot => string.Format(NORMALIZE_LOT_EXPR, "sl.LotNo");

        private string BuildSqlLech() => $@"
            SELECT
                s.LOT, s.PART, s.NAME,
                ISNULL(s.SLCONLAI,0) AS SL_STOCKTP,
                ISNULL(x.TongActive,0) AS SL_SLOT,
                (ISNULL(s.SLCONLAI,0) - ISNULL(x.TongActive,0)) AS ChenhLech
            FROM STOCKTP s
            LEFT JOIN (
                SELECT {LotKeySlotLot} AS LotKey, SUM(sl.Quantity) AS TongActive
                FROM SlotLot sl
                WHERE sl.PhieuStatus = 0
                GROUP BY {LotKeySlotLot}
            ) x ON {LotKeyStockTp} = x.LotKey
            WHERE ISNULL(s.SLCONLAI,0) > 0
              AND ISNULL(s.SLCONLAI,0) <> ISNULL(x.TongActive,0)";

        public int DemLechDoiChieu()
        {
            object kq = ExecuteScalar($"SELECT COUNT(*) FROM ({BuildSqlLech()}) t");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public DataTable GetGridLechDoiChieu()
            => LoadData(BuildSqlLech());

        // ── các method khác giữ nguyên như bản trước ──

        public int DemPhieuChoNhap()
        {
            object kq = ExecuteScalar(@"
                SELECT COUNT(*) FROM vNhapTP
                WHERE ISNULL(KET_THUC_LOT,0)=0
                  AND ISNULL(SL_DA_NHAP,0) < ISNULL(SL_DA_SAN_XUAT,0)");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public DataTable GetGridChoNhap() => LoadData(@"
            SELECT FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM, CA_SAN_XUAT,
                   NGAY_SAN_XUAT, SL_DA_SAN_XUAT, SL_DA_NHAP,
                   (SL_DA_SAN_XUAT - SL_DA_NHAP) AS SL_CON_THIEU
            FROM vNhapTP
            WHERE ISNULL(KET_THUC_LOT,0) = 0
              AND ISNULL(SL_DA_NHAP,0) < ISNULL(SL_DA_SAN_XUAT,0)
            ORDER BY NGAY_SAN_XUAT DESC");

        public int DemDaNhapHomNay()
        {
            object kq = ExecuteScalar(
                "SELECT COUNT(*) FROM SlotLot WHERE CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public DataTable GetGridDaNhapHomNay() => LoadData(@"
            SELECT sl.LotNo, sl.ItemCode, sl.Quantity, sl.TemCode, sl.CreatedDate,
                   s.SlotNumber, r.RackName, w.Name AS WarehouseName
            FROM SlotLot sl
            JOIN Slot s ON s.SlotId = sl.SlotId
            JOIN Rack r ON r.RackId = s.RackId
            JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
            WHERE CAST(sl.CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
            ORDER BY sl.CreatedDate DESC");

        public int GetTongTonStockTp()
        {
            object kq = ExecuteScalar(
                "SELECT ISNULL(SUM(SLCONLAI),0) FROM STOCKTP WHERE ISNULL(SLCONLAI,0) > 0");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public int GetTongTonRackThat()
        {
            object kq = ExecuteScalar(@"
        SELECT ISNULL(SUM(sl.Quantity),0) FROM SlotLot sl
        JOIN Slot s ON s.SlotId = sl.SlotId
        JOIN Rack r ON r.RackId = s.RackId
        JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
        WHERE sl.PhieuStatus = 0 AND w.Name <> @BulkWarehouseName",
                new SqlParameter("@BulkWarehouseName", BulkImportConfig.WarehouseName));
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public int GetTongTonKhoTam()
        {
            object kq = ExecuteScalar(@"
        SELECT ISNULL(SUM(sl.Quantity),0) FROM SlotLot sl
        JOIN Slot s ON s.SlotId = sl.SlotId
        JOIN Rack r ON r.RackId = s.RackId
        JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
        WHERE sl.PhieuStatus = 0 AND w.Name = @BulkWarehouseName",
                new SqlParameter("@BulkWarehouseName", BulkImportConfig.WarehouseName));
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }
    }
}
