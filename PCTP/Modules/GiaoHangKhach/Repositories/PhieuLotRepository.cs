using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    /// <summary>
    /// Cấp phát/thu hồi số LOT khi bắn QR và truy vấn LOT tồn kho cho giao hàng.
    /// </summary>
    public sealed class PhieuLotRepository
    : SqlRepositoryBase,
      IPhieuLotRepository
    {
        public PhieuLotRepository(PhieuSqlExecutor sql, IUnitOfWork unitOfWork)
            : base(sql, unitOfWork)
        {
        }

        public string GetLotNo(string maHang, int stt, int dem, int slGiao, PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return GetLotNo(maHang, stt, dem, slGiao, tables.DocQRTable, tables.TmpTable);
        }

        public string GetLotNo(string maHang, int stt, int dem, int slGiao, string docQRTable = "DOCQRCODE", string tmpTable = "TMPPHIEUGIAOHANG")
        {
            Db.ValidateTableName(docQRTable);
            Db.ValidateTableName(tmpTable);

            DataTable dt = Db.ExecuteStoredProcedure(
                "Usp_Qrcode_Take_Lot2405",
                new SqlParameter("@_MaFCC", maHang ?? ""),
                new SqlParameter("@_STTP", stt),
                new SqlParameter("@_DeM", dem),
                new SqlParameter("@_SLGIAO", slGiao),
                new SqlParameter("@DOCQRTABLE", docQRTable),
                new SqlParameter("@TMPTABLE", tmpTable));

            if (dt == null || dt.Rows.Count == 0) return "";

            var parts = new List<string>();
            foreach (DataRow row in dt.Rows)
            {
                string lotFcc = row["LOTFCC"]?.ToString()?.Trim() ?? "";
                string fcc = row["FCC"]?.ToString()?.Trim() ?? "";
                parts.Add($"{lotFcc}-{fcc}");
            }
            return string.Join(",", parts);
        }

        public void CapNhapLotTmpPhieu(int stt, string lot, string tenBan)
        {
            if (stt <= 0 || string.IsNullOrWhiteSpace(lot)) return;
            Db.ValidateTableName(tenBan);
            Db.ExecuteNonQuery(
                $"UPDATE [{tenBan}] SET LOT = @lot WHERE STT = @stt",
                new SqlParameter("@lot", lot),
                new SqlParameter("@stt", stt));
        }

        public void LayLaiLotNo(int stt, PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            LayLaiLotNo(stt, tables.TmpTable, tables.DocQRTable);
        }

        public void LayLaiLotNo(int stt, string tenBan, string docQRTable)
        {
            if (stt <= 0) return;
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            // A FIFO release is valid only for the exact delivery STT that still
            // owns a current QR row. This prevents a focused GridView row from
            // being released when it is not the row that violated FIFO.
            int linkedQrCount = Convert.ToInt32(Db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{docQRTable}] WHERE ISNULL(STTBAN, 0) = @stt",
                new SqlParameter("@stt", stt)) ?? 0);

            if (linkedQrCount <= 0)
                return;

            // This method is called only after the user explicitly confirmed
            // the FIFO violations. Do not add STATUS guards here: a confirmed
            // violation must be released deterministically for this exact STT.
            Db.ExecuteNonQuery(
                $"UPDATE [{tenBan}] " +
                "SET LOT = '', STATUS = 'NG', STATUSDOC = 'NG', TTPHIEU = NULL " +
                "WHERE STT = @stt",
                new SqlParameter("@stt", stt));

            // Keep the physical QR scan for traceability, but detach it from the
            // delivery row and mark it NG. It is then excluded from the current
            // CNK candidates and cannot be consumed by Usp_Qrcode_Update_Stock2405.
            Db.ExecuteNonQuery(
                $"UPDATE [{docQRTable}] " +
                "SET GIO = NULL, KETQUA = 'NG', STTBAN = NULL " +
                "WHERE ISNULL(STTBAN, 0) = @stt",
                new SqlParameter("@stt", stt));
        }

        public DataTable LoadGhepLot(string tenBan, string ifsTable)
            => ExecuteStoredProcedure("Usp_Qrcode_gheplot",
                new SqlParameter("@TMPTABLE", tenBan),
                new SqlParameter("@IFSTABLE", ifsTable));

        // FIFO source for the manual LOT picker.
        // FIFO_RANK is calculated from STOCKTP itself, so the UI does not
        // depend on a separate configuration table that may not exist in the
        // production database.
        public DataTable GetDanhSachLotTuKho(string maHang)
        {
            const int keyLen = PCTP.Common.LotCodeHelper.LEN_LEGACY_KEY;
            string sql = $@"
;WITH StockLot AS
(
    SELECT
        PART,
        LEFT(LOT, {keyLen}) AS LOTKEY,
        MIN(LOT) AS LOTDISPLAY,
        SUM(ISNULL(SLCONLAI, 0)) AS SLCONLAI,
        SUM(ISNULL(SLXUAT, 0)) AS SLXUAT,
        MIN(NAME) AS NAME
    FROM STOCKTP
    WHERE PART = @ma
      AND ISNULL(SLCONLAI, 0) > 0
      AND LEN(ISNULL(LOT, '')) >= {keyLen}
    GROUP BY PART, LEFT(LOT, {keyLen})
),
Fifo AS
(
    SELECT
        PART,
        LOTKEY,
        LOTDISPLAY,
        SLCONLAI,
        SLXUAT,
        NAME,
        ROW_NUMBER() OVER
        (
            PARTITION BY PART
            ORDER BY
                LEFT(LOTKEY, 6) ASC,
                CASE SUBSTRING(LOTKEY, 12, 1)
                    WHEN '0' THEN 0
                    WHEN '1' THEN 1
                    WHEN '2' THEN 2
                    WHEN '3' THEN 3
                    ELSE 9
                END ASC,
                LOTKEY ASC
        ) AS FIFO_RANK
    FROM StockLot
)
SELECT
    LOTDISPLAY AS LOT,
    SLCONLAI,
    SLXUAT,
    PART,
    NAME,
    FIFO_RANK,
    CAST(1 AS bit) AS FIFO_REQUIRED
FROM Fifo
ORDER BY FIFO_RANK;";

            return Db.LoadData(sql, new SqlParameter("@ma", maHang ?? ""));
        }

        public DataTable TakeLotYMVN(string tmpTable, string docQRTable, bool isLoaiSP)
        {
            var ds = Db.ExecuteStoredProcedureDataSet(
                "Usp_Qrcode_Take_LotYMVN2405",
                new SqlParameter("@TMPTABLE", tmpTable),
                new SqlParameter("@DOCQRTABLE", docQRTable),
                new SqlParameter("@ISLOAIASP", isLoaiSP ? 1 : 0));
            return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
        }
    }
}