using PCTP.Domain.Entities;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuTmpRepository : SqlRepositoryBase, IPhieuTmpRepository
    {
        public PhieuTmpRepository(PhieuSqlExecutor db, IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // LoadPhieuDocQR
        // ============================================================
        public DataTable LoadPhieuDocQR(
            string ngayGiao, string nhaMay, string gioFcc, int addNm, PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            Db.ValidateTableName(tables.TmpTable);
            Db.ValidateTableName(tables.SourceTable);
            Db.ValidateTableName(tables.DocQRTable);

            DataTable tt = LoadData(
                $"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAOFCC, NHAMAY FROM [{tables.SourceTable}]");

            if (tt.Rows.Count > 0)
            {
                DataRow row = tt.Rows[0];
                ngayGiao = row["NGAYGIAO"]?.ToString() ?? "";
                nhaMay = row["NHAMAY"]?.ToString() ?? "";
                gioFcc = row["GIOGIAOFCC"]?.ToString() ?? "";
                addNm = DbValueHelper.ToInt(row["ADDNM"]);
            }

            return Db.CallPhieuSP("Usp_Qrcode_LOAD_PHIEU_DOCQR2405", ngayGiao, nhaMay, gioFcc, addNm, tables);
        }

        public DataTable LoadPhieuDocQR(
            string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tmpTable, string ifsTable, string docQRTable)
        {
            return LoadPhieuDocQR(ngayGiao, nhaMay, gioFcc, addNm,
                new PhieuTableSet(tmpTable, ifsTable, docQRTable));
        }

        // ============================================================
        // LuuVaLoad
        // ============================================================
        public DataTable LuuVaLoad(
            PhieuTableSet tables, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (donHang == null) throw new ArgumentNullException(nameof(donHang));

            Db.ValidateTableName(tables.TmpTable);
            Db.ValidateTableName(tables.SourceTable);
            Db.ValidateTableName(tables.DocQRTable);

            ConvertDateTimeColumns(donHang);

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                SWLog.Measure($"4b. DropCreate [{tables.SourceTable}]",
                    () => Db.DropCreate(tables.SourceTable, donHang));

                SWLog.Measure($"4c. BulkInsert {donHang.Rows.Count} rows → [{tables.SourceTable}]",
                    () => Db.BulkInsert(tables.SourceTable, donHang));

                SWLog.Measure($"4d. Guard DELETE [{tables.TmpTable}]",
                    () => GuardDeleteTmp(tables.TmpTable, tables.DocQRTable));

                var result = SWLog.Measure($"4e. CallSP [{tenSP}]",
                    () => Db.CallPhieuSP(tenSP, ngayGiao, nhaMay, gioFcc, addNm, tables));

                if (ownTransaction) Uow.Commit();
                return result;
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }

        public DataTable LuuVaLoad(
            string tenSPBang, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tenBan, string docQRTable, string ifsView = "")
        {
            var tables = new PhieuTableSet(tenBan, tenSPBang, docQRTable, tenBan, ifsView);
            return LuuVaLoad(tables, tenSP, donHang, ngayGiao, nhaMay, gioFcc, addNm);
        }

        // ============================================================
        // LoadTuTmpTable
        // ============================================================
        public DataTable LoadTuTmpTable(string tmpTable)
        {
            Db.ValidateTableName(tmpTable);
            string sql = $@"
SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO,
       STATUS, TTPHIEU, NHAMAY, ADDNM, HOP, STATUSDOC, Note,
       ISNULL(PO_NO, '') AS PO_NO, ISNULL(PO_ITEM, '') AS PO_ITEM
FROM [{tmpTable}]
ORDER BY TRY_CAST(STT AS INT), STT";
            return LoadData(sql);
        }

        // ============================================================
        // GetDonHangHienTai
        // ============================================================
        public DataTable GetDonHangHienTai(string tenBan)
        {
            Db.ValidateTableName(tenBan);
            return LoadData($"SELECT STT, MAHANG, LOT, STATUS, STATUSDOC FROM [{tenBan}] ORDER BY STT");
        }

        // ============================================================
        // XoaTmpPhieu / XoaDocQRCode
        // ============================================================
        public void XoaTmpPhieu(string tenBan)
        {
            Db.ValidateTableName(tenBan);
            ExecuteNonQuery($"DELETE FROM [{tenBan}]");
        }

        public void XoaDocQRCode(string docQRTable)
        {
            Db.ValidateTableName(docQRTable);
            ExecuteNonQuery($"DELETE FROM [{docQRTable}]");
        }

        // ============================================================
        // GetTrangThaiDangBan
        // ============================================================
        public TrangThaiBan GetTrangThaiDangBan(PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return GetTrangThaiDangBan(tables.TmpTable, tables.DocQRTable);
        }

        public TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            var result = new TrangThaiBan();

            int demQR = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]"));
            if (demQR == 0) { result.DangBan = false; return result; }

            int demPhieu = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{tmpTable}]"));
            if (demPhieu == 0) { result.DangBan = true; result.DataKhongKhop = true; return result; }

            DataTable dt = LoadData($"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAO, NHAMAY FROM [{tmpTable}]");
            if (dt.Rows.Count == 0) { result.DangBan = true; result.DataKhongKhop = true; return result; }

            DataRow row = dt.Rows[0];
            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = row["ADDNM"] == DBNull.Value ? 1 : Convert.ToInt32(row["ADDNM"]);
            result.NhaMay = row["NHAMAY"] == DBNull.Value ? "" : row["NHAMAY"].ToString().Trim();
            result.NgayGiao = row["NGAYGIAO"] == DBNull.Value ? "" : Convert.ToDateTime(row["NGAYGIAO"]).ToString("yyyy-MM-dd");

            string gioDon = row["GIOGIAO"] == DBNull.Value ? "" : row["GIOGIAO"].ToString().Trim();
            if (gioDon.Length == 1) gioDon = "0" + gioDon;
            result.GioGiaoFCC = gioDon;

            return result;
        }

        // ============================================================
        // GetTrangThaiDangBanYMVN
        // ============================================================
        public TrangThaiBan GetTrangThaiDangBanYMVN(PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return GetTrangThaiDangBanYMVN(tables.TmpTable, tables.DocQRTable);
        }

        public TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            var result = new TrangThaiBan();

            int demTmp = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{tmpTable}] WHERE ADDNM = 0"));
            if (demTmp == 0) { result.DangBan = false; return result; }

            int demQR = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]"));
            if (demQR == 0) { result.DangBan = false; return result; }

            DataTable dt = LoadData($"SELECT TOP 1 NGAYGIAO, GIOGIAO FROM [{tmpTable}]");
            if (dt.Rows.Count == 0) { result.DangBan = false; return result; }

            DataRow row = dt.Rows[0];
            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = 1;
            result.NhaMay = "YAMAHA - VIET NAM";

            string ngayRaw = row["NGAYGIAO"]?.ToString() ?? "";
            result.NgayGiao = ngayRaw.Length >= 10 ? ngayRaw.Substring(0, 10) : ngayRaw;
            result.GioGiaoFCC = row["GIOGIAO"]?.ToString().Trim() ?? "";

            return result;
        }

        // ============================================================
        // EnsureTablesExist
        // ============================================================
        public void EnsureTablesExist()
        {
            string[] tables = { "IFSPHIEUGIAOHANG", "IFSPHIEUGIAOHANGView" };
            const string createSql =
                "IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type = 'U') " +
                "CREATE TABLE [{0}] (STT INT, MAHANG NVARCHAR(50), TENHANG NVARCHAR(100), SOLUONG INT, " +
                "NGAYGIAO SMALLDATETIME, GIOGIAO NVARCHAR(50), GIOGIAOFCC NVARCHAR(200), NHAMAY NVARCHAR(100), " +
                "ADDNM INT, LOT NVARCHAR(500), STATUS NVARCHAR(50), STATUSDOC NVARCHAR(50))";

            foreach (string table in tables)
                ExecuteNonQuery(string.Format(createSql, table));
        }

        // ============================================================
        // Private
        // ============================================================
        private void ConvertDateTimeColumns(DataTable donHang)
        {
            var dateTimeColumns = donHang.Columns.Cast<DataColumn>()
                .Where(c => c.DataType == typeof(DateTime))
                .Select(c => c.ColumnName)
                .ToList();

            foreach (string columnName in dateTimeColumns)
            {
                string tempName = columnName + "_STR";
                donHang.Columns.Add(tempName, typeof(string));
                foreach (DataRow row in donHang.Rows)
                    row[tempName] = row[columnName] == DBNull.Value ? "" : ((DateTime)row[columnName]).ToString("yyyy-MM-dd");
                donHang.Columns.Remove(columnName);
                donHang.Columns[tempName].ColumnName = columnName;
            }
        }

        private void GuardDeleteTmp(string tmpTable, string docQRTable)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            int tmpExists = DbValueHelper.ToInt(ExecuteScalar(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[" + tmpTable + "]') AND type = 'U'"));
            if (tmpExists != 1) return;

            int docQRExists = DbValueHelper.ToInt(ExecuteScalar(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[" + docQRTable + "]') AND type = 'U'"));

            if (docQRExists == 1)
            {
                int demDocQR = DbValueHelper.ToInt(ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]"));
                if (demDocQR > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LuuVaLoad] SKIP DELETE [{tmpTable}] — đang có {demDocQR} dòng trong [{docQRTable}]");
                    return;
                }
            }

            ExecuteNonQuery($"DELETE FROM [{tmpTable}]");
            System.Diagnostics.Debug.WriteLine($"[LuuVaLoad] Đã DELETE [{tmpTable}]");
        }
    }
}