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
    public class GiaoBuNGRepository : IGiaoBuNGRepository
    {
        private readonly SQLPROVIDER _sql;
        public GiaoBuNGRepository(SQLPROVIDER sql) => _sql = sql;
        private static string Esc(string s) => (s ?? "").Replace("'", "''");

        public List<PhieuGiaoGocInfo> TimPhieuGocTheoLot(string lot)
        {
            var result = new List<PhieuGiaoGocInfo>();
            if (string.IsNullOrWhiteSpace(lot)) return result;

            string esc = Esc(lot);
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, $@"
            SELECT * FROM vWDinhDanhPhieuGiao
            WHERE LOT = '{esc}' OR LOT LIKE '{esc}-%' OR LOT LIKE '%,{esc}-%'
            ORDER BY NGAYGIAO DESC");

            return MapPhieuGoc(dt);
        }

        public List<PhieuGiaoGocInfo> TimPhieuGocTheoMaHangNgay(string maHang, DateTime tu, DateTime den)
        {
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb, @"
            SELECT * FROM vWDinhDanhPhieuGiao
            WHERE MAHANG = @maHang AND CAST(NGAYGIAO AS DATE) BETWEEN @tu AND @den
            ORDER BY NGAYGIAO DESC",
                new SqlParameter("@maHang", maHang),
                new SqlParameter("@tu", tu.Date),
                new SqlParameter("@den", den.Date));

            return MapPhieuGoc(dt);
        }

        private List<PhieuGiaoGocInfo> MapPhieuGoc(DataTable dt)
        {
            var result = new List<PhieuGiaoGocInfo>();
            if (dt == null) return result;
            foreach (DataRow r in dt.Rows)
                result.Add(new PhieuGiaoGocInfo
                {
                    Stt = r["STT"] == DBNull.Value ? 0 : Convert.ToInt32(r["STT"]),
                    Lot = r["LOT"]?.ToString(),
                    MaHang = r["MAHANG"]?.ToString(),
                    TenHang = r["TENHANG"]?.ToString(),
                    SoLuong = r["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(r["SOLUONG"]),
                    NgayGiao = r["NGAYGIAO"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NGAYGIAO"]),
                    GioGiao = r["GIOGIAO"]?.ToString(),
                    NhaMay = r["NHAMAY"]?.ToString(),
                    Cua = r["CUA"]?.ToString(),
                    Truyen = r["TRUYEN"]?.ToString(),
                    PoNo = r["PO_NO"]?.ToString(),
                    Note = r["Note"]?.ToString(),
                    DinhDanhKey = r["DinhDanhKey"]?.ToString()
                });
            return result;
        }

        // ── Tra cứu LOT rework đã nhập kho thật (STOCKTP) ──────────────────
        public StockItem TraCuuLotDaNhapKho(string lot)
        {
            DataTable dt = _sql.LoadData1(_sql.B7R2_FCCdb,
                "SELECT * FROM STOCKTP WHERE LOT = @lot", new SqlParameter("@lot", lot));
            if (dt == null || dt.Rows.Count == 0) return null;

            DataRow r = dt.Rows[0];
            return new StockItem
            {
                Lot = r["LOT"] as string,
                Part = r["PART"] as string,
                Name = r["NAME"] as string,
                SlNhap = r["SLNHAP"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SLNHAP"]),
                SlConLai = r["SLCONLAI"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SLCONLAI"]),
                SlXuat = r["SLXUAT"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["SLXUAT"])
            };
        }

        // ── Tái dùng đúng SQL của TraHangRepository.GetSlotsChuaLot ────────
        public List<SlotChuaLotInfo> GetSlotsChuaLot(string lot)
        {
            var result = new List<SlotChuaLotInfo>();
            if (string.IsNullOrWhiteSpace(lot)) return result;

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdbb, @"
            SELECT sl.SlotId, sl.Quantity, sl.TemCode, sl.ImportDate,
                   s.SlotNumber, r.RackName, w.Name AS WarehouseName
            FROM SlotLot sl
            JOIN Slot s      ON s.SlotId      = sl.SlotId
            JOIN Rack r      ON r.RackId      = s.RackId
            JOIN Warehouse w ON w.WarehouseId = r.WarehouseId
            WHERE sl.LotNo = @lot
            ORDER BY sl.Quantity DESC",
                new List<SqlParameter> { new SqlParameter("@lot", lot) });

            foreach (DataRow r in dt.Rows)
                result.Add(new SlotChuaLotInfo
                {
                    SlotId = Convert.ToInt32(r["SlotId"]),
                    Quantity = Convert.ToInt32(r["Quantity"]),
                    TemCode = r["TemCode"]?.ToString(),
                    ImportDate = r["ImportDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ImportDate"]),
                    WarehouseName = r["WarehouseName"]?.ToString(),
                    RackName = r["RackName"]?.ToString(),
                    SlotNumber = Convert.ToInt32(r["SlotNumber"])
                });
            return result;
        }

        public void InsertLuuPhieuGiaoBu(SqlConnection conn, SqlTransaction tran,
            PhieuGiaoGocInfo phieuGoc, string lotFccGop, int tongSlFcc, string nguoiThucHien)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
            INSERT INTO LUUPHIEUGIAOHANG
                (CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                 NGAYGIAO, GIOGIAO, STATUS, NHAMAY, GIOGIAOFCC,
                 PO_NO, TTPHIEU, Note, PhieuGocKey)
            VALUES
                (@cua, @truyen, @maHang, @tenHang, @lot, 'PCS', @sl,
                 CAST(GETDATE() AS SMALLDATETIME), CONVERT(VARCHAR(8), GETDATE(), 108),
                 'OK', @nhaMay, CONVERT(VARCHAR(8), GETDATE(), 108),
                 @poNo, @nguoiThucHien, 'GIAO_BU_NG', @phieuGocKey)",
                new SqlParameter("@cua", phieuGoc.Cua ?? ""),
                new SqlParameter("@truyen", phieuGoc.Truyen ?? ""),
                new SqlParameter("@maHang", phieuGoc.MaHang),
                new SqlParameter("@tenHang", phieuGoc.TenHang ?? ""),
                new SqlParameter("@lot", lotFccGop),
                new SqlParameter("@sl", tongSlFcc),
                new SqlParameter("@nhaMay", phieuGoc.NhaMay ?? ""),
                new SqlParameter("@poNo", phieuGoc.PoNo ?? ""),
                new SqlParameter("@nguoiThucHien", nguoiThucHien ?? Environment.UserName),
                new SqlParameter("@phieuGocKey", phieuGoc.DinhDanhKey ?? ""));
        }

        public void InsertLuuDocQRCodeGiaoBu(SqlConnection conn, SqlTransaction tran,
            string lotFcc, string maHangFcc, int slTemFcc, string nhaMay, string phieuGocKey)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
            INSERT INTO LUUDOCQRCODE
                (LOTFCC, MAHANGFCC, SLTEMFCC,
                 LOTHVN, MAHANGHVN, SLTEMHVN,
                 STATUS, MAFCC, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY)
            VALUES
                (@lotFcc, @maHangFcc, @slTemFcc,
                 NULL, NULL, NULL,
                 1, @maHangFcc, 'GIAO_BU_NG', CAST(GETDATE() AS SMALLDATETIME),
                 CONVERT(VARCHAR(8), GETDATE(), 108), @nhaMay)",
                new SqlParameter("@lotFcc", lotFcc),
                new SqlParameter("@maHangFcc", maHangFcc),
                new SqlParameter("@slTemFcc", slTemFcc),
                new SqlParameter("@nhaMay", nhaMay ?? ""));
        }

        // ── CHỈ xuất — KHÔNG đụng SLSX, giữ đúng nguyên tắc điểm 2 ──────────
        public void XuatKhoGiaoBu(SqlConnection conn, SqlTransaction tran, string lot, int soLuong)
        {
            _sql.ExecuteNonQuery(conn, tran, @"
            UPDATE STOCKTP SET
                SLXUAT   = ISNULL(SLXUAT,0)   + @sl,
                SLCONLAI = ISNULL(SLCONLAI,0) - @sl,
                NGAYXUAT = CAST(GETDATE() AS SMALLDATETIME)
            WHERE LOT = @lot",
                new SqlParameter("@sl", soLuong),
                new SqlParameter("@lot", lot));
        }
    }
}
