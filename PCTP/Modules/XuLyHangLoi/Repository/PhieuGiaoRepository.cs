using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public sealed class PhieuGiaoRepository : SqlRepositoryBase, IPhieuGiaoRepository
    {
        public PhieuGiaoRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        public List<PhieuGiaoUngVienInfo> TimTheoLot(string lotNo)
        {
            // Fuzzy-match giống GetLichSuGiaoHangTheoLot đã có — LOT trong LUUPHIEUGIAOHANG
            // là chuỗi ghép "LOTA-100,LOTB-50" nên phải STRING_SPLIT rồi so khớp qua LotCodeHelper.
            string lotValueExpr = "LTRIM(RTRIM(LEFT(part.value, CHARINDEX('-', part.value + '-') - 1)))";
            string match = LotCodeHelper.BuildLotMatchSql(lotValueExpr, "@lot");
            string sql = $@"
            SELECT DISTINCT g.STT, g.LOT, g.MAHANG, g.TENHANG, g.SOLUONG, g.NGAYGIAO,
                   g.GIOGIAO, g.GIOGIAOFCC, g.NHAMAY, g.CUA, g.TRUYEN,
                   ISNULL(g.PO_NO,'') AS PO_NO, ISNULL(g.Note,'') AS Note
            FROM LUUPHIEUGIAOHANG g
            CROSS APPLY STRING_SPLIT(g.LOT, ',') part
            WHERE {match}
            ORDER BY g.NGAYGIAO DESC";
            DataTable dt = LoadData(sql, new SqlParameter("@lot", lotNo));
            return Map(dt);
        }

        public List<PhieuGiaoUngVienInfo> TimTheoMaHangNgayGiao(string maHang, DateTime ngayGiao)
        {
            string sql = @"
            SELECT STT, LOT, MAHANG, TENHANG, SOLUONG, NGAYGIAO, GIOGIAO, GIOGIAOFCC,
                   NHAMAY, CUA, TRUYEN, ISNULL(PO_NO,'') AS PO_NO, ISNULL(Note,'') AS Note
            FROM LUUPHIEUGIAOHANG
            WHERE MAHANG = @ma AND CAST(NGAYGIAO AS DATE) = @ng
            ORDER BY GIOGIAO";
            DataTable dt = LoadData(sql, new SqlParameter("@ma", maHang), new SqlParameter("@ng", ngayGiao.Date));
            return Map(dt);
        }

        public PhieuGiaoUngVienInfo GetByDinhDanhKey(string dinhDanhKey)
        {
            if (!DinhDanhKeyHelper.TryParse(dinhDanhKey, out var nhaMay, out var ngayGiao,
                    out var gioGiaoFcc, out var poNo, out var stt))
                return null;

            string sql = @"
            SELECT TOP 1 STT, LOT, MAHANG, TENHANG, SOLUONG, NGAYGIAO, GIOGIAO, GIOGIAOFCC,
                   NHAMAY, CUA, TRUYEN, ISNULL(PO_NO,'') AS PO_NO, ISNULL(Note,'') AS Note
            FROM LUUPHIEUGIAOHANG
            WHERE NHAMAY = @nm AND CAST(NGAYGIAO AS DATE) = @ng
              AND GIOGIAOFCC = @gg AND ISNULL(PO_NO,'') = @po AND STT = @stt";
            DataTable dt = LoadData(sql,
                new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao ?? (object)DBNull.Value),
                new SqlParameter("@gg", gioGiaoFcc),
                new SqlParameter("@po", poNo),
                new SqlParameter("@stt", stt));
            return dt.Rows.Count > 0 ? MapRow(dt.Rows[0]) : null;
        }

        public List<PhieuGiaoUngVienInfo> GetPhieuChoGiaoBu(string maHang)
        {
            string sql = @"
            SELECT STT, LOT, MAHANG, TENHANG, SOLUONG, NGAYGIAO, GIOGIAO, GIOGIAOFCC,
                   NHAMAY, CUA, TRUYEN, ISNULL(PO_NO,'') AS PO_NO, ISNULL(Note,'') AS Note
            FROM LUUPHIEUGIAOHANG
            WHERE MAHANG = @ma AND Note LIKE 'CHO_GIAO_BU:%'
            ORDER BY NGAYGIAO DESC";
            DataTable dt = LoadData(sql, new SqlParameter("@ma", maHang));
            return Map(dt);
        }

        private static List<PhieuGiaoUngVienInfo> Map(DataTable dt) => dt.Rows.Cast<DataRow>().Select(MapRow).ToList();

        private static PhieuGiaoUngVienInfo MapRow(DataRow r)
        {
            short stt = r["STT"] == DBNull.Value ? (short)0 : Convert.ToInt16(r["STT"]);
            string nhaMay = r["NHAMAY"] as string;
            DateTime? ngay = r["NGAYGIAO"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["NGAYGIAO"]);
            string gioFcc = r["GIOGIAOFCC"] as string;
            string po = r["PO_NO"] as string;

            return new PhieuGiaoUngVienInfo
            {
                STT = stt,
                DinhDanhKey = DinhDanhKeyHelper.Build(nhaMay, ngay, gioFcc, po, stt),
                LOT = r["LOT"] as string,
                MAHANG = r["MAHANG"] as string,
                TENHANG = r["TENHANG"] as string,
                SOLUONG = r["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(r["SOLUONG"]),
                NGAYGIAO = ngay,
                GIOGIAO = r["GIOGIAO"] as string,
                GIOGIAOFCC = gioFcc,
                NHAMAY = nhaMay,
                CUA = r["CUA"] as string,
                TRUYEN = r["TRUYEN"] as string,
                PO_NO = po,
                Note = r["Note"] as string
            };
        }
        // Thêm vào PhieuGiaoRepository
        public void CapNhatNotePhieuGiao(string dinhDanhKey, string note)
        {
            if (!DinhDanhKeyHelper.TryParse(dinhDanhKey, out var nhaMay, out var ngayGiao,
                    out var gioGiaoFcc, out var poNo, out var stt))
                throw new ArgumentException($"DinhDanhKey không hợp lệ: '{dinhDanhKey}'");

            ExecuteNonQuery(
                "UPDATE LUUPHIEUGIAOHANG SET Note = @note " +
                "WHERE NHAMAY = @nm AND CAST(NGAYGIAO AS DATE) = @ng " +
                "AND GIOGIAOFCC = @gg AND ISNULL(PO_NO,'') = @po AND STT = @stt",
                new SqlParameter("@note", note),
                new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao ?? (object)DBNull.Value),
                new SqlParameter("@gg", gioGiaoFcc),
                new SqlParameter("@po", poNo),
                new SqlParameter("@stt", stt));
        }
    }
}
