using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Infrastructure.Repositories
{
    /// <summary>
    /// Toàn bộ thao tác DB cho bảng DOCQRCODE.
    /// </summary>
    //public class DocQRRepository : IDocQRRepository
    //{
    //    private readonly SQLPROVIDER _sql;
    //    private readonly CustomerConfig _cfg;
    //    public DocQRRepository(SQLPROVIDER sql, CustomerConfig cfg)
    //    {
    //        _sql = sql;
    //        _cfg = cfg;
    //    }
    //    // ════════════════════════════════════════════════════════════════
    //    // Helper dùng chung
    //    // ════════════════════════════════════════════════════════════════
    //    private static string Q(string table) => "[dbo].[" + table + "]";
    //    // ── Helper: lấy tên bảng đúng theo loại SP/MP ────────────────────────
    //    private string GetDocQRTable(bool isSP = false)
    //        => isSP && !string.IsNullOrEmpty(_cfg.DocQRTableSP)
    //            ? _cfg.DocQRTableSP
    //            : _cfg.DocQRTable;

    //    private string GetIfsTable(bool isSP = false)
    //        => isSP && !string.IsNullOrEmpty(_cfg.IfsTableSP)
    //            ? _cfg.IfsTableSP
    //            : _cfg.IfsTable;

    //    private string GetTmpTable(bool isSP = false)
    //        => isSP && !string.IsNullOrEmpty(_cfg.TmpTableSP)
    //            ? _cfg.TmpTableSP
    //            : _cfg.TmpTable;
    //    // ════════════════════════════════════════════════════════════════
    //    // Các method cũ — gọi xuống overload với table mặc định
    //    // (Giữ nguyên để không break code cũ)
    //    // ════════════════════════════════════════════════════════════════
    //    public IReadOnlyList<DocQRCode> GetAll() => GetAll("DOCQRCODE");
    //    public int GetMaxStt() => GetMaxStt("DOCQRCODE");
    //    public DataTable GetAllAsTable() => GetAllAsTable("DOCQRCODE");
    //    public int Count() => Count("DOCQRCODE");
    //    public int CountChuaDG() => CountChuaDG("DOCQRCODE");
    //    public void InsertFCC(DocQRCode item) => InsertFCC(item, "DOCQRCODE");
    //    public void UpdateHVN(DocQRCode item) => UpdateHVN(item, "DOCQRCODE");
    //    public void UpdateSlHvn(int stt, int slMoi) => UpdateSlHvn(stt, slMoi, "DOCQRCODE");
    //    public void Delete(int stt) => Delete(stt, "DOCQRCODE");
    //    public void DeleteAll() => DeleteAll("DOCQRCODE");
    //    public int GetTongSlDaBan(string ma) => GetTongSlDaBan(ma, "DOCQRCODE");
    //    public int GetSoLuongGiaoTheoMa(string ma) => GetSoLuongGiaoTheoMa(ma, "IFSPHIEUGIAOHANG");
    //    // ── Đọc ─────────────────────────────────────────────────────────────
    //    public IReadOnlyList<DocQRCode> GetAll(string docQrTable)
    //    {
    //        string query =
    //            $"SELECT STT, LOTFCC, MAHANGFCC, MAFCC, SLTEMFCC, " +
    //            $"       LOTHVN, MAHANGHVN, SLTEMHVN, GIO, KETQUA " +
    //            $"FROM {Q(docQrTable)} ORDER BY STT";

    //        DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, query);
    //        var list = new List<DocQRCode>(dt.Rows.Count);
    //        foreach (DataRow r in dt.Rows)
    //            list.Add(MapRow(r));
    //        return list;
    //    }

    //    public int GetMaxStt(string docQrTable)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT ISNULL(MAX(STT), 0) FROM {Q(docQrTable)}");
    //        return int.TryParse(raw, out int v) ? v : 0;
    //    }

    //    public DataTable GetAllAsTable(string docQrTable)
    //    {
    //        string query =
    //            $"SELECT STT, LOTFCC, MAHANGFCC, SLTEMFCC, " +
    //            $"       LOTHVN, MAHANGHVN, SLTEMHVN, GIO, SUALOTHVN, KETQUA " +
    //            $"FROM {Q(docQrTable)} ORDER BY STT";
    //        return _sql.ExecuteQuery(_sql.B7R2_FCCdb, query);
    //    }

    //    public int Count(string docQrTable)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT COUNT(*) FROM {Q(docQrTable)}");
    //        return int.TryParse(raw, out int v) ? v : 0;
    //    }

    //    public int CountChuaDG(string docQrTable)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT COUNT(*) FROM {Q(docQrTable)} " +
    //            $"WHERE ISNULL(KETQUA,'') <> 'DG'");
    //        return int.TryParse(raw, out int v) ? v : 0;
    //    }
    //    public string GetGearName(int gearCode)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT ISNULL(Name,'') FROM B20Gear WHERE Code = {gearCode}");
    //        return raw.Trim();
    //    }
    //    // ── Lấy ID mã hàng — trả về string thô, Service format 5 chữ số ────
    //    public string GetIdMaHang(string maHang)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT ISNULL(CAST(Id AS VARCHAR(10)),'') " +
    //            $"FROM B20Item WHERE code = '{Esc(maHang)}'");
    //        return raw.Trim(); // "123" → Service format thành "00123"
    //    }

    //    // ── Kiểm tra khi quét HVN ───────────────────────────────────────────
    //    public bool KiemTraTemMa(string maHvn)
    //    {
    //        string mafcc = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            "SELECT ISNULL(MAHANGFCC,'') " +
    //            "FROM DOCQRCODE WHERE (LOTHVN IS NULL OR LOTHVN = '')").Trim();

    //        if (string.IsNullOrEmpty(mafcc)) return false;

    //        // Kiểm tra alias mã (tbl_QR_ComparePart) — giữ đúng logic form gốc
    //        string alias = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT ISNULL(PartNo,'') FROM tbl_QR_ComparePart " +
    //            $"WHERE PartNoCompare = '{Esc(mafcc)}' AND Isactive = 1").Trim();

    //        if (!string.IsNullOrEmpty(alias))
    //            mafcc = alias;

    //        // Form gốc: MAHANGFCC.Replace("-","") == MAHVN
    //        return mafcc.Replace("-", "") == maHvn;
    //    }

    //    public bool KiemTraTemSoLuong(string maHvn, int slTemHvn)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            "SELECT ISNULL(SLTEMFCC,0) " +
    //            "FROM DOCQRCODE WHERE (LOTHVN IS NULL OR LOTHVN = '')");

    //        if (!int.TryParse(raw, out int slFcc)) return false;
    //        return slFcc == slTemHvn;
    //    }

    //    // ── Kiểm tra SL đã bắn ──────────────────────────────────────────────
    //    public int GetTongSlDaBan(string maHang, string docQrTable)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT ISNULL(SUM(SLTEMFCC),0) " +
    //            $"FROM {Q(docQrTable)} WHERE MAHANGFCC = '{Esc(maHang)}'");
    //        return int.TryParse(raw, out int v) ? v : 0;
    //    }
    //    // DocQRRepository — thêm method kiểm tra trùng nhanh hơn
    //    // Kiểm tra trùng theo cặp LotFCC + SoPhieu (parts[4])
    //    public bool KiemTraTrungTemTong(string lotFcc, string soPhieu, string docQrTable)
    //    {
    //        object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
    //            $"SELECT COUNT(*) FROM {Q(docQrTable)} " +
    //            $"WHERE LOTFCC = @lot AND SOPHIEU = @sophieu",
    //            new SqlParameter[] {
    //        new SqlParameter("@lot",     lotFcc),
    //        new SqlParameter("@sophieu", soPhieu)
    //            });
    //        return int.TryParse(kq?.ToString(), out int v) && v > 0;
    //    }

    //    public int GetSoLuongGiaoTheoMa(string maHang, string ifsTable)
    //    {
    //        string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
    //            $"SELECT ISNULL(SUM(SOLUONG),0) " +
    //            $"FROM {Q(ifsTable)} WHERE MAHANG = '{Esc(maHang)}'");
    //        return int.TryParse(raw, out int v) ? v : 0;
    //    }

    //    // ── Ghi FCC ─────────────────────────────────────────────────────────
    //    public void InsertFCC(DocQRCode item, string docQrTable, bool coGear = false)
    //    {
    //        string sql = coGear
    //            ? $"INSERT INTO {Q(docQrTable)} " +
    //              $"(STT,LOTFCC,MAHANGFCC,MAFCC,SLTEMFCC,GIO,Gear,SOPHIEU) " +
    //              $"VALUES ({item.STT},'{Esc(item.LotFCC)}','{Esc(item.MaHangFCC)}'," +
    //              $"'{Esc(item.MaFCC)}',{item.SlTemFCC},'{Esc(item.Gio)}'," +
    //              $"'{Esc(item.Gear)}','{Esc(item.SoPhieu)}')"
    //            : $"INSERT INTO {Q(docQrTable)} " +
    //              $"(STT,LOTFCC,MAHANGFCC,MAFCC,SLTEMFCC,GIO,SOPHIEU) " +
    //              $"VALUES ({item.STT},'{Esc(item.LotFCC)}','{Esc(item.MaHangFCC)}'," +
    //              $"'{Esc(item.MaFCC)}',{item.SlTemFCC},'{Esc(item.Gio)}'," +
    //              $"'{Esc(item.SoPhieu)}')";
    //        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql);
    //    }

    //    // ── Ghi HVN (update dòng FCC chưa ghép) ────────────────────────────
    //    public void UpdateHVN(DocQRCode item, string docQrTable)
    //    {
    //        string sql =
    //            $"UPDATE {Q(docQrTable)} " +
    //            $"SET LOTHVN    = '{Esc(item.LotHVN)}', " +
    //            $"    MAHANGHVN = '{Esc(item.MaHangHVN)}', " +
    //            $"    SLTEMHVN  = {item.SlTemHVN}, " +
    //            $"    STATUS    = 1, " +
    //            $"    KETQUA    = '{Esc(item.KetQua)}' " +
    //            $"WHERE STT = {item.STT}";
    //        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql);
    //    }

    //    public void UpdateSlHvn(int stt, int slMoi, string docQrTable)
    //    {
    //        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
    //            $"UPDATE {Q(docQrTable)} SET SLHVN = {slMoi} WHERE STT = {stt}");
    //    }

    //    // ── Xóa ─────────────────────────────────────────────────────────────
    //    public void Delete(int stt, string docQrTable)
    //    {
    //        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
    //            $"DELETE FROM {Q(docQrTable)} WHERE STT = {stt}");
    //    }

    //    public void DeleteAll(string docQrTable)
    //    {
    //        _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
    //            $"DELETE FROM {Q(docQrTable)}");
    //    }


    //    // ── MapRow — Convert.ToInt32 tránh InvalidCastException ─────────────
    //    // r.Field<int> yêu cầu đúng kiểu int; DB có thể trả decimal/short
    //    private static DocQRCode MapRow(DataRow r) => new DocQRCode
    //    {
    //        STT = r["STT"] == DBNull.Value ? 0 : Convert.ToInt32(r["STT"]),
    //        LotFCC = r["LOTFCC"] == DBNull.Value ? "" : r["LOTFCC"].ToString(),
    //        MaHangFCC = r["MAHANGFCC"] == DBNull.Value ? "" : r["MAHANGFCC"].ToString(),
    //        MaFCC = r["MAFCC"] == DBNull.Value ? "" : r["MAFCC"].ToString(),
    //        SlTemFCC = r["SLTEMFCC"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLTEMFCC"]),
    //        LotHVN = r["LOTHVN"] == DBNull.Value ? "" : r["LOTHVN"].ToString(),
    //        MaHangHVN = r["MAHANGHVN"] == DBNull.Value ? "" : r["MAHANGHVN"].ToString(),
    //        SlTemHVN = r["SLTEMHVN"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLTEMHVN"]),
    //        Gio = r["GIO"] == DBNull.Value ? "" : r["GIO"].ToString(),
    //        KetQua = r["KETQUA"] == DBNull.Value ? "" : r["KETQUA"].ToString()
    //    };

    //    private static string Esc(string s) => (s ?? "").Replace("'", "''");
    //}
    public class DocQRRepository : IDocQRRepository
    {
        private readonly SQLPROVIDER _sql;
        private readonly CustomerConfig _cfg;  // ← thêm

        public DocQRRepository(SQLPROVIDER sql, CustomerConfig cfg)
        {
            _sql = sql;
            _cfg = cfg;
        }

        private static string Q(string table) => "[dbo].[" + table + "]";

        // ── Helper: lấy tên bảng đúng theo loại SP/MP ────────────────────────
        private string GetDocQRTable(bool isSP = false)
            => isSP && !string.IsNullOrEmpty(_cfg.DocQRTableSP)
                ? _cfg.DocQRTableSP
                : _cfg.DocQRTable;

        private string GetIfsTable(bool isSP = false)
            => isSP && !string.IsNullOrEmpty(_cfg.IfsTableSP)
                ? _cfg.IfsTableSP
                : _cfg.IfsTable;

        private string GetTmpTable(bool isSP = false)
            => isSP && !string.IsNullOrEmpty(_cfg.TmpTableSP)
                ? _cfg.TmpTableSP
                : _cfg.TmpTable;
        // ════════════════════════════════════════════════════════════════════
        // Overload không tham số — dùng config mặc định (isSP = false)
        // ════════════════════════════════════════════════════════════════════
        public IReadOnlyList<DocQRCode> GetAll() => GetAll(GetDocQRTable());
        public DataTable GetAllAsTable() => GetAllAsTable(GetDocQRTable());
        public int Count() => Count(GetDocQRTable());
        public int GetMaxStt() => GetMaxStt(GetDocQRTable());
        public int CountChuaDG() => CountChuaDG(GetDocQRTable());
        public void InsertFCC(DocQRCode item) => InsertFCC(item, GetDocQRTable(), _cfg.CoGear);
        public void UpdateHVN(DocQRCode item) => UpdateHVN(item, GetDocQRTable());
        public void UpdateSlHvn(int stt, int slMoi) => UpdateSlHvn(stt, slMoi, GetDocQRTable());
        public void Delete(int stt) => Delete(stt, GetDocQRTable());
        public void DeleteAll() => DeleteAll(GetDocQRTable());
        public int GetTongSlDaBan(string ma) => GetTongSlDaBan(ma, GetDocQRTable());
        public int GetSoLuongGiaoTheoMa(string ma) => GetSoLuongGiaoTheoMa(ma, GetIfsTable());
        // ════════════════════════════════════════════════════════════════════
        // Overload bool isSP — DocQRService gọi khi biết loại
        // ════════════════════════════════════════════════════════════════════
        public IReadOnlyList<DocQRCode> GetAll(bool isSP) => GetAll(GetDocQRTable(isSP));
        public DataTable GetAllAsTable(bool isSP) => GetAllAsTable(GetDocQRTable(isSP));
        public int Count(bool isSP) => Count(GetDocQRTable(isSP));
        public int GetMaxStt(bool isSP) => GetMaxStt(GetDocQRTable(isSP));
        public int CountChuaDG(bool isSP) => CountChuaDG(GetDocQRTable(isSP));
        public void InsertFCC(DocQRCode item, bool isSP) => InsertFCC(item, GetDocQRTable(isSP), _cfg.CoGear);
        public void UpdateHVN(DocQRCode item, bool isSP) => UpdateHVN(item, GetDocQRTable(isSP));
        public void UpdateSlHvn(int stt, int slMoi, bool isSP) => UpdateSlHvn(stt, slMoi, GetDocQRTable(isSP));
        public void Delete(int stt, bool isSP) => Delete(stt, GetDocQRTable(isSP));
        public void DeleteAll(bool isSP) => DeleteAll(GetDocQRTable(isSP));
        public int GetTongSlDaBan(string ma, bool isSP) => GetTongSlDaBan(ma, GetDocQRTable(isSP));
        public int GetSoLuongGiaoTheoMa(string ma, bool isSP) => GetSoLuongGiaoTheoMa(ma, GetIfsTable(isSP));

        // ════════════════════════════════════════════════════════════════════
        // Implementations — giữ nguyên, chỉ nhận tên bảng động
        // ════════════════════════════════════════════════════════════════════
        public IReadOnlyList<DocQRCode> GetAll(string docQrTable)
        {
            string query =
                $"SELECT STT, LOTFCC, MAHANGFCC, MAFCC, SLTEMFCC, " +
                $"       LOTHVN, MAHANGHVN, SLTEMHVN, GIO, KETQUA " +
                $"FROM {Q(docQrTable)} ORDER BY STT";
            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, query);
            var list = new List<DocQRCode>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
                list.Add(MapRow(r));
            return list;
        }

        public int GetMaxStt(string docQrTable)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(MAX(STT), 0) FROM {Q(docQrTable)}");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public DataTable GetAllAsTable(string docQrTable)
        {
            string query =
                $"SELECT STT, LOTFCC, MAHANGFCC, SLTEMFCC, " +
                $"       LOTHVN, MAHANGHVN, SLTEMHVN, GIO, SUALOTHVN, KETQUA " +
                $"FROM {Q(docQrTable)} ORDER BY STT";
            return _sql.ExecuteQuery(_sql.B7R2_FCCdb, query);
        }

        public int Count(string docQrTable)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM {Q(docQrTable)}");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public int CountChuaDG(string docQrTable)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM {Q(docQrTable)} " +
                $"WHERE ISNULL(KETQUA,'') <> 'DG'");
            return int.TryParse(raw, out int v) ? v : 0;
        }
        //YMVN
        public string GetGearName(int gearCode)
        {
            // Tra B20Gear — dùng cho NormalizeLotFCC_YMVN (QR có mã số)
            if (gearCode <= 0) return "";
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(Name,'') FROM B20Gear WHERE Code = {gearCode}");
            return raw.Trim();
        }

        public string GetGearName(string gear)
        {
            // Gear đã là chuỗi — trả thẳng, không tra DB
            // Dùng cho Purchase_Order_YMVN.Gear = "Gear C :150pcs"
            return gear?.Trim() ?? "";
        }
        public bool KiemTraDuSlGear(string maHang, string gio,
    string gear, int slCan, string docQRTable)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(SUM(SLTEMFCC),0) " +
                $"FROM [{docQRTable}] " +
                $"WHERE MAHANGFCC = '{Esc(maHang)}' " +
                $"  AND GIO       = '{Esc(gio)}' " +
                $"  AND Gear      = '{Esc(gear)}'");

            return int.TryParse(raw, out int sl) && sl >= slCan;
        }

        public void UpdateGear(int stt, string gear, string docQRTable)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"UPDATE [{docQRTable}] " +
                $"SET Gear = '{Esc(gear)}' " +
                $"WHERE STT = {stt}");
        }

        public DataTable GetThongKeGear(string maHang, string gio,
            string docQRTable)
        {
            string sql =
                $"SELECT Gear, " +
                $"       COUNT(*)        AS SoTem, " +
                $"       SUM(SLTEMFCC)   AS TongSL " +
                $"FROM [{docQRTable}] " +
                $"WHERE MAHANGFCC = '{Esc(maHang)}' " +
                $"  AND GIO       = '{Esc(gio)}' " +
                $"GROUP BY Gear " +
                $"ORDER BY Gear";

            return _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
        }
        /// ------------------------------------------------------------
        // Thêm vào IDocQRRepository / DocQRRepository — dùng CHUNG cho mọi nơi cần ID pad
        public string GetIdMaHangPadded(string maHang)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                "SELECT STUFF('00000', 5-LEN(id)+1, LEN(id), id) " +
                $"FROM B20Item WHERE code = '{Esc(maHang)}'");
            return raw.Trim(); // luôn "00123"
        }

        public bool KiemTraTemMa(string maHvn)
        {
            // ── Dùng docQRTable từ config ────────────────────────────────────
            string docQrTable = GetDocQRTable();
            string mafcc = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(MAHANGFCC,'') " +
                $"FROM {Q(docQrTable)} " +
                $"WHERE (LOTHVN IS NULL OR LOTHVN = '')").Trim();

            if (string.IsNullOrEmpty(mafcc)) return false;

            string alias = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(PartNo,'') FROM tbl_QR_ComparePart " +
                $"WHERE PartNoCompare = '{Esc(mafcc)}' AND Isactive = 1").Trim();

            if (!string.IsNullOrEmpty(alias)) mafcc = alias;

            return mafcc.Replace("-", "") == maHvn;
        }

        public bool KiemTraTemSoLuong(string maHvn, int slTemHvn)
        {
            string docQrTable = GetDocQRTable();
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(SLTEMFCC,0) " +
                $"FROM {Q(docQrTable)} " +
                $"WHERE (LOTHVN IS NULL OR LOTHVN = '')");

            if (!int.TryParse(raw, out int slFcc)) return false;
            return slFcc == slTemHvn;
        }

        public int GetTongSlDaBan(string maHang, string docQrTable)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(SUM(SLTEMFCC),0) " +
                $"FROM {Q(docQrTable)} WHERE MAHANGFCC = '{Esc(maHang)}'");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public bool KiemTraTrungTemTong(string lotFcc, string soPhieu, string docQrTable)
        {
            string matchCondition = LotCodeHelper.BuildLotMatchSql("LOTFCC", "@lot");

            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                $"SELECT COUNT(*) FROM {Q(docQrTable)} " +
                $"WHERE {matchCondition} AND SOPHIEU = @sophieu",
                new SqlParameter[] {
            new SqlParameter("@lot",     lotFcc),
            new SqlParameter("@sophieu", soPhieu)
                });
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public int GetSoLuongGiaoTheoMa(string maHang, string ifsTable)
        {
            string raw = _sql.ExecuteReader(_sql.B7R2_FCCdb,
                $"SELECT ISNULL(SUM(SOLUONG),0) " +
                $"FROM {Q(ifsTable)} WHERE MAHANG = '{Esc(maHang)}'");
            return int.TryParse(raw, out int v) ? v : 0;
        }

        public void InsertFCC(DocQRCode item, string docQrTable,
            bool coGear = false)
        {
            string sql = coGear
                ? $"INSERT INTO {Q(docQrTable)} " +
                  $"(STT,LOTFCC,MAHANGFCC,MAFCC,SLTEMFCC,GIO,Gear,SOPHIEU) " +
                  $"VALUES ({item.STT},'{Esc(item.LotFCC)}','{Esc(item.MaHangFCC)}'," +
                  $"'{Esc(item.MaFCC)}',{item.SlTemFCC},'{Esc(item.Gio)}'," +
                  $"'{Esc(item.Gear)}','{Esc(item.SoPhieu)}')"
                : $"INSERT INTO {Q(docQrTable)} " +
                  $"(STT,LOTFCC,MAHANGFCC,MAFCC,SLTEMFCC,GIO,SOPHIEU) " +
                  $"VALUES ({item.STT},'{Esc(item.LotFCC)}','{Esc(item.MaHangFCC)}'," +
                  $"'{Esc(item.MaFCC)}',{item.SlTemFCC},'{Esc(item.Gio)}'," +
                  $"'{Esc(item.SoPhieu)}')";
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql);
        }

        public void UpdateHVN(DocQRCode item, string docQrTable)
        {
            string sql =
                $"UPDATE {Q(docQrTable)} " +
                $"SET LOTHVN    = '{Esc(item.LotHVN)}', " +
                $"    MAHANGHVN = '{Esc(item.MaHangHVN)}', " +
                $"    SLTEMHVN  = {item.SlTemHVN}, " +
                $"    STATUS    = 1, " +
                $"    KETQUA    = '{Esc(item.KetQua)}' " +
                $"WHERE STT = {item.STT}";
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sql);
        }

        public void UpdateSlHvn(int stt, int slMoi, string docQrTable)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"UPDATE {Q(docQrTable)} SET SLHVN = {slMoi} WHERE STT = {stt}");
        }

        public void Delete(int stt, string docQrTable)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"DELETE FROM {Q(docQrTable)} WHERE STT = {stt}");
        }

        public void DeleteAll(string docQrTable)
        {
            _sql.ExecuteNonQuery(_sql.B7R2_FCCdb,
                $"DELETE FROM {Q(docQrTable)}");
        }

        private static DocQRCode MapRow(DataRow r) => new DocQRCode
        {
            STT = r["STT"] == DBNull.Value ? 0 : Convert.ToInt32(r["STT"]),
            LotFCC = r["LOTFCC"] == DBNull.Value ? "" : r["LOTFCC"].ToString(),
            MaHangFCC = r["MAHANGFCC"] == DBNull.Value ? "" : r["MAHANGFCC"].ToString(),
            MaFCC = r["MAFCC"] == DBNull.Value ? "" : r["MAFCC"].ToString(),
            SlTemFCC = r["SLTEMFCC"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLTEMFCC"]),
            LotHVN = r["LOTHVN"] == DBNull.Value ? "" : r["LOTHVN"].ToString(),
            MaHangHVN = r["MAHANGHVN"] == DBNull.Value ? "" : r["MAHANGHVN"].ToString(),
            SlTemHVN = r["SLTEMHVN"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLTEMHVN"]),
            Gio = r["GIO"] == DBNull.Value ? "" : r["GIO"].ToString(),
            KetQua = r["KETQUA"] == DBNull.Value ? "" : r["KETQUA"].ToString()
        };

        private static string Esc(string s) => (s ?? "").Replace("'", "''");
    }
}
