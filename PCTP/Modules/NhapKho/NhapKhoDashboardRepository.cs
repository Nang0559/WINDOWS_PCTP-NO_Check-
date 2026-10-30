using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;
using PCTP.Shared.Models;

using System.Data;
using System.Data.SqlClient;


namespace PCTP.Modules.NhapKho
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

        private string LotKeyStockTpT => string.Format(NORMALIZE_LOT_EXPR, "t.LOT");

        private string LotKeyPending => string.Format(NORMALIZE_LOT_EXPR, "h.LotGoc");

        private string BuildSqlLech(string selectClause)
        {
            return $@"
            WITH Stock AS
            (
                SELECT {LotKeyStockTp} AS LotKey,
                       MAX(s.LOT) AS LotNo,
                       MAX(s.PART) AS ItemCode,
                       MAX(s.NAME) AS ItemName,
                       SUM(ISNULL(s.SLCONLAI,0)) AS Qty
                FROM STOCKTP s
                GROUP BY {LotKeyStockTp}
            ),
            ActiveSlot AS
            (
                SELECT {LotKeySlotLot} AS LotKey,
                       MAX(sl.LotNo) AS LotNo,
                       MAX(sl.ItemCode) AS ItemCode,
                       SUM(ISNULL(sl.Quantity,0)) AS Qty
                FROM SlotLot sl
                WHERE sl.PhieuStatus = 0
                GROUP BY {LotKeySlotLot}
            ),
            Pending AS
            (
                -- Hàng đã pick nhưng chưa xác nhận giao vẫn thuộc tồn STOCKTP theo quy tắc nghiệp vụ.
                SELECT {LotKeyPending} AS LotKey,
                       MAX(h.LotGoc) AS LotNo,
                       MAX(h.MaHang) AS ItemCode,
                       SUM(ISNULL(h.SoLuong,0)) AS Qty
                FROM FVN_HangChoGiao h
                WHERE h.TrangThai = 'ChoGiao'
                  AND ISNULL(h.SoLuong,0) > 0
                  AND NULLIF(LTRIM(RTRIM(h.LotGoc)), '') IS NOT NULL
                GROUP BY {LotKeyPending}
            ),
            AllLotKeys AS
            (
                SELECT LotKey FROM Stock
                UNION
                SELECT LotKey FROM ActiveSlot
                UNION
                SELECT LotKey FROM Pending
            )
            {selectClause}
            FROM AllLotKeys k
            LEFT JOIN Stock s ON s.LotKey = k.LotKey
            LEFT JOIN ActiveSlot a ON a.LotKey = k.LotKey
            LEFT JOIN Pending p ON p.LotKey = k.LotKey
            WHERE ISNULL(s.Qty,0) <> ISNULL(a.Qty,0) + ISNULL(p.Qty,0)";
        }

        private string BuildSqlLech() => BuildSqlLech(@"
            SELECT
                COALESCE(s.LotNo, a.LotNo, p.LotNo) AS LOT,
                COALESCE(s.ItemCode, a.ItemCode, p.ItemCode) AS PART,
                s.ItemName AS NAME,
                ISNULL(s.Qty,0) AS SL_STOCKTP,
                ISNULL(a.Qty,0) + ISNULL(p.Qty,0) AS SL_SLOT,
                ISNULL(s.Qty,0) - (ISNULL(a.Qty,0) + ISNULL(p.Qty,0)) AS ChenhLech");

        public int DemLechDoiChieu()
        {
            // BuildSqlLech() bắt đầu bằng WITH (CTE). Không được nhúng CTE vào
            // derived table "FROM ( ... )" vì SQL Server yêu cầu WITH ở cấp statement.
            // Dùng cùng CTE nhưng đổi projection thành COUNT(*) để giữ đúng semantics.
            object kq = ExecuteScalar(BuildSqlLech("SELECT COUNT(*)"));
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public DataTable GetGridLechDoiChieu()
            => LoadData(BuildSqlLech());

        // ── các method khác giữ nguyên như bản trước ──

        public int DemPhieuChoNhap()
        {
            object kq = ExecuteScalar(
                "SELECT COUNT(*) FROM vNhapTP" + ReceivingWorklistFilter.WhereClause);
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public DataTable GetGridChoNhap() => LoadData(@"
            SELECT FIND, LOT_NO, MODEL, TEN_SAN_PHAM, MA_SAN_PHAM, CA_SAN_XUAT,
                   NGAY_SAN_XUAT, SL_DA_SAN_XUAT, SL_DA_NHAP,
                   (SL_DA_SAN_XUAT - SL_DA_NHAP) AS SL_CON_THIEU
            FROM vNhapTP" + ReceivingWorklistFilter.WhereClause + @"
            ORDER BY NGAY_SAN_XUAT DESC");

        public int DemDaNhapHomNay()
        {
            object kq = ExecuteScalar(
                "SELECT COUNT(*) FROM SlotLot WHERE PhieuStatus = 0 AND CAST(CreatedDate AS DATE) = CAST(GETDATE() AS DATE)");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public DataTable GetGridDaNhapHomNay() => LoadData($@"
            SELECT sl.LotNo,
                   COALESCE(NULLIF(RTRIM(it.Model), ''), NULLIF(RTRIM(st.MODEL), ''), '') AS Model,
                   sl.ItemCode,
                   COALESCE(NULLIF(RTRIM(it.Name), ''), RTRIM(st.NAME), '') AS TenSP,
                   st.CASX AS CaSX,
                   st.NGAYSX AS NgaySX,
                   sl.Quantity, sl.TemCode, sl.CreatedDate,
                   s.SlotNumber, r.RackName, w.Name AS WarehouseName
            FROM SlotLot sl
            JOIN Slot s ON s.SlotId = sl.SlotId
            JOIN Rack r ON r.RackId = s.RackId
            JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
            -- OUTER APPLY TOP 1: mỗi SlotLot đúng 1 dòng (không nhân dòng khi STOCKTP/vB20Item có bản ghi trùng khoá).
            OUTER APPLY (SELECT TOP (1) t.MODEL, t.NAME, t.CASX, t.NGAYSX
                         FROM STOCKTP t
                         WHERE {LotKeyStockTpT} = {LotKeySlotLot}
                         ORDER BY CASE WHEN t.LOT = sl.LotNo THEN 0 ELSE 1 END) st
            OUTER APPLY (SELECT TOP (1) i.Model, i.Name
                         FROM dbo.vB20Item i
                         WHERE i.Code = sl.ItemCode
                         ORDER BY ISNULL(i.IsActive, 0) DESC, i.Id DESC) it
            WHERE sl.PhieuStatus = 0 AND CAST(sl.CreatedDate AS DATE) = CAST(GETDATE() AS DATE)
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
