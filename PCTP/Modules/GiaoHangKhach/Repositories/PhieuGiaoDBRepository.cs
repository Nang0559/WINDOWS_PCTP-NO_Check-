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
    public sealed class PhieuGiaoDBRepository :SqlRepositoryBase, IPhieuGiaoDBRepository
    {
       

        public PhieuGiaoDBRepository(PhieuSqlExecutor db,
            IUnitOfWork uow):base(db, uow) { }
      

        // ============================================================
        // IPhieuGiaoDBRepository
        // ============================================================

        public DataTable GetDanhSachMaHang()
        {
            const string sql = @"
                    SELECT
                        ID,
                        Code,
                        Name
                    FROM B20Item
                    WHERE LEN(Code) > 10
                    GROUP BY
                        ID,
                        Code,
                        Name
                    ORDER BY ID";

            return LoadData(sql);
        }

        public DataTable LoadTmpPhieuGiaoDB(string tenBan, DateTime ngayGiao, int addNm)
        {
            Db.ValidateTableName(tenBan);

            string sql = $@"
        SELECT
            '' AS IDP,
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
            ISNULL(Note, '') AS Note,
            ISNULL(PO_NO, '')   AS PO_NO,
            ISNULL(PO_ITEM, '') AS PO_ITEM
        FROM [{tenBan}]
        WHERE CAST(NGAYGIAO AS DATE) = @ngayGiao
          AND ADDNM = @addNm";   // ← SỬA: thêm lọc nhà máy + ngày giao

            return LoadData(sql,
                new SqlParameter("@ngayGiao", ngayGiao.Date),
                new SqlParameter("@addNm", addNm));
        }

        public void LuuGiaoDB(
            DataTable donHang,
            string gioFccMoTa,
            int addNm,
            string tmpTable,
            string ifsTable,
            string nhaMayOverride = "")
        {
            if (donHang == null)
                throw new ArgumentNullException(nameof(donHang));

            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(ifsTable);

            // ========================================================
            // 1. Tạo / reset bảng IFS
            // ========================================================

            Db.DropCreate(
                ifsTable,
                donHang);

            // ========================================================
            // 2. Bulk insert đơn hàng vào bảng IFS
            // ========================================================

            Db.BulkInsertDataTable(
                ifsTable,
                donHang);

            // ========================================================
            // 3. Xác định nhà máy
            // ========================================================

            string nhaMay;

            if (!string.IsNullOrWhiteSpace(nhaMayOverride))
            {
                nhaMay = nhaMayOverride;
            }
            else
            {
                nhaMay = addNm == 1
                    ? "HON DA - VIET NAM(NHA MAY VP)"
                    : "HON DA - VIET NAM(NHA MAY HA NAM)";
            }

            // ========================================================
            // 4. Gọi SP load phiếu
            // ========================================================

            var tables = new PhieuTableSet(
                tmpTable,
                ifsTable,
                "DOCQRCODE");

            Db.CallPhieuSP(
                "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                DateTime.Now.ToString("yyyy-MM-dd"),
                nhaMay,
                gioFccMoTa,
                addNm,
                tables);

            // ========================================================
            // 5. Đồng bộ trạng thái TMPPHIEUGIAOHANGDBCT
            // ========================================================

            string sql = $@"
                UPDATE D
                SET
                    D.GGFCC     = T.GIOGIAO,
                    D.LOT       = T.LOT,
                    D.NGAYGIAO  = T.NGAYGIAO,
                    D.STATUS    = 'OK'
                FROM [{tmpTable}] T
                INNER JOIN TMPPHIEUGIAOHANGDBCT D
                    ON D.MAHANG = T.MAHANG
                    AND D.IDP =
                        SUBSTRING(
                            T.TTPHIEU,
                            CHARINDEX('-', T.TTPHIEU) + 1,
                            LEN(T.TTPHIEU)
                        )
                    AND D.STATUS = 'NG'
                    AND T.LOT <> ''";

            Db.ExecuteNonQuery(sql);
        }

        public DataTable BuildDonHangTuUpload()
        {
            const string sql = @"
        SELECT
            D.IDP,
            H.NgayLap,
            H.NHAMAY  AS ADDNM_HEADER,   -- TMPPHIEUNHANDB.NHAMAY đã lưu SẴN dạng mã 1/2
            D.MaHang  AS MAHANG,
            D.TenHang AS TENHANG,
            D.SoLuong AS SOLUONG,
            D.GioGiao AS GIOGIAO,
            D.NhaMay  AS NHAMAY,          -- text hiển thị, lưu riêng ở bảng chi tiết
            D.CUA,
            D.TRUYEN
        FROM TMPPHIEUGIAOHANGDBCT D
        INNER JOIN TMPPHIEUNHANDB H ON H.IDP = D.IDP
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
                row["NGAYGIAO"] = r["NgayLap"] == DBNull.Value ? (object)DateTime.Now : r["NgayLap"];
                row["GIOGIAO"] = r["GIOGIAO"]?.ToString() ?? "";
                row["STATUS"] = "NG";
                // Định dạng "PREFIX-IDP": SP dùng CHARINDEX('-', TTPHIEU)+1 để lấy IDP —
                // prefix cố định "GIAODB" (không chứa dấu '-') để tránh parse sai nếu Name có gạch ngang.
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

      
        public int SinhIDPMoi()
        {
            object kq = ExecuteScalar("SELECT ISNULL(MAX(IDP), 0) + 1 FROM TMPPHIEUNHANDB");
            return kq == null || kq == DBNull.Value ? 1 : Convert.ToInt32(kq);
        }

        public void UploadChiTietGiaoDB(DataTable chiTiet, bool xoaCuTruoc)
        {
            if (chiTiet == null || chiTiet.Rows.Count == 0)
                throw new ArgumentException("Không có dữ liệu để upload.", nameof(chiTiet));

            if (xoaCuTruoc)
                ExecuteNonQuery("DELETE FROM TMPPHIEUGIAOHANGDBCT");

            foreach (DataRow row in chiTiet.Rows)
            {
                ExecuteNonQuery(
                    "INSERT INTO TMPPHIEUGIAOHANGDBCT " +
                    "(IDP, MaHang, TenHang, SoLuong, GioGiao, NhaMay, CUA, TRUYEN, Status, TTNHAN) " +
                    "VALUES (@idp,@ma,@ten,@sl,@gio,@nm,@cua,@tr,'NG',1)",
                    new SqlParameter("@idp", row["IDP"]),
                    new SqlParameter("@ma", row["MaHang"]),
                    new SqlParameter("@ten", row["TenHang"]),
                    new SqlParameter("@sl", row["SoLuong"]),
                    new SqlParameter("@gio", row["GioGiao"]),
                    new SqlParameter("@nm", row["NhaMay"]),
                    new SqlParameter("@cua", row["CUA"]),
                    new SqlParameter("@tr", row["TRUYEN"]));
            }

            var idpGroups = chiTiet.AsEnumerable().GroupBy(r => r["IDP"].ToString());
            foreach (var grp in idpGroups)
            {
                string idp = grp.Key;
                var first = grp.First();
                string name = first["Name"].ToString();
                object ngayLap = first["NgayLap"];
                string nhaMay = first["NhaMay"].ToString();
                int addNM = nhaMay.Contains("HA NAM") ? 2 : 1;

                ExecuteNonQuery(
                    "IF NOT EXISTS (SELECT 1 FROM TMPPHIEUNHANDB WHERE IDP=@idp) " +
                    "INSERT INTO TMPPHIEUNHANDB (IDP, Name, NgayLap, NHAMAY) " +
                    "VALUES (@idp, @name, @ngay, @nm)",
                    new SqlParameter("@idp", idp),
                    new SqlParameter("@name", name),
                    new SqlParameter("@ngay", ngayLap == DBNull.Value ? (object)DBNull.Value : ngayLap),
                    new SqlParameter("@nm", addNM));
            }
        }
    }
}
