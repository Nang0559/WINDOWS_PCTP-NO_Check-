using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuGiaoDBRepository : SqlRepositoryBase, IPhieuGiaoDBRepository
    {
        public PhieuGiaoDBRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        // ============================================================
        // IPhieuGiaoDBRepository
        // ============================================================

        public DataTable GetDanhSachMaHang()
        {
            const string sql = @"
            SELECT ID, Code, Name
            FROM B20Item
            WHERE LEN(Code) > 10
            GROUP BY ID, Code, Name
            ORDER BY ID";
            return LoadData(sql);
        }

        public DataTable LoadTmpPhieuGiaoDB(string tenBan, DateTime ngayGiao, int addNm)
        {
            Db.ValidateTableName(tenBan);

            string sql = $@"
            SELECT
                '' AS IDP, STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV,
                SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU, NHAMAY, ADDNM,
                HOP, STATUSDOC, ISNULL(Note, '') AS Note,
                ISNULL(PO_NO, '') AS PO_NO, ISNULL(PO_ITEM, '') AS PO_ITEM
            FROM [{tenBan}]
            WHERE CAST(NGAYGIAO AS DATE) = @ngayGiao
              AND ADDNM = @addNm";

            return LoadData(sql,
                new SqlParameter("@ngayGiao", ngayGiao.Date),
                new SqlParameter("@addNm", addNm));
        }

        public void LuuGiaoDB(
            DataTable donHang, string gioFccMoTa, int addNm,
            string tmpTable, string ifsTable, string nhaMayOverride = "")
        {
            if (donHang == null) throw new ArgumentNullException(nameof(donHang));

            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(ifsTable);

            Db.DropCreate(ifsTable, donHang);
            Db.BulkInsertDataTable(ifsTable, donHang);

            string nhaMay = !string.IsNullOrWhiteSpace(nhaMayOverride)
                ? nhaMayOverride
                : (addNm == 1
                    ? "HON DA - VIET NAM(NHA MAY VP)"
                    : "HON DA - VIET NAM(NHA MAY HA NAM)");

            var tables = new PhieuTableSet(tmpTable, ifsTable, "DOCQRCODE");

            Db.CallPhieuSP(
                "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                DateTime.Now.ToString("yyyy-MM-dd"),
                nhaMay, gioFccMoTa, addNm, tables);

            string sql = $@"
            UPDATE D
            SET D.GGFCC = T.GIOGIAO, D.LOT = T.LOT, D.NGAYGIAO = T.NGAYGIAO, D.STATUS = 'OK'
            FROM [{tmpTable}] T
            INNER JOIN TMPPHIEUGIAOHANGDBCT D
                ON D.MAHANG = T.MAHANG
               AND D.IDP = SUBSTRING(T.TTPHIEU, CHARINDEX('-', T.TTPHIEU) + 1, LEN(T.TTPHIEU))
               AND D.STATUS = 'NG'
               AND T.LOT <> ''";

            ExecuteNonQuery(sql);   // ✅ đổi Db.ExecuteNonQuery → ExecuteNonQuery (base class) để tham gia Uow nếu có
        }

        // ✅ ĐÃ ĐỔI tên bảng TMPPHIEUNHANDB → TMPPHIEUGIAODBHD
        public DataTable BuildDonHangTuUpload()
        {
            const string sql = @"
            SELECT
                D.IDP,
                H.NGAYLAP,
                H.NHAMAY  AS ADDNM_HEADER,
                D.MaHang  AS MAHANG,
                D.TenHang AS TENHANG,
                D.SoLuong AS SOLUONG,
                D.GioGiao AS GIOGIAO,
                D.NhaMay  AS NHAMAY,
                D.CUA,
                D.TRUYEN
            FROM TMPPHIEUGIAOHANGDBCT D
            INNER JOIN TMPPHIEUGIAODBHD H ON H.IDP = D.IDP
            WHERE D.STATUS = 'NG'";

            DataTable raw = LoadData(sql);

            var dt = new DataTable();
            dt.Columns.Add("STT", typeof(string));
            dt.Columns.Add("CUA", typeof(string));
            dt.Columns.Add("TRUYEN", typeof(string));
            dt.Columns.Add("MAHANG", typeof(string));
            dt.Columns.Add("TENHANG", typeof(string));
            dt.Columns.Add("LOT", typeof(string));
            dt.Columns.Add("DV", typeof(string));
            dt.Columns.Add("SOLUONG", typeof(int));
            dt.Columns.Add("NGAYGIAO", typeof(DateTime));
            dt.Columns.Add("GIOGIAO", typeof(string));
            dt.Columns.Add("STATUS", typeof(string));
            dt.Columns.Add("TTPHIEU", typeof(string));
            dt.Columns.Add("NHAMAY", typeof(string));
            dt.Columns.Add("ADDNM", typeof(int));
            dt.Columns.Add("HOP", typeof(string));
            dt.Columns.Add("STATUSDOC", typeof(string));
            dt.Columns.Add("Note", typeof(string));
            dt.Columns.Add("PO_NO", typeof(string));
            dt.Columns.Add("PO_ITEM", typeof(string));

            int stt = 1;
            foreach (DataRow r in raw.Rows)
            {
                string idp = r["IDP"].ToString();
                var row = dt.NewRow();
                row["STT"] = (stt++).ToString();
                row["CUA"] = r["CUA"]?.ToString() ?? "";
                row["TRUYEN"] = r["TRUYEN"]?.ToString() ?? "";
                row["MAHANG"] = r["MAHANG"]?.ToString() ?? "";
                row["TENHANG"] = r["TENHANG"]?.ToString() ?? "";
                row["LOT"] = "";
                row["DV"] = "PCS";
                row["SOLUONG"] = DbValueHelper.SafeInt(r["SOLUONG"]);
                row["NGAYGIAO"] = r["NGAYLAP"] == DBNull.Value ? (object)DateTime.Now : r["NGAYLAP"];
                row["GIOGIAO"] = r["GIOGIAO"]?.ToString() ?? "";
                row["STATUS"] = "NG";
                row["TTPHIEU"] = $"GIAODB-{idp}";
                row["NHAMAY"] = r["NHAMAY"]?.ToString() ?? "";
                row["ADDNM"] = DbValueHelper.SafeInt(r["ADDNM_HEADER"]);
                row["HOP"] = "";
                row["STATUSDOC"] = "NG";
                row["Note"] = "";
                row["PO_NO"] = "";
                row["PO_ITEM"] = "";
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ✅ THAY THẾ HOÀN TOÀN SinhIDPMoi() + UploadChiTietGiaoDB() cũ.
        // Header tạo TRƯỚC (IDENTITY tự sinh, không tính tay MAX+1 nữa),
        // Detail tạo SAU cùng đúng IDP vừa sinh — tất cả trong 1 Uow transaction.
        public int TaoPhieuVaChiTietGiaoDB(
            string ten, DateTime ngayLap, int nhaMay, string nhaMayName,
            string note, DataTable chiTiet)
        {
            if (chiTiet == null || chiTiet.Rows.Count == 0)
                throw new ArgumentException("Chi tiết phiếu không được rỗng.", nameof(chiTiet));

            bool ownTransaction = !HasTransaction;
            if (ownTransaction) Uow.Begin();
            try
            {
                object idpObj = ExecuteScalar(@"
                INSERT INTO TMPPHIEUGIAODBHD
                    (NAME, TRANGTHAIGIAO, Note, NGAYLAP, NHAMAY, NHAMAYNAME)
                OUTPUT INSERTED.IDP
                VALUES (@ten, 0, @note, @ngayLap, @nhaMay, @nhaMayName)",
                    new SqlParameter("@ten", (object)ten ?? DBNull.Value),
                    new SqlParameter("@note", (object)note ?? DBNull.Value),
                    new SqlParameter("@ngayLap", ngayLap),
                    new SqlParameter("@nhaMay", nhaMay),
                    new SqlParameter("@nhaMayName", (object)nhaMayName ?? DBNull.Value));

                int idp = Convert.ToInt32(idpObj);

                foreach (DataRow row in chiTiet.Rows)
                {
                    ExecuteNonQuery(@"
                    INSERT INTO TMPPHIEUGIAOHANGDBCT
                        (IDP, MAHANG, TENHANG, SOLUONG, GIOGIAO, NHAMAY, CUA, TRUYEN, STATUS, TTNHAN)
                    VALUES
                        (@idp, @ma, @ten, @sl, @gio, @nm, @cua, @tr, 'NG', 1)",
                        new SqlParameter("@idp", idp),
                        new SqlParameter("@ma", row["MaHang"]),
                        new SqlParameter("@ten", row["TenHang"]),
                        new SqlParameter("@sl", row["SoLuong"]),
                        new SqlParameter("@gio", row["GioGiao"]),
                        new SqlParameter("@nm", nhaMayName),
                        new SqlParameter("@cua", row["CUA"]),
                        new SqlParameter("@tr", row["TRUYEN"]));
                }

                if (ownTransaction) Uow.Commit();
                return idp;
            }
            catch
            {
                if (ownTransaction) Uow.Rollback();
                throw;
            }
        }
    }
}
