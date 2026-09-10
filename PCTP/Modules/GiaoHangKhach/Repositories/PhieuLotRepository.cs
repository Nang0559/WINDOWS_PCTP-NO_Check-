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
    /// Cấp phát/thu hồi số LOT khi bắn QR: sinh LOT mới theo mã hàng + STT + đếm, ghi lại
    /// vào bảng TMP, và "hoàn tác" (LayLaiLotNo) khi người dùng xoá dòng đã bắn nhầm.
    /// Implementation của <see cref="IPhieuLotRepository"/> — một trong các mảnh được
    /// <see cref="PhieuRepository"/> tổng hợp lại. Xem WORKFLOW_GIAOHANGKHACH.md mục 2.
    /// </summary>
    public sealed class PhieuLotRepository
    : SqlRepositoryBase,
      IPhieuLotRepository
    {
       

        public PhieuLotRepository(
            PhieuSqlExecutor sql,
            IUnitOfWork unitOfWork)
            : base(sql, unitOfWork)
        {
            //if (sql == null)
            //    throw new ArgumentNullException(nameof(sql));
            //if (unitOfWork == null)
            //    throw new ArgumentNullException(nameof(unitOfWork));
            //_db = sql;
        }

        // ============================================================
        // IPhieuLotRepository
        // ============================================================

        public string GetLotNo(
            string maHang,
            int stt,
            int dem,
            int slGiao,
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return GetLotNo(
                maHang,
                stt,
                dem,
                slGiao,
                tables.DocQRTable,
                tables.TmpTable);
        }

        public string GetLotNo(
            string maHang,
            int stt,
            int dem,
            int slGiao,
            string docQRTable = "DOCQRCODE",
            string tmpTable = "TMPPHIEUGIAOHANG")
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

                new SqlParameter("@TMPTABLE", tmpTable)
            );

            if (dt == null || dt.Rows.Count == 0)
                return "";

            var parts = new List<string>();

            foreach (DataRow row in dt.Rows)
            {
                string lotFcc =
                    row["LOTFCC"]?.ToString()?.Trim() ?? "";

                string fcc =
                    row["FCC"]?.ToString()?.Trim() ?? "";

                parts.Add($"{lotFcc}-{fcc}");
            }

            return string.Join(",", parts);
        }

        // ============================================================
        // CapNhapLotTmpPhieu
        // ============================================================

        public void CapNhapLotTmpPhieu(
            int stt,
            string lot,
            string tenBan)
        {
            if (stt <= 0)
                return;

            if (string.IsNullOrWhiteSpace(lot))
                return;

            Db.ValidateTableName(tenBan);

            Db.ExecuteNonQuery(
                $"UPDATE [{tenBan}] " +
                "SET LOT = @lot " +
                "WHERE STT = @stt",

                new SqlParameter("@lot", lot),

                new SqlParameter("@stt", stt)
            );
        }

        // ============================================================
        // LayLaiLotNo - PhieuTableSet
        // ============================================================

        public void LayLaiLotNo(
            int stt,
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            LayLaiLotNo(
                stt,
                tables.TmpTable,
                tables.DocQRTable);
        }

        // ============================================================
        // LayLaiLotNo - overload tương thích cũ
        // ============================================================

        public void LayLaiLotNo(
            int stt,
            string tenBan,
            string docQRTable)
        {
            if (stt <= 0)
                return;

            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            // --------------------------------------------------------
            // 1. Reset LOT trong bảng TMP
            // --------------------------------------------------------

            Db.ExecuteNonQuery(
                $"UPDATE [{tenBan}] " +
                "SET LOT = '', " +
                "    STATUSDOC = 'NG', " +
                "    TTPHIEU = NULL " +
                "WHERE STT = @stt " +
                "  AND ISNULL(STATUS, '') <> 'OK'",

                new SqlParameter("@stt", stt)
            );

            // --------------------------------------------------------
            // 2. Reset QRCode đã ghép với STT
            // --------------------------------------------------------

            Db.ExecuteNonQuery(
                $"UPDATE [{docQRTable}] " +
                "SET GIO = NULL, " +
                "    KETQUA = 'OK', " +
                "    STTBAN = NULL " +
                "WHERE ISNULL(STTBAN, 0) = @stt " +
                "  AND KETQUA = 'DG'",

                new SqlParameter("@stt", stt)
            );
        }

        // ============================================================
        // LoadGhepLot
        // ============================================================

        public DataTable LoadGhepLot(string tenBan, string ifsTable)
     => ExecuteStoredProcedure("Usp_Qrcode_gheplot",
         new SqlParameter("@TMPTABLE", tenBan),
         new SqlParameter("@IFSTABLE", ifsTable));

        // ============================================================
        // GetDanhSachLotTuKho
        // ============================================================

        public DataTable GetDanhSachLotTuKho(
            string maHang)
        {
            const string sql = @"
            SELECT
                LOT,
                SLCONLAI,
                SLXUAT,
                PART,
                NAME
            FROM STOCKTP
            WHERE PART = @ma
              AND SLCONLAI > 0
            ORDER BY LOT";

            return Db.LoadData(
                sql,
                new SqlParameter(
                    "@ma",
                    maHang ?? ""));
        }

        public DataTable TakeLotYMVN(string tmpTable, string docQRTable, bool isLoaiSP)
        {
            var ds = Db.ExecuteStoredProcedureDataSet( "Usp_Qrcode_Take_LotYMVN2405",
                new SqlParameter("@TMPTABLE", tmpTable),
                new SqlParameter("@DOCQRTABLE", docQRTable),
                new SqlParameter("@ISLOAISP", isLoaiSP ? 1 : 0));
            return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
        }
    }
}
