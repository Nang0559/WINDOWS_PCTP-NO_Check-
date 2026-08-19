using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuTmpRepository : IPhieuTmpRepository
    {
        private readonly PhieuSqlExecutor _db;

        public PhieuTmpRepository(
            PhieuSqlExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // ============================================================
        // LoadPhieuDocQR
        // ============================================================

        public DataTable LoadPhieuDocQR(
            string ngayGiao,
            string nhaMay,
            string gioFcc,
            int addNm,
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            _db.ValidateTableName(tables.TmpTable);
            _db.ValidateTableName(tables.SourceTable);
            _db.ValidateTableName(tables.DocQRTable);

            DataTable tt = _db.LoadData(
                $"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAOFCC, NHAMAY " +
                $"FROM [{tables.SourceTable}]");

            if (tt.Rows.Count > 0)
            {
                DataRow row = tt.Rows[0];

                ngayGiao = row["NGAYGIAO"]?.ToString() ?? "";
                nhaMay = row["NHAMAY"]?.ToString() ?? "";
                gioFcc = row["GIOGIAOFCC"]?.ToString() ?? "";
                addNm = PhieuSqlExecutor.SafeInt(row["ADDNM"]);
            }

            return _db.CallPhieuSP(
                "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                ngayGiao,
                nhaMay,
                gioFcc,
                addNm,
                tables);
        }

        // ============================================================
        // Wrapper cũ
        // ============================================================

        public DataTable LoadPhieuDocQR(
            string ngayGiao,
            string nhaMay,
            string gioFcc,
            int addNm,
            string tmpTable,
            string ifsTable,
            string docQRTable)
        {
            return LoadPhieuDocQR(
                ngayGiao,
                nhaMay,
                gioFcc,
                addNm,
                new PhieuTableSet(
                    tmpTable,
                    ifsTable,
                    docQRTable));
        }

        // ============================================================
        // LuuVaLoad
        // ============================================================

        public DataTable LuuVaLoad(
            PhieuTableSet tables,
            string tenSP,
            DataTable donHang,
            string ngayGiao,
            string nhaMay,
            string gioFcc,
            int addNm)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            if (donHang == null)
                throw new ArgumentNullException(nameof(donHang));

            _db.ValidateTableName(tables.TmpTable);
            _db.ValidateTableName(tables.SourceTable);
            _db.ValidateTableName(tables.DocQRTable);

            ConvertDateTimeColumns(donHang);

            // --------------------------------------------------------
            // 4b. Drop/Create source table
            // --------------------------------------------------------

            SWLog.Measure(
                $"4b. DropCreate [{tables.SourceTable}]",
                () => _db.DropCreate(
                    tables.SourceTable,
                    donHang));

            // --------------------------------------------------------
            // 4c. Bulk insert
            // --------------------------------------------------------

            SWLog.Measure(
                $"4c. BulkInsert {donHang.Rows.Count} rows → [{tables.SourceTable}]",
                () => _db.BulkInsert(
                    tables.SourceTable,
                    donHang));

            // --------------------------------------------------------
            // 4d. Guard DELETE TMP
            // --------------------------------------------------------

            SWLog.Measure(
                $"4d. Guard DELETE [{tables.TmpTable}]",
                () => GuardDeleteTmp(
                    tables.TmpTable,
                    tables.DocQRTable));

            // --------------------------------------------------------
            // 4e. Call SP
            // --------------------------------------------------------

            return SWLog.Measure(
                $"4e. CallSP [{tenSP}]",
                () => _db.CallPhieuSP(
                    tenSP,
                    ngayGiao,
                    nhaMay,
                    gioFcc,
                    addNm,
                    tables));
        }

        // ============================================================
        // Wrapper cũ
        // ============================================================

        public DataTable LuuVaLoad(
            string tenSPBang,
            string tenSP,
            DataTable donHang,
            string ngayGiao,
            string nhaMay,
            string gioFcc,
            int addNm,
            string tenBan,
            string docQRTable,
            string ifsView = "")
        {
            var tables = new PhieuTableSet(
                tenBan,
                tenSPBang,
                docQRTable,
                tenBan,
                ifsView);

            return LuuVaLoad(
                tables,
                tenSP,
                donHang,
                ngayGiao,
                nhaMay,
                gioFcc,
                addNm);
        }

        // ============================================================
        // LoadTuTmpTable
        // ============================================================

        public DataTable LoadTuTmpTable(
            string tmpTable)
        {
            _db.ValidateTableName(tmpTable);

            string sql = $@"
SELECT
    STT,
    CUA,
    TRUYEN,
    MAHANG,
    TENHANG,
    LOT,
    DV,
    SOLUONG,
    NGAYGIAO,
    GIOGIAO,
    STATUS,
    TTPHIEU,
    NHAMAY,
    ADDNM,
    HOP,
    STATUSDOC,
    Note,
    ISNULL(PO_NO, '')   AS PO_NO,
    ISNULL(PO_ITEM, '') AS PO_ITEM
FROM [{tmpTable}]
ORDER BY
    TRY_CAST(STT AS INT),
    STT";

            return _db.LoadData(sql);
        }

        // ============================================================
        // GetDonHangHienTai
        // ============================================================

        public DataTable GetDonHangHienTai(
            string tenBan)
        {
            _db.ValidateTableName(tenBan);

            return _db.LoadData(
                $"SELECT STT, MAHANG, LOT, STATUS, STATUSDOC " +
                $"FROM [{tenBan}] " +
                $"ORDER BY STT");
        }

        // ============================================================
        // XoaTmpPhieu
        // ============================================================

        public void XoaTmpPhieu(
            string tenBan)
        {
            _db.ValidateTableName(tenBan);

            _db.ExecuteNonQuery(
                $"DELETE FROM [{tenBan}]");
        }

        // ============================================================
        // XoaDocQRCode
        // ============================================================

        public void XoaDocQRCode(
            string docQRTable)
        {
            _db.ValidateTableName(docQRTable);

            _db.ExecuteNonQuery(
                $"DELETE FROM [{docQRTable}]");
        }

        // ============================================================
        // GetTrangThaiDangBan
        // ============================================================

        public TrangThaiBan GetTrangThaiDangBan(
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return GetTrangThaiDangBan(
                tables.TmpTable,
                tables.DocQRTable);
        }

        public TrangThaiBan GetTrangThaiDangBan(
            string tmpTable,
            string docQRTable)
        {
            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(docQRTable);

            var result = new TrangThaiBan();

            object demQRRaw = _db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{docQRTable}]");

            int demQR = PhieuSqlExecutor.SafeInt(demQRRaw);

            if (demQR == 0)
            {
                result.DangBan = false;
                return result;
            }

            object demPhieuRaw = _db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{tmpTable}]");

            int demPhieu =
                PhieuSqlExecutor.SafeInt(demPhieuRaw);

            if (demPhieu == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataTable dt = _db.LoadData(
                $"SELECT TOP 1 " +
                $"ADDNM, NGAYGIAO, GIOGIAO, NHAMAY " +
                $"FROM [{tmpTable}]");

            if (dt.Rows.Count == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataRow row = dt.Rows[0];

            result.DangBan = true;
            result.DataKhongKhop = false;

            result.AddNM =
                row["ADDNM"] == DBNull.Value
                    ? 1
                    : Convert.ToInt32(row["ADDNM"]);

            result.NhaMay =
                row["NHAMAY"] == DBNull.Value
                    ? ""
                    : row["NHAMAY"]
                        .ToString()
                        .Trim();

            result.NgayGiao =
                row["NGAYGIAO"] == DBNull.Value
                    ? ""
                    : Convert.ToDateTime(
                        row["NGAYGIAO"])
                        .ToString("yyyy-MM-dd");

            string gioDon =
                row["GIOGIAO"] == DBNull.Value
                    ? ""
                    : row["GIOGIAO"]
                        .ToString()
                        .Trim();

            if (gioDon.Length == 1)
                gioDon = "0" + gioDon;

            result.GioGiaoFCC = gioDon;

            return result;
        }

        // ============================================================
        // GetTrangThaiDangBanYMVN
        // ============================================================

        public TrangThaiBan GetTrangThaiDangBanYMVN(
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return GetTrangThaiDangBanYMVN(
                tables.TmpTable,
                tables.DocQRTable);
        }

        public TrangThaiBan GetTrangThaiDangBanYMVN(
            string tmpTable,
            string docQRTable)
        {
            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(docQRTable);

            var result = new TrangThaiBan();

            object demTmpRaw = _db.ExecuteScalar(
                $"SELECT COUNT(*) " +
                $"FROM [{tmpTable}] " +
                $"WHERE ADDNM = 0");

            int demTmp =
                PhieuSqlExecutor.SafeInt(demTmpRaw);

            if (demTmp == 0)
            {
                result.DangBan = false;
                return result;
            }

            int demQR =
                PhieuSqlExecutor.SafeInt(
                    _db.ExecuteScalar(
                        $"SELECT COUNT(*) " +
                        $"FROM [{docQRTable}]"));

            if (demQR == 0)
            {
                result.DangBan = false;
                return result;
            }

            DataTable dt = _db.LoadData(
                $"SELECT TOP 1 NGAYGIAO, GIOGIAO " +
                $"FROM [{tmpTable}]");

            if (dt.Rows.Count == 0)
            {
                result.DangBan = false;
                return result;
            }

            DataRow row = dt.Rows[0];

            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = 1;
            result.NhaMay = "YAMAHA - VIET NAM";

            string ngayRaw =
                row["NGAYGIAO"]?.ToString() ?? "";

            result.NgayGiao =
                ngayRaw.Length >= 10
                    ? ngayRaw.Substring(0, 10)
                    : ngayRaw;

            result.GioGiaoFCC =
                row["GIOGIAO"]?
                    .ToString()
                    .Trim() ?? "";

            return result;
        }

        // ============================================================
        // EnsureTablesExist
        // ============================================================

        public void EnsureTablesExist()
        {
            string[] tables =
            {
            "IFSPHIEUGIAOHANG",
            "IFSPHIEUGIAOHANGView"
        };

            const string createSql =
                "IF NOT EXISTS (" +
                " SELECT * FROM sys.objects " +
                " WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') " +
                " AND type = 'U'" +
                ") " +
                "CREATE TABLE [{0}] (" +
                " STT INT, " +
                " MAHANG NVARCHAR(50), " +
                " TENHANG NVARCHAR(100), " +
                " SOLUONG INT, " +
                " NGAYGIAO SMALLDATETIME, " +
                " GIOGIAO NVARCHAR(50), " +
                " GIOGIAOFCC NVARCHAR(200), " +
                " NHAMAY NVARCHAR(100), " +
                " ADDNM INT, " +
                " LOT NVARCHAR(500), " +
                " STATUS NVARCHAR(50), " +
                " STATUSDOC NVARCHAR(50)" +
                ")";

            foreach (string table in tables)
            {
                _db.ExecuteNonQuery(
                    string.Format(
                        createSql,
                        table));
            }
        }

        // ============================================================
        // Private
        // ============================================================

        private void ConvertDateTimeColumns(
            DataTable donHang)
        {
            var dateTimeColumns =
                donHang.Columns
                    .Cast<DataColumn>()
                    .Where(c =>
                        c.DataType == typeof(DateTime))
                    .Select(c => c.ColumnName)
                    .ToList();

            foreach (string columnName
                     in dateTimeColumns)
            {
                string tempName =
                    columnName + "_STR";

                donHang.Columns.Add(
                    tempName,
                    typeof(string));

                foreach (DataRow row
                         in donHang.Rows)
                {
                    row[tempName] =
                        row[columnName] == DBNull.Value
                            ? ""
                            : ((DateTime)row[columnName])
                                .ToString("yyyy-MM-dd");
                }

                donHang.Columns.Remove(columnName);

                donHang.Columns[tempName]
                    .ColumnName = columnName;
            }
        }

        private void GuardDeleteTmp(
            string tmpTable,
            string docQRTable)
        {
            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(docQRTable);

            object tmpExistsRaw =
                _db.ExecuteScalar(
                    "SELECT COUNT(*) " +
                    "FROM sys.objects " +
                    "WHERE object_id = OBJECT_ID(N'[dbo].[" +
                    tmpTable +
                    "]') " +
                    "AND type = 'U'");

            int tmpExists =
                PhieuSqlExecutor.SafeInt(
                    tmpExistsRaw);

            if (tmpExists != 1)
                return;

            object docQRExistsRaw =
                _db.ExecuteScalar(
                    "SELECT COUNT(*) " +
                    "FROM sys.objects " +
                    "WHERE object_id = OBJECT_ID(N'[dbo].[" +
                    docQRTable +
                    "]') " +
                    "AND type = 'U'");

            int docQRExists =
                PhieuSqlExecutor.SafeInt(
                    docQRExistsRaw);

            if (docQRExists == 1)
            {
                int demDocQR =
                    PhieuSqlExecutor.SafeInt(
                        _db.ExecuteScalar(
                            $"SELECT COUNT(*) " +
                            $"FROM [{docQRTable}]"));

                if (demDocQR > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[LuuVaLoad] SKIP DELETE " +
                        $"[{tmpTable}] — đang có " +
                        $"{demDocQR} dòng trong " +
                        $"[{docQRTable}]");

                    return;
                }
            }

            _db.ExecuteNonQuery(
                $"DELETE FROM [{tmpTable}]");

            System.Diagnostics.Debug.WriteLine(
                $"[LuuVaLoad] Đã DELETE [{tmpTable}]");
        }
    }
}
