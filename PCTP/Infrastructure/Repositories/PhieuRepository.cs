using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Services;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Infrastructure.Repositories
{

    /// <summary>
    /// Toàn bộ SQL liên quan đến phiếu tập trung tại đây.
    /// Form và Service KHÔNG viết SQL nữa.
    /// </summary>
    public class PhieuRepository : IPhieuRepository
    {
        private readonly SQLPROVIDER _sql;
        private readonly CustomerConfig _cfg;
        public PhieuRepository(SQLPROVIDER sql, CustomerConfig cfg) { _sql = sql; _cfg = cfg; }

        // ════════════════════════════════════════════════════════════════════════
        // Đếm / kiểm tra
        // ════════════════════════════════════════════════════════════════════════
        public int CountDocQRCode(string docQRTable)
        {
            ValidateTenBan(docQRTable);
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{docQRTable}]");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public bool CheckCoMaNG(string tenBan)
        {
            string kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT dbo.ufn_QRcode_ADD_CMD_MANG()")?.ToString() ?? "0";
            return int.TryParse(kq, out int v) && (v == 1 || v == 2);
        }

        public bool KiemTraMaTrongPhieu(string maHang, string tenBan)
        {
            ValidateTenBan(tenBan);
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{tenBan}] WHERE MAHANG = @ma",
                new SqlParameter[] { new SqlParameter("@ma", maHang) });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Load phiếu khi đang bắn dở (isBanQR = true)
        // ════════════════════════════════════════════════════════════════════════
        public DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay,
                                         string gioFcc, int addNm,
                                         string tmpTable, string ifsTable,
                                         string docQRTable)
        {
            ValidateTenBan(tmpTable);
            ValidateTenBan(ifsTable);
            ValidateTenBan(docQRTable);

            // Đọc lại context từ IFS nếu có
            DataTable tt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAOFCC, NHAMAY FROM [{ifsTable}]");
            if (tt.Rows.Count > 0)
            {
                ngayGiao = tt.Rows[0]["NGAYGIAO"].ToString();
                nhaMay = tt.Rows[0]["NHAMAY"].ToString();
                gioFcc = tt.Rows[0]["GIOGIAOFCC"].ToString();
                addNm = SafeInt(tt.Rows[0]["ADDNM"]);
            }

            return CallSP("Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                          ngayGiao, nhaMay, gioFcc, addNm,
                          tmpTable: tmpTable,
                          ifsTable: ifsTable,
                          docQRTable: docQRTable);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Drop → Create → BulkInsert → CallSP
        // ════════════════════════════════════════════════════════════════════════
        // ════════════════════════════════════════════════════════════════════════
        // FIX 1: PhieuRepository.LuuVaLoad
        // Thêm guard: không DELETE TMPPHIEUGIAOHANG khi DOCQRCODE đang có data
        // ════════════════════════════════════════════════════════════════════════
        public DataTable LuuVaLoad(string tenSPBang, string tenSP, DataTable donHang,
                                    string ngayGiao, string nhaMay,
                                    string gioFcc, int addNm,
                                    string tenBan, string docQRTable,
                                    string ifsView = "")
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(tenSPBang);
            ValidateTenBan(docQRTable);
            if (!string.IsNullOrEmpty(ifsView))
                ValidateTenBan(ifsView);

            // ── 4a. Convert DateTime columns → string ────────────────────────
            SWLog.Measure("4a. ConvertDateTimeColumns", () =>
            {
                var dateTimeCols = donHang.Columns
                    .Cast<DataColumn>()
                    .Where(c => c.DataType == typeof(DateTime))
                    .Select(c => c.ColumnName)
                    .ToList();
                foreach (string colName in dateTimeCols)
                {
                    string tempName = colName + "_STR";
                    donHang.Columns.Add(tempName, typeof(string));
                    foreach (DataRow row in donHang.Rows)
                        row[tempName] = row[colName] == DBNull.Value
                            ? "" : ((DateTime)row[colName]).ToString("yyyy-MM-dd");
                    donHang.Columns.Remove(colName);
                    donHang.Columns[tempName].ColumnName = colName;
                }
            });

            // ── 4b. DropCreate IFS table ─────────────────────────────────────
            SWLog.Measure($"4b. DropCreate [{tenSPBang}]",
                () => DropCreate(tenSPBang, donHang));

            // ── 4c. BulkInsert vào IFS table ─────────────────────────────────
            SWLog.Measure($"4c. BulkInsert {donHang.Rows.Count} rows → [{tenSPBang}]",
                () => SqlTableCreator.BulkInsertDataTable(
                          _sql.B7R2_FCCdb, tenSPBang, donHang));

            // ── 4d. Xóa TMPPHIEUGIAOHANG trước khi SP INSERT ─────────────────
            // GUARD: chỉ xóa khi DOCQRCODE không có data (không đang bắn dở)
            SWLog.Measure($"4d. Guard DELETE [{tenBan}]", () =>
            {
                // Kiểm tra TMPPHIEUGIAOHANG tồn tại không
                string tmpExists = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                    "SELECT COUNT(*) FROM sys.objects " +
                    $"WHERE object_id = OBJECT_ID(N'[dbo].[{tenBan}]') " +
                    "AND type = 'U'");

                if (tmpExists != "1") return;

                // Kiểm tra DOCQRCODE có data không
                // Nếu có → đang bắn dở → KHÔNG xóa TMPPHIEUGIAOHANG
                string docQRExists = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                    "SELECT COUNT(*) FROM sys.objects " +
                    $"WHERE object_id = OBJECT_ID(N'[dbo].[{docQRTable}]') " +
                    "AND type = 'U'");

                if (docQRExists == "1")
                {
                    string demDocQR = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                        $"SELECT COUNT(*) FROM [{docQRTable}]");

                    if (int.TryParse(demDocQR, out int dem) && dem > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LuuVaLoad] SKIP DELETE [{tenBan}] " +
                            $"— đang có {dem} dòng trong [{docQRTable}]");
                        return;  // ← KHÔNG xóa khi đang bắn dở
                    }
                }

                // Không có DOCQRCODE → xóa TMPPHIEUGIAOHANG bình thường
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                    $"DELETE FROM [{tenBan}]");

                System.Diagnostics.Debug.WriteLine(
                    $"[LuuVaLoad] Đã DELETE [{tenBan}]");
            });

            // ── 4e. Gọi SP theo loại ─────────────────────────────────────────
            if (tenSP.Equals("Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                    StringComparison.OrdinalIgnoreCase))
            {
                return SWLog.Measure($"4e. CallSP [{tenSP}]",
                    () => CallSP(tenSP, ngayGiao, nhaMay, gioFcc, addNm,
                                 tmpTable: tenBan,
                                 ifsTable: tenSPBang,
                                 docQRTable: docQRTable));
            }

            return SWLog.Measure($"4e. CallSP [{tenSP}] (view)",
                () => CallSP(tenSP, ngayGiao, nhaMay, gioFcc, addNm,
                             tenBan: tenBan,
                             ifsView: ifsView,
                             docQRTable: docQRTable));
        }
        private DataTable DeduplicateDataTable(DataTable dt)
        {
            // Dùng DataView để loại bỏ duplicate
            var seen = new HashSet<string>();
            var toRemove = new List<DataRow>();

            foreach (DataRow row in dt.Rows)
            {
                // Key = tất cả cột ghép lại
                string key = string.Join("|",
                    row.ItemArray.Select(v => v?.ToString() ?? ""));

                if (!seen.Add(key))
                    toRemove.Add(row);
            }

            foreach (var row in toRemove)
                dt.Rows.Remove(row);

            return dt;
        }
        /// <summary>
        /// Tạo các bảng tạm nếu chưa có — gọi khi khởi động app.
        /// Đảm bảo SP không bao giờ gặp "Invalid object name".
        /// </summary>
        public void EnsureTablesExist()
        {
            // Schema tối thiểu — SP sẽ INSERT đúng cột sau
            string[] tables = new[]
            {
        "IFSPHIEUGIAOHANG",
        "IFSPHIEUGIAOHANGView"
    };

            string createSql =
                "IF NOT EXISTS (" +
                "    SELECT * FROM sys.objects " +
                "    WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') " +
                "    AND type = 'U'" +
                ") " +
                "CREATE TABLE [{0}] (" +
                "    STT        INT," +
                "    MAHANG     NVARCHAR(50)," +
                "    TENHANG    NVARCHAR(100)," +
                "    SOLUONG    INT," +
                "    NGAYGIAO   SMALLDATETIME," +
                "    GIOGIAO    NVARCHAR(50)," +
                "    GIOGIAOFCC NVARCHAR(200)," +
                "    NHAMAY     NVARCHAR(100)," +
                "    ADDNM      INT," +
                "    LOT        NVARCHAR(500)," +
                "    STATUS     NVARCHAR(50)," +
                "    STATUSDOC  NVARCHAR(50)" +
                ")";

            foreach (var table in tables)
            {
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                    string.Format(createSql, table));
            }
        }
        // ════════════════════════════════════════════════════════════════════════
        // Hàng thiếu
        // ════════════════════════════════════════════════════════════════════════
        public DataTable LoadHangThieu(bool isMayBanQR, string tenBan)
        {
            if (isMayBanQR)
                return _sql.LoadData(_sql.B7R2_FCCdb, "Usp_Qrcode_LOAD_HANGTHIEU");

            return _sql.LoadData(_sql.B7R2_FCCdb, "Usp_Qrcode_LOAD_HANGTHIEUView",
                new SqlParameter("@TENBAN", tenBan));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Load phiếu đã lưu
        // ════════════════════════════════════════════════════════════════════════
        public DataTable LoadLuuPhieu(string nhaMay, string ngayGiao, string gioGiaoFcc)
        {
            return _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, " +
                "NGAYGIAO, GIOGIAO, STATUS, TTPHIEU, NHAMAY, HOP, STATUSDOC, Note, " +
                "ISNULL(PO_NO,'') AS PO_NO, ISNULL(PO_ITEM,'') AS PO_ITEM " +
                "FROM LUUPHIEUGIAOHANG " +
                "WHERE NHAMAY=@nm AND NGAYGIAO=@ng AND GIOGIAOFCC=@gg",
                new List<SqlParameter>
                {
                new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao),
                new SqlParameter("@gg", gioGiaoFcc)
                });
        }

        public DataTable LoadTmpPhieuGiaoDB(string tenBan)
        {
            ValidateTenBan(tenBan);
            return _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT '' AS IDP, STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, " +
                $"SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU, NHAMAY, ADDNM, " +
                $"HOP, STATUSDOC, Note, " +
                $"ISNULL(PO_NO,'') AS PO_NO, ISNULL(PO_ITEM,'') AS PO_ITEM " +
                $"FROM [{tenBan}]");
        }

        public DataTable LoadGhepLot()
        {
            var ds = _sql.ExecuteProcedureReturnDataSet(
                _sql.B7R2_FCCdb, "Usp_Qrcode_gheplot");
            return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
        }

        public DataTable GetDanhSachLotTuKho(string maHang)
        {
            // Lấy LOT đang có trong kho theo mã hàng
            string sql = $@"
        SELECT LOT, SLCONLAI, SLXUAT, PART, NAME
        FROM STOCKTP
        WHERE PART = '{maHang.Replace("'", "''")}' 
          AND SLCONLAI > 0
        ORDER BY LOT";

            return _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Trạng thái bắn QR
        // ════════════════════════════════════════════════════════════════════════
        public TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable)
        {
            ValidateTenBan(tmpTable);
            ValidateTenBan(docQRTable);

            var result = new TrangThaiBan();

            object demQRRaw = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{docQRTable}]");
            if (!int.TryParse(demQRRaw?.ToString(), out int demQR) || demQR == 0)
            {
                result.DangBan = false;
                return result;
            }

            object demPhieuRaw = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{tmpTable}]");
            if (!int.TryParse(demPhieuRaw?.ToString(), out int demPhieu) || demPhieu == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAO, NHAMAY FROM [{tmpTable}]");
            if (dt.Rows.Count == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataRow r = dt.Rows[0];
            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = r["ADDNM"] == DBNull.Value ? 1 : Convert.ToInt32(r["ADDNM"]);
            result.NhaMay = r["NHAMAY"] == DBNull.Value ? "" : r["NHAMAY"].ToString().Trim();

            if (r["NGAYGIAO"] != DBNull.Value)
            {
                string ngayRaw = r["NGAYGIAO"].ToString();
                //result.NgayGiao = ngayRaw.Length >= 10 ? ngayRaw.Substring(0, 10) : ngayRaw;
                result.NgayGiao = r["NGAYGIAO"] == DBNull.Value ? ""
                  : Convert.ToDateTime(r["NGAYGIAO"])
                      .ToString("yyyy-MM-dd");
            }
            else result.NgayGiao = "";

            string gioDon = r["GIOGIAO"] == DBNull.Value ? "" : r["GIOGIAO"].ToString().Trim();
            if (gioDon.Length == 1) gioDon = "0" + gioDon;
            result.GioGiaoFCC = gioDon;

            return result;
        }

        //--- YMVN
        public TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable)
        {
            ValidateTenBan(tmpTable);
            ValidateTenBan(docQRTable);
            var result = new TrangThaiBan();

            // Form gốc: check addnm = 0
            string demTmpRaw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{tmpTable}] WHERE addnm = 0");
            if (!int.TryParse(demTmpRaw, out int demTmp) || demTmp == 0)
            {
                result.DangBan = false;
                return result;
            }

            // Check DOCQRCODE
            string demQRRaw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{docQRTable}]");
            if (!int.TryParse(demQRRaw, out int demQR) || demQR == 0)
            {
                result.DangBan = false;  // có TMP nhưng không có QR → load mới
                return result;
            }

            // Có cả TMP lẫn QR → đang bắn dở
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT TOP 1 NGAYGIAO, GIOGIAO FROM [{tmpTable}]");
            if (dt.Rows.Count == 0) { result.DangBan = false; return result; }

            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = 1;                    // ← bỏ _cfg, YMVN luôn = 1
            result.NhaMay = "YAMAHA - VIET NAM"; // ← hardcode ở repo là OK vì chỉ YMVN gọi method này

            string ngayRaw = dt.Rows[0]["NGAYGIAO"].ToString();
            result.NgayGiao = ngayRaw.Length >= 10 ? ngayRaw.Substring(0, 10) : ngayRaw;
            result.GioGiaoFCC = dt.Rows[0]["GIOGIAO"].ToString().Trim();
            result.NhaMay = "YAMAHA - VIET NAM";
            return result;
        }
        // PhieuRepository — thêm InsertTmpYMVN
        public void InsertTmpYMVN(string stt, string cua, string truyen,
        string maHang, string tenHang, string lot, string dv,
        int slXuat, string ngayGiao, string gear,string gioXuat, string tmpTable,
        string poNo = "", string cusPoNo = "")
        {
            ValidateTenBan(tmpTable);

            string sql = $@"
        INSERT INTO [{tmpTable}]
            (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV,
             SOLUONG, NGAYGIAO,GEAR, GIOGIAO, STATUS,
             PO_NO, TTPHIEU)
        VALUES
            (@STT, @CUA, @TRUYEN, @MAHANG, @TENHANG, @LOT, @DV,
             @SOLUONG, @NGAYGIAO,@GEAR, @GIOGIAO, 'NG',
             @PO_NO, @CUSPO)";

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@STT", stt),
                new SqlParameter("@CUA", cua),
                new SqlParameter("@TRUYEN", truyen),
                new SqlParameter("@MAHANG", maHang),
                new SqlParameter("@TENHANG", tenHang),
                new SqlParameter("@LOT", lot),
                new SqlParameter("@DV", dv),
                new SqlParameter("@SOLUONG", slXuat),
                new SqlParameter("@NGAYGIAO", ngayGiao),
                  new SqlParameter("@GEAR", gear),
                new SqlParameter("@GIOGIAO", gioXuat),
                new SqlParameter("@PO_NO", poNo),
                new SqlParameter("@CUSPO", cusPoNo));
        }


        // ════════════════════════════════════════════════════════════════════════
        // Xóa QR
        // ════════════════════════════════════════════════════════════════════════
        public void XoaDocQRCode(string docQRTable)
        {
            ValidateTenBan(docQRTable);
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"DELETE FROM [{docQRTable}]");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Lot
        // ════════════════════════════════════════════════════════════════════════
        public DataTable GetDonHangChuaLot(string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);
            return _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                $"SELECT STT, MAHANG, LOT, SOLUONG FROM [{tenBan}] " +
                $"WHERE (LOT = '' OR LOT IS NULL) " +
                $"AND MAHANG IN ( " +
                $"    SELECT MAHANGFCC FROM [{docQRTable}] " +
                $"    WHERE ISNULL(KETQUA,'') <> 'DG' " +
                $"    GROUP BY MAHANGFCC" +
                $") ORDER BY STT");
        }

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl,
                                               string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);
            string sql =
                $"SELECT STT, MAHANG, TENHANG, GIOGIAO, SOLUONG, " +
                "CASE " +
                "    WHEN STATUS IS NULL OR STATUS = '' THEN N'Chưa Bắn QRCODE' " +
                "    WHEN STATUS = '0'                  THEN N'Đang Bắn QRCODE' " +
                "    WHEN STATUS = '1'                  THEN N'Đã Bắn QRCODE' " +
                "    ELSE STATUS " +
                $"END AS STATUS FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma AND SOLUONG = @sl " +
                $"AND (LOT = '' OR LOT IS NULL) " +
                $"AND MAHANG IN (" +
                $"    SELECT MAHANGFCC FROM [{docQRTable}] " +
                $"    WHERE ISNULL(KETQUA,'') <> 'DG' GROUP BY MAHANGFCC)";

            return _sql.LoadData1(_sql.B7R2_FCCdb, sql,
                new SqlParameter("@ma", maHang),
                new SqlParameter("@sl", sl));
        }

        public int CountTrungMaSl(string maHang, int sl,
                                   string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma AND SOLUONG = @sl " +
                $"AND (LOT = '' OR LOT IS NULL) " +
                $"AND MAHANG IN (" +
                $"    SELECT MAHANGFCC FROM [{docQRTable}] " +
                $"    WHERE KETQUA <> 'DG' GROUP BY MAHANGFCC)",
                new SqlParameter[]
                {
                new SqlParameter("@ma", maHang),
                new SqlParameter("@sl", sl)
                });
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public string GetLotNo(string maHang, int stt, int dem, int slGiao,
                        string docQRTable = "DOCQRCODE",
                        string tmpTable = "TMPPHIEUGIAOHANG")
        {
            DataTable dt = _sql.LoadData(_sql.B7R2_FCCdb, "Usp_Qrcode_Take_Lot2405",
                new SqlParameter("@_MaFCC", maHang),
                new SqlParameter("@_STTP", stt),
                new SqlParameter("@_DeM", dem),
                new SqlParameter("@_SLGIAO", slGiao),
                new SqlParameter("@DOCQRTABLE", docQRTable),  // ← THÊM
                new SqlParameter("@TMPTABLE", tmpTable));    // ← THÊM

            var parts = new List<string>();
            foreach (DataRow row in dt.Rows)
                parts.Add($"{row["LOTFCC"].ToString().Trim()}" +
                          $"-{row["FCC"].ToString().Trim()}");
            return string.Join(",", parts);
        }

        public void CapNhapLotTmpPhieu(int stt, string lot, string tenBan)
        {
            if (stt <= 0 || string.IsNullOrWhiteSpace(lot)) return;
            ValidateTenBan(tenBan);
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"UPDATE [{tenBan}] SET LOT = @lot WHERE STT = @stt",
                new SqlParameter("@lot", lot),
                new SqlParameter("@stt", stt));
        }
        public DataTable GetDonHangHienTai(string tenBan)
        {
            ValidateTenBan(tenBan);
            return _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT STT, MAHANG, LOT, STATUS, STATUSDOC " +
                $"FROM [{tenBan}] ORDER BY STT");
        }

        public void LayLaiLotNo(int stt, string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"UPDATE [{tenBan}] " +
                "SET LOT = '', STATUSDOC = 'NG', TTPHIEU = NULL " +
                "WHERE STT = @stt AND ISNULL(STATUS,'') <> 'OK'",
                new SqlParameter("@stt", stt));

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"UPDATE [{docQRTable}] " +
                "SET GIO = NULL, KETQUA = 'OK', STTBAN = NULL " +
                "WHERE ISNULL(STTBAN, 0) = @stt AND KETQUA = 'DG'",
                new SqlParameter("@stt", stt));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Kho
        // ════════════════════════════════════════════════════════════════════════
        public int CapNhapKho(string gioGiaoFcc, string nhaMay,
                       string tmpTable, string docQRTable,
                       out DataTable errors)
        {
            var ds = _sql.ExecuteProcedureReturnDataSet(
                _sql.B7R2_FCCdb,
                "Usp_Qrcode_Update_Stock2405",   // ← dùng SP mới có dynamic table
                new SqlParameter("@GIOGIAOFCC", gioGiaoFcc ?? ""),
                new SqlParameter("@NHAMAY", nhaMay ?? ""),
                new SqlParameter("@TMPTABLE", tmpTable ?? "TMPPHIEUGIAOHANG"),
                new SqlParameter("@DOCQRTABLE", docQRTable ?? "DOCQRCODE"));

            errors = ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
            DataTable stok = ds.Tables[0]; // ← dòng thành công
                                           // ── Chỉ đánh dấu IsDelivered cho YMVN/HTN (có OrderTable) ───────
                                           // HVN không có OrderTable → bỏ qua
                                           // ── (1) Trừ SlotLot của kho ảo A0 theo từng LOT vừa xuất OK ──────────
            bool coAnhHuongA0 = false;
            if (stok.Rows.Count > 0)
            {
                var bulkService = new BulkStockAdjustService();
                foreach (DataRow row in stok.Rows)
                {
                    string lot = row["LOT"]?.ToString();
                    int sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                    if (string.IsNullOrWhiteSpace(lot) || sl <= 0) continue;

                    bool anhHuong = bulkService.TruKhoAoTheoLot(lot, sl); // ← đổi trả về bool
                    if (anhHuong) coAnhHuongA0 = true;
                }
            }

            // ── (2) Báo cho MainStockSV (nếu đang mở) vẽ lại — chỉ khi có ảnh hưởng A0 ─
            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();
            if (_cfg != null
            && _cfg.LoadTuBangRieng
            && !string.IsNullOrEmpty(_cfg.OrderTable)
            && stok.Rows.Count > 0)
            {
                foreach (DataRow row in stok.Rows)
                {
                    string maHang = row["MH"]?.ToString() ?? "";
                    int stt = row.Table.Columns.Contains("STT")
                                    ? Convert.ToInt32(row["STT"]) : 0;
                    if (string.IsNullOrEmpty(maHang)) continue;

                    // ← dùng STT để query chính xác 1 dòng
                    string whereClause = stt > 0
                        ? $"STT={stt}"
                        : $"MAHANG='{SqlHelper.Esc(maHang)}' AND STATUS='OK'";

                    string ngayGiao = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                        $"SELECT CONVERT(varchar, NGAYGIAO, 23) " +
                        $"FROM [{tmpTable}] WHERE {whereClause}");

                    string poNo = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                        $"SELECT ISNULL(PO_NO,'') " +
                        $"FROM [{tmpTable}] WHERE {whereClause}");

                    if (!string.IsNullOrEmpty(poNo) && !string.IsNullOrEmpty(ngayGiao))
                        DanhDauDaGiao(poNo, maHang, ngayGiao, _cfg);
                }
            }

            return stok.Rows.Count;
        }
        public int CapNhapKhoHTN(string nhaMay, string tmpTable,
                          string docQRTable, out DataTable errors)
            => CapNhapKho("", nhaMay, tmpTable, docQRTable, out errors);
        public int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors)
        {
            var ds = _sql.ExecuteProcedureReturnDataSet(
                _sql.B7R2_FCCdb, "Usp_Qrcode_Update_Stock_SP",
                new SqlParameter("@GIOGIAOFCC", gioGiaoFcc),
                new SqlParameter("@NHAMAY", nhaMay));

            errors = ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
            DataTable stok = ds.Tables[0];

            // ── THÊM: trừ kho ảo A0 — CHỈ patch nếu xác nhận SP này có cột LOT/SOLUONG ──
            if (stok.Rows.Count > 0 && stok.Columns.Contains("LOT") && stok.Columns.Contains("SOLUONG"))
            {
                var bulkService = new BulkStockAdjustService();
                bool coAnhHuongA0 = false;

                foreach (DataRow row in stok.Rows)
                {
                    string lot = row["LOT"]?.ToString();
                    int sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                    if (string.IsNullOrWhiteSpace(lot) || sl <= 0) continue;

                    if (bulkService.TruKhoAoTheoLot(lot, sl))
                        coAnhHuongA0 = true;
                }

                if (coAnhHuongA0)
                    StockChangedNotifier.RaiseStockChanged();
            }

            return stok.Rows.Count;
        }
        public bool CapNhapKhoYMVN(int stt, string lotSl, string maHang,
                              string ngayGiao, string gioGiao, string nhaMay,
                  
                              out DS_ERR_CNK error)
        {
            error = null;
            var bulkService = new BulkStockAdjustService();
            bool coAnhHuongA0 = false;
            string tmpTable = _cfg.TmpTable;      // TMPPHIEUGIAOHANG_100002
            string docQRTable = _cfg.DocQRTable;    // YMVN_DOCQRCODE
            string tmpTableSP = _cfg.TmpTableSP;   // SP_TMPPHIEUGIAOHANG
            string docQRTableSP = _cfg.DocQRTableSP; // SP_DOCQRCODE

            // ── Tách từng LOT-SL ────────────────────────────────────────────────
            string[] lotParts = lotSl.Split(',');
            foreach (string part in lotParts)
            {
                string[] tach = part.Trim().Split('-');
                if (tach.Length < 2) continue;

                string lot = tach[0].Length >= 13
                    ? tach[0].Substring(0, 13) : tach[0];

                if (!int.TryParse(tach[1], out int sl)) continue;

                // ── Kiểm tra tồn kho ─────────────────────────────────────────────
                string slConlaiRaw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                    $"SELECT ISNULL(slconlai,0) FROM STOCKTP " +
                    $"WHERE SUBSTRING(LOT,1,13) = '{SqlHelper.Esc(lot)}'");
                
                if (!int.TryParse(slConlaiRaw, out int slConlai) || slConlai < sl)
                {
                    error = new DS_ERR_CNK
                    {
                        MH = maHang,
                        LOT = lot,
                        SLC = sl,
                        SLTK = slConlai,
                        SLT = sl - slConlai,
                        Ms = "Không đủ tồn kho"
                    };
                    return false;
                }

                // ── Trừ kho ──────────────────────────────────────────────────────
                string gg = ngayGiao + " " + gioGiao + ":00";
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                    $"UPDATE t " +
                    $"SET t.ngayxuat   = '{gg}', " +
                    $"    t.slxuat     = slxuat + {sl}, " +
                    $"    t.slconlai   = slconlai - {sl} " +
                    $"FROM (SELECT TOP 1 * FROM STOCKTP " +
                    $"      WHERE SUBSTRING(LOT,1,13) = '{SqlHelper.Esc(lot)}') t");
                // ── THÊM ĐÚNG CHỖ: chỉ trừ A0 SAU KHI STOCKTP đã trừ thành công ──
                if (bulkService.TruKhoAoTheoLot(lot, sl))
                    coAnhHuongA0 = true;
            }

            // ── Lưu LUUDOCQRCODE — dùng tên bảng động ───────────────────────────
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, $@"
        INSERT INTO LUUDOCQRCODE
            (LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
             STATUS, MAFCC, STT, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY)
        SELECT
            LEFT(LOTFCC,  500), LEFT(MAHANGFCC, 60), SLTEMFCC,
            LEFT(LOTHVN,  500), LEFT(MAHANGHVN, 60), SLTEMHVN,
            STATUS, LEFT(MAFCC, 50), STT, KETQUA,
            '{ngayGiao}', '{SqlHelper.Esc(gioGiao)}', '{SqlHelper.Esc(nhaMay)}'
        FROM [{docQRTable}]
        WHERE MAHANGFCC = '{SqlHelper.Esc(maHang)}'
          AND KETQUA    = 'DG'");

            // ── Lưu LUUPHIEUGIAOHANG — thêm GEAR từ cột đúng ────────────────────
            // TMPPHIEUGIAOHANG_100002.GEAR → LUUPHIEUGIAOHANG.GearYMVN
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, $@"
        INSERT INTO LUUPHIEUGIAOHANG
            (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
             NGAYGIAO, GIOGIAO, STATUS, GearYMVN,       -- ← đúng tên cột LUUPHIEUGIAOHANG
             NHAMAY, GIOGIAOFCC,
             PO_NO, TTPHIEU)
        SELECT
            STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
            NGAYGIAO, GIOGIAO, 'OK',
            ISNULL(GEAR,''),                            -- ← GEAR từ TMPPHIEUGIAOHANG_100002
            '{SqlHelper.Esc(nhaMay)}',
            CONVERT(VARCHAR(8), GETDATE(), 108),
            ISNULL(PO_NO,''), ISNULL(TTPHIEU,'')
        FROM [{tmpTable}]
        WHERE STT    = {stt}
          AND STATUS = 'NG'");

            // ── Cập nhật STATUS = OK ─────────────────────────────────────────────
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"UPDATE [{tmpTable}] SET STATUS = 'OK' WHERE STT = {stt}");

            // ── Xóa DOCQRCODE đã xử lý ───────────────────────────────────────────
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"DELETE FROM [{docQRTable}] " +
                $"WHERE MAHANGFCC = '{SqlHelper.Esc(maHang)}' AND KETQUA = 'DG'");
            // ── Đánh dấu IsDelivered=1 trong Purchase_Order ──────────────────
            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();
            // Lấy PO_NO từ TMP để xác định đúng đơn hàng
            string poNo = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(PO_NO,'') FROM [{tmpTable}] WHERE STT={stt}");

            if (!string.IsNullOrEmpty(poNo) && !string.IsNullOrEmpty(_cfg.OrderTable))
            {
                DanhDauDaGiao(poNo, maHang, ngayGiao, _cfg);
            }
            return true;
        }

        public int LuuPhieuSP(string nhaMay, string ngayGiao,
                               string gioGiaoFcc, string loaiPhieu)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                "UPDATE LUUPHIEUGIAOHANG " +
                "SET LOT = ISNULL(PO_NO,'') + '-' + ISNULL(PO_ITEM,'') " +
                "WHERE NHAMAY=@nm AND NGAYGIAO=@ng AND GIOGIAOFCC=@gg " +
                "AND (LOT IS NULL OR LOT='') AND ISNULL(PO_NO,'')<>''",
                new SqlParameter[] {
                new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao),
                new SqlParameter("@gg", gioGiaoFcc)
                });

            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT COUNT(*) FROM LUUPHIEUGIAOHANG " +
                "WHERE NHAMAY=@nm AND NGAYGIAO=@ng AND GIOGIAOFCC=@gg " +
                "AND LOT IS NOT NULL AND LOT<>''",
                new SqlParameter[] {
                new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao),
                new SqlParameter("@gg", gioGiaoFcc)
                });
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public void CapNhapTTPHIEU(string nhaMay, string ngayGiao,
                                     string gioGiaoFcc, int stt, string ghiChu)
        {
            string safe = ghiChu?.Trim() == "STOP" ? "STOP" : "";
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                "UPDATE LUUPHIEUGIAOHANG SET TTPHIEU=@gc " +
                "WHERE NHAMAY=@nm AND NGAYGIAO=@ng AND GIOGIAOFCC=@gg AND STT=@stt",
                new SqlParameter("@gc", safe),
                new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao),
                new SqlParameter("@gg", gioGiaoFcc),
                new SqlParameter("@stt", stt));
        }
        // Thêm vào cuối CapNhapKhoYMVN sau INSERT LUUPHIEUGIAOHANG
        public void DanhDauDaGiao(string poNo, string maHang,
            string ngayGiao, CustomerConfig cfg)
        {
            if (string.IsNullOrEmpty(cfg.OrderTable)) return;

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, $@"
        UPDATE [{cfg.OrderTable}]
        SET IsDelivered   = 1,
            DeliveredDate = GETDATE()
        WHERE Oder_no = '{SqlHelper.Esc(poNo)}'
          AND Part_no  = '{SqlHelper.Esc(maHang)}'
          AND CAST(NgayGiao AS DATE) = '{ngayGiao}'
          AND IsDelivered = 0");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Giao DB
        // ════════════════════════════════════════════════════════════════════════
        public DataTable GetDanhSachMaHang() =>
            _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT ID, Code, Name FROM B20Item " +
                "WHERE LEN(Code) > 10 GROUP BY ID, Code, Name ORDER BY ID");

        public void LuuGiaoDB(DataTable donHang, string gioFccMoTa,
                               int addNm, string tmpTable, string ifsTable,
                               string nhaMayOverride = "")
        {
            ValidateTenBan(tmpTable);
            ValidateTenBan(ifsTable);

            DropCreate(ifsTable, donHang);
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, $"DELETE FROM [{ifsTable}]");
            SqlTableCreator.BulkInsertDataTable(_sql.B7R2_FCCdb, ifsTable, donHang);

            // 100001: tính theo addNm; customer khác: dùng nhaMayOverride
            string nhaMay = !string.IsNullOrEmpty(nhaMayOverride)
                ? nhaMayOverride
                : (addNm == 1
                    ? "HON DA - VIET NAM(NHA MAY VP)"
                    : "HON DA - VIET NAM(NHA MAY HA NAM)");

            CallSP("Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                   DateTime.Now.ToString("yyyy-MM-dd"),
                   nhaMay, gioFccMoTa, addNm,
                   tmpTable: tmpTable,
                   ifsTable: ifsTable,
                   docQRTable: "DOCQRCODE");

            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"UPDATE D SET D.GGFCC=T.GIOGIAO, D.LOT=T.LOT, " +
                $"D.NGAYGIAO=T.NGAYGIAO, D.STATUS='OK' " +
                $"FROM [{tmpTable}] T " +
                $"INNER JOIN TMPPHIEUGIAOHANGDBCT D " +
                $"  ON D.MAHANG=T.MAHANG " +
                $"  AND D.IDP=SUBSTRING(T.TTPHIEU," +
                $"      CHARINDEX('-',T.TTPHIEU)+1,LEN(T.TTPHIEU)) " +
                $"  AND D.STATUS='NG' AND T.LOT<>''");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════════
        public Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList)
{
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (maHangList == null || maHangList.Count == 0) return result;

            // Build IN clause
            string inClause = string.Join(",",
                maHangList.Select(m => $"'{m.Replace("'", "''")}'"));

            string sql =
                $"SELECT Code, ISNULL(CAST(MinCloseQty AS INT), 0) AS QC " +
                $"FROM B20Item WHERE Code IN ({inClause})";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
            foreach (DataRow row in dt.Rows)
                result[row["Code"].ToString().Trim()] = Convert.ToInt32(row["QC"]);

            return result;
        }

        // ── CallSP gộp — 1 method duy nhất cho tất cả SP ────────────────────────
        private DataTable CallSP(string tenSP, string ngayGiao, string nhaMay,
                                  string gioFcc, int addNm,
                                  string tmpTable = "",
                                  string ifsTable = "",
                                  string docQRTable = "",
                                  string tenBan = "",
                                  string ifsView = "")
        {
            object ngayParam = DateTime.TryParse(ngayGiao, out DateTime dt)
                ? (object)dt : DBNull.Value;

            var paramList = new List<SqlParameter>
        {
            new SqlParameter("@NGAYGIAO", SqlDbType.SmallDateTime) { Value = ngayParam },
            new SqlParameter("@NHAMAY",   nhaMay),
            new SqlParameter("@GIOFCC",   gioFcc),
            new SqlParameter("@ADDNM",    addNm)
        };

            // SP bắn QR: Usp_Qrcode_LOAD_PHIEU_DOCQR2405
            if (!string.IsNullOrEmpty(tmpTable))
                paramList.Add(new SqlParameter("@TMPTABLE", tmpTable));
            if (!string.IsNullOrEmpty(ifsTable))
                paramList.Add(new SqlParameter("@IFSTABLE", ifsTable));
            if (!string.IsNullOrEmpty(docQRTable))
                paramList.Add(new SqlParameter("@DOCQRTABLE", docQRTable));

            // SP view: Usp_Qrcode_LOAD_PHIEU_DOCQRView2405
            if (!string.IsNullOrEmpty(tenBan))
                paramList.Add(new SqlParameter("@TENBAN", tenBan));
            if (!string.IsNullOrEmpty(ifsView))
                paramList.Add(new SqlParameter("@IFSVIEW", ifsView));

            var ds = _sql.ExecuteProcedureReturnDataSet(
                _sql.B7R2_FCCdb, tenSP, paramList.ToArray());
            return ds.Tables[0];
        }

        private void DropCreate(string tenBang, DataTable schema)
        {
            // Thay vì DROP → CREATE (gây race condition)
            // Dùng: nếu chưa có → CREATE, nếu có rồi → chỉ TRUNCATE
            string checkExist = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT COUNT(*) FROM sys.objects " +
                $"WHERE object_id = OBJECT_ID(N'[dbo].[{tenBang}]') " +
                "AND type = 'U'");

            if (checkExist == "0")
            {
                // Chưa có → CREATE lần đầu
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                    SqlTableCreator.GetCreateFromDataTableSQL(tenBang, schema));
            }
            else
            {
                // Đã có → chỉ TRUNCATE, không DROP
                // TRUNCATE nhanh hơn DELETE và không gây missing bảng
                _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                    $"TRUNCATE TABLE [{tenBang}]");
            }
        }

        private static void ValidateTenBan(string tenBan)
        {
            if (string.IsNullOrWhiteSpace(tenBan) ||
                System.Text.RegularExpressions.Regex.IsMatch(tenBan, @"[^A-Za-z0-9_]"))
                throw new ArgumentException($"Tên bảng không hợp lệ: '{tenBan}'");
        }

        private static int SafeInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            try { return Convert.ToInt32(val); }
            catch { return 0; }
        }


        ///-YMVN
        public void XoaTmpPhieu(string tenBan)
        {
            ValidateTenBan(tenBan);
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, $"DELETE FROM [{tenBan}]");
        }
        public void ExecNonQuery(string spName)
    => _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, spName);
        public IReadOnlyList<string> GetGioGiaoYMVN(string ngayGiao)
        {
            // Lấy distinct giờ từ Purchase_Order_YMVN theo ngày
            string sql =
                "SELECT DISTINCT " +
                "    RIGHT('0' + CAST(DATEPART(HH, NgayGiao) AS VARCHAR), 2) + ':' + " +
                "    RIGHT('0' + CAST(DATEPART(MI, NgayGiao) AS VARCHAR), 2) AS GIO " +
                "FROM Purchase_Order_YMVN " +
                $"WHERE CAST(NgayGiao AS DATE) = '{ngayGiao}' " +
                "ORDER BY GIO";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
            return dt.AsEnumerable()
                     .Select(r => r["GIO"].ToString())
                     .ToList();
        }
        public void ExecSP(string spName, params SqlParameter[] parms)
        => _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, spName, parms);
        public DataTable ExecSPWithResult(string spName, params SqlParameter[] parms)
    => _sql.LoadData(_sql.B7R2_FCCdb, spName, parms);

       

       public DataTable LoadPhieuTuBangRieng(string ngayGiao, string gioFilter,
                               bool isLoaiSP, string dockCodeSP,
                               CustomerConfig cfg)
{
    ValidateTenBan(cfg.OrderTable);

    // Filter giờ

    string whereGio = "";
    if (!string.IsNullOrWhiteSpace(gioFilter))
    {
        string gioInt = string.Join(",",
            gioFilter
                .Split(',')
                .Select(g => g.Trim().Trim('\''))
                .Select(g => int.Parse(g).ToString()));
        whereGio = $"AND DATEPART(HH, o.NgayGiao) IN ({gioInt}) ";
    }

    // Filter SP/MP
    string whereSP = "";
    if (cfg.CoLoaiSP)
    {
        whereSP = isLoaiSP
            ? $"AND RTRIM(o.CUA) = '{dockCodeSP.Replace("'", "''")}' "
            : $"AND RTRIM(o.CUA) <> '{dockCodeSP.Replace("'", "''")}' ";
    }

            string sql =
            "SELECT " +
            "    o.Oder_no                               AS PO_NO, " +
            "    CONVERT(VARCHAR(5), o.NgayGiao, 108)    AS GIO, " +
            "    MAX(o.CUA)                              AS CUA, " +
            "    MAX(o.CUA)                              AS TRUYEN, " +
            "    o.Part_no                               AS MAHANG, " +
            "    MAX(o.Part_name)                        AS TENHANG, " +
            "    ''                                      AS LOT, " +
            "    'PCS'                                   AS DV, " +
            "    SUM(o.Slgiao)                           AS SOLUONG, " +
            "    MAX(o.QCDG)                             AS HOP, " +
            "    MAX(ISNULL(o.Gear, ''))                 AS GEAR, " +
            "    'NG'                                    AS STATUS, " +
            "    'NG'                                    AS STATUSDOC, " +
            "    ''                                      AS TTPHIEU, " +
            "    MIN(o.NgayGiao)                         AS NGAYGIAO, " +
            "    o.Oder_no                               AS ORDER_NO " +
            $"FROM [{cfg.OrderTable}] o " +
            $"WHERE CAST(o.NgayGiao AS DATE) = '{ngayGiao}' " +
            // ── Chỉ lấy đơn hàng chưa giao ──────────────────────────────────
            //"AND (o.IsDelivered = 0 OR o.IsDelivered IS NULL) " +
            whereGio +
            whereSP +
            "GROUP BY o.Oder_no, o.Part_no, " +
            "         CONVERT(VARCHAR(5), o.NgayGiao, 108) " +
            "ORDER BY GIO, MAX(o.CUA), o.Part_no";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);

    if (!dt.Columns.Contains("STT"))
        dt.Columns.Add("STT", typeof(int));
    for (int i = 0; i < dt.Rows.Count; i++)
        dt.Rows[i]["STT"] = i + 1;

    MergeLotTuBangRieng(dt, ngayGiao, cfg.TenNhaMay);
    return dt;
}
        public DataTable LoadTuTmpTable(string tmpTable)
        {
            ValidateTenBan(tmpTable);

            // Đọc thẳng từ bảng TMP — đã có LOT, STATUS từ quá trình bắn QR
            string sql = $@"
        SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV,
               SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU,
               NHAMAY, ADDNM, HOP, STATUSDOC, Note,
               ISNULL(PO_NO,'')   AS PO_NO,
               ISNULL(PO_ITEM,'') AS PO_ITEM
        FROM [{tmpTable}]
        ORDER BY TRY_CAST(STT AS INT), STT";

            return _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
        }
        private void MergeLotTuBangRieng(DataTable dt,
        string ngayGiao, string tenNhaMay)
        {
            if (dt.Rows.Count == 0) return;
            if (string.IsNullOrEmpty(tenNhaMay)) return;

            // ── Thêm GearSuDung vào SELECT ───────────────────────────────────────
            string sql =
                "SELECT MAHANG, GIOGIAO, LOT, STATUS, STATUSDOC, " +
                "       ISNULL(PO_NO,'')      AS PO_NO, " +
                "       ISNULL(SOLUONG, 0)    AS SOLUONG, " +
                "       ISNULL(GearYMVN,'') AS GEAR " +  // ← thêm
                "FROM LUUPHIEUGIAOHANG " +
                $"WHERE CAST(NGAYGIAO AS DATE) = '{ngayGiao}' " +
                $"  AND NHAMAY = '{tenNhaMay.Replace("'", "''")}'";

            DataTable luuDt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
            if (luuDt.Rows.Count == 0) return;

            // ── Build lookup ─────────────────────────────────────────────────────
            var lookup = new Dictionary<string, DataRow>(
                StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in luuDt.Rows)
            {
                string gioChuan = NormalizeGio(row["GIOGIAO"].ToString().Trim());
                string keyFull = row["MAHANG"].ToString().Trim()
                                + "|" + gioChuan
                                + "|" + row["PO_NO"].ToString().Trim();
                string keyShort = row["MAHANG"].ToString().Trim()
                                + "|" + gioChuan;

                if (!lookup.ContainsKey(keyFull)) lookup[keyFull] = row;
                if (!lookup.ContainsKey(keyShort)) lookup[keyShort] = row;
            }

            // ── Merge vào dt ─────────────────────────────────────────────────────
            // Đảm bảo dt có cột GearSuDung trước khi ghi
            if (!dt.Columns.Contains("GearSuDung"))
                dt.Columns.Add("GearSuDung", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                string gioChuan = NormalizeGio(row["GIO"].ToString().Trim());
                string maHang = row["MAHANG"].ToString().Trim();
                string poNo = dt.Columns.Contains("PO_NO")
                                    ? row["PO_NO"]?.ToString().Trim() ?? ""
                                    : "";

                string keyFull = maHang + "|" + gioChuan + "|" + poNo;
                string keyShort = maHang + "|" + gioChuan;

                DataRow luuRow = null;
                if (!lookup.TryGetValue(keyFull, out luuRow))
                    lookup.TryGetValue(keyShort, out luuRow);

                if (luuRow == null) continue;

                string lot = luuRow["LOT"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(lot)) continue;

                row["LOT"] = lot;
                row["STATUS"] = luuRow["STATUS"]?.ToString() ?? "NG";
                row["STATUSDOC"] = luuRow["STATUSDOC"]?.ToString() ?? "NG";

                // ── FIX: chỉ merge GEAR từ LUUPHIEUGIAOHANG nếu GEAR hiện tại rỗng ──
                // GEAR đã được load từ Purchase_Order → ưu tiên giữ nguyên
                if (dt.Columns.Contains("GEAR"))
                {
                    string gearHienTai = row["GEAR"]?.ToString().Trim() ?? "";
                    string gearLuu = luuRow["GEAR"]?.ToString().Trim() ?? "";

                    if (string.IsNullOrEmpty(gearHienTai) && !string.IsNullOrEmpty(gearLuu))
                        row["GEAR"] = gearLuu;
                    // ← nếu đã có GEAR từ Purchase_Order → giữ nguyên
                }
            }
        }

        // ── Helper: chuẩn hóa giờ về 2 chữ số ───────────────────────────────────
        // "8" → "08", "08:30" → "08", "8:30" → "08", "08H" → "08", "8H" → "08"
        private static string NormalizeGio(string gio)
        {
            if (string.IsNullOrWhiteSpace(gio)) return "00";

            // Bỏ "H" ở cuối
            gio = gio.Replace("H", "").Trim();

            // Lấy phần trước dấu ":"
            int colonIdx = gio.IndexOf(':');
            if (colonIdx >= 0)
                gio = gio.Substring(0, colonIdx).Trim();

            // Pad 2 chữ số
            if (int.TryParse(gio, out int gioInt))
                return gioInt.ToString("00");

            return "00";
        }
        // 5b. Load phiếu đang bắn dở từ bảng tạm YMVN
        public DataTable LoadPhieuDangDocYMVN(string tmpTable, bool isLoaiSP = false)
        {
            ValidateTenBan(tmpTable);

            if (isLoaiSP)
            {
                return _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                    $"SELECT STT, " +
                    $"NGAYGIAO    AS WANTED_DELIVERY_DATE, " +
                    $"SLGIAO      AS BUY_QTY_DUE, " +
                    $"'pcs'       AS DV, " +
                    $"''          AS Gear, " +
                    $"NHAMAY, " +
                    $"PO_NO       AS PO_NO, " +
                    $"MAHANG      AS CUSTOMER_PART_NO, " +
                    $"TENHANG     AS CATALOG_DESC, " +
                    $"CUA         AS DOCK_CODE, " +
                    $"''          AS HOP, " +
                    $"''          AS XE, " +
                    $"LOTNO       AS LOT, " +
                    $"STATUS " +
                    $"FROM [{tmpTable}]");
            }
            else
            {
                return _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                    $"SELECT STT, " +
                    $"NGAYGIAO    AS WANTED_DELIVERY_DATE, " +
                    $"SOLUONG     AS BUY_QTY_DUE, " +
                    $"DV, " +
                    $"NHAMAY, " +
                    $"CUA         AS PO, " +
                    $"MAHANG      AS CUSTOMER_PART_NO, " +
                    $"TENHANG     AS CATALOG_DESC, " +
                    $"TRUYEN      AS DOCK_CODE, " +
                    $"''          AS HOP, " +
                    $"''          AS XE, " +
                    $"LOT, " +
                    $"STATUS, " +
                    $"TTPHIEU     AS Gear " +
                    $"FROM [{tmpTable}]");
            }
        }

        // 5c. Hàng thiếu YMVN — so sánh IFS vs tồn kho (DuyetTTHangThieu cũ)
        // Trả DataTable để View bind vào GCT_HT
        public DataTable LoadHangThieuYMVN(string ngayXuatMDY, bool isLoaiSP)
        {
            var dt = new DataTable();
            dt.Columns.Add("MAHANG");
            dt.Columns.Add("SLGIAO", typeof(int));
            dt.Columns.Add("SLTONKHO", typeof(int));
            dt.Columns.Add("SLTHIEU", typeof(int));

            string dockWhere = isLoaiSP ? "AND CUA = 'VSP1'" : "AND CUA <> 'VSP1'";

            // ← sửa: Part_no thay CUSTOMER_PART_NO, Slgiao thay BUY_QTY_DUE
            string sqlGiao =
                $"SELECT Part_no AS MAHANG, SUM(Slgiao) AS SLGIAO " +
                $"FROM Purchase_Order_YMVN " +
                $"WHERE CONVERT(VARCHAR(10), NgayGiao, 101) = '{SqlHelper.Esc(ngayXuatMDY)}' " +
                $"{dockWhere} " +
                $"GROUP BY Part_no";

            DataTable giao = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sqlGiao);

            foreach (DataRow r in giao.Rows)
            {
                string ma = r["MAHANG"].ToString();
                int slGiao = SafeInt(r["SLGIAO"]);

                string tkRaw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                    $"SELECT ISNULL(SUM(slconlai),0) FROM stocktp " +
                    $"WHERE part='{SqlHelper.Esc(ma)}' AND slconlai > 0");
                int tk = int.TryParse(tkRaw, out int v) ? v : 0;

                if (slGiao > tk)
                    dt.Rows.Add(ma, slGiao, tk, slGiao - tk);  // ← sửa: slGiao-tk thay tk-slGiao
            }

            return dt;
        }
        public IReadOnlyList<string> GetDanhSachGioYMVN(string ngayXuatMDY)
        {
            var dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb,
                "SELECT CONVERT(VARCHAR, NgayGiao, 108) AS TIMES " +
                "FROM Purchase_Order_YMVN " +
                $"WHERE CONVERT(VARCHAR(10), NgayGiao, 101) = '{ngayXuatMDY}' " +
                "GROUP BY NgayGiao ORDER BY NgayGiao");
            return dt.Rows.Cast<DataRow>()
                     .Select(r => r["TIMES"].ToString())
                     .ToList();
        }

        public void UploadMilkrunSP(DataTable donHang, string ngayGiao)
        {
            foreach (DataRow row in donHang.Rows)
            {
                string dockCode = row["DOCK_CODE"]?.ToString() ?? "";
                if (!dockCode.Contains("VSP")) continue;

                string po = row["PO"]?.ToString() ?? "";
                string pno = row["CUSTOMER_PART_NO"]?.ToString() ?? "";
                string name = row["CATALOG_DESC"]?.ToString() ?? "";
                int sl = SafeInt(row["BUY_QTY_DUE"]);

                string check = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                    $"SELECT COUNT(*) FROM Purchase_Order_YMVN " +
                    $"WHERE Oder_no='{SqlHelper.Esc(po)}' AND Part_no='{SqlHelper.Esc(pno)}'");

                if (check == "0")
                    _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                        $"INSERT INTO Purchase_Order_YMVN " +
                        $"(Oder_no,Part_no,Part_name,NgayGiao,Slgiao,QCDG,CUA) " +
                        $"VALUES('{SqlHelper.Esc(po)}','{SqlHelper.Esc(pno)}','{SqlHelper.Esc(name)}'," +
                        $"'{ngayGiao}',{sl},0,'VSP1')");
            }
        }

        
    }

}
  
