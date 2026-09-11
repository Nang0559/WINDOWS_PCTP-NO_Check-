using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
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

        public DataTable LoadTmpPhieuGiaoDB(string tenBan)
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
                    Note,
                    ISNULL(PO_NO, '')   AS PO_NO,
                    ISNULL(PO_ITEM, '') AS PO_ITEM
                FROM [{tenBan}]";

            return LoadData(sql);
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
                ROW_NUMBER() OVER (PARTITION BY ct.IDP ORDER BY ct.ID) AS STT,
                ct.CUA,
                ct.TRUYEN,
                ct.MAHANG,
                ct.TENHANG,
                ISNULL(ct.LOT, '') AS LOT,
                ISNULL(ct.DV, '') AS DV,
                ct.SOLUONG,
                h.NgayLap AS NGAYGIAO,
                ct.GIOGIAO,
                ISNULL(ct.STATUS, 'NG') AS STATUS,
                CAST(ct.IDP AS NVARCHAR(20)) + '-' + ct.MAHANG AS TTPHIEU,
                ct.NHAMAY,
                CASE WHEN ct.NHAMAY LIKE '%HA NAM%' THEN 2 ELSE 1 END AS ADDNM,
                ISNULL(ct.HOP, 0) AS HOP,
                ISNULL(ct.STATUSDOC, 'NG') AS STATUSDOC
            FROM TMPPHIEUGIAOHANGDBCT ct
            INNER JOIN TMPPHIEUNHANDB h ON h.IDP = ct.IDP
            WHERE ISNULL(ct.STATUS, 'NG') = 'NG'";
            return LoadData(sql);
        }
    }
}
