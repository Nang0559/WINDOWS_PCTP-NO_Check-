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
    public sealed class PhieuGiaoDBRepository : IPhieuGiaoDBRepository
    {
        private readonly PhieuSqlExecutor _db;

        public PhieuGiaoDBRepository(PhieuSqlExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

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

            return _db.LoadData(sql);
        }

        public DataTable LoadTmpPhieuGiaoDB(string tenBan)
        {
            _db.ValidateTableName(tenBan);

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

            return _db.LoadData(sql);
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

            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(ifsTable);

            // ========================================================
            // 1. Tạo / reset bảng IFS
            // ========================================================

            _db.DropCreate(
                ifsTable,
                donHang);

            // ========================================================
            // 2. Bulk insert đơn hàng vào bảng IFS
            // ========================================================

            _db.BulkInsert(
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

            _db.CallPhieuSP(
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

            _db.ExecuteNonQuery(sql);
        }
    }
}
