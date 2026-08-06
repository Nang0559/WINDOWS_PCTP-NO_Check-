using PCTP.ClassSQL;
using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Infrastructure.Repositories
{
    public class GioXuatRepository : IGioXuatRepository
    {
        private readonly SQLPROVIDER _sql;
        public GioXuatRepository(SQLPROVIDER sql) => _sql = sql;

        public IReadOnlyList<GioXuat> GetDanhSachGioVP() => Load("GioFCCVP");
        public IReadOnlyList<GioXuat> GetDanhSachGioHN() => Load("GioFCCHN");
        public IReadOnlyDictionary<string, string> GetDictGioVP() => LoadDict("GioFCCVP");
        public IReadOnlyDictionary<string, string> GetDictGioHN() => LoadDict("GioFCCHN");

        // ════════════════════════════════════════════════════════════════════
        // Load danh sách giờ xuất — GROUP BY cột giờ VP hoặc HN
        // Form gốc loadKGXSQL():
        //   "select A.GioFCCVP FROM
        //    (select GioFCCVP, MAX(ID) AS GIOHVN from QRCODE_CHANGETIME
        //     group by GioFCCVP) A order by A.GIOHVN"
        // ════════════════════════════════════════════════════════════════════
        private IReadOnlyList<GioXuat> Load(string colName)
        {
            // Thay STRING_AGG bằng FOR XML PATH — tương thích SQL Server 2008+
            string sql =
                $"SELECT A.{colName}, A.MinID, A.DanhSachGio " +
                $"FROM ( " +
                $"    SELECT {colName}, " +
                $"           MIN(ID) AS MinID, " +
                $"           STUFF( " +
                $"               (SELECT ',' + RIGHT('0' + CAST(b.GIOHVN AS VARCHAR(2)), 2) " +
                $"                FROM QRCODE_CHANGETIME b " +
                $"                WHERE b.{colName} = a.{colName} " +
                $"                ORDER BY b.ID " +
                $"                FOR XML PATH('')), " +
                $"               1, 1, '') AS DanhSachGio " +
                $"    FROM QRCODE_CHANGETIME a " +
                $"    GROUP BY {colName} " +
                $") A " +
                $"ORDER BY A.MinID";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
            var list = new List<GioXuat>();

            foreach (DataRow row in dt.Rows)
            {
                string moTa = row[colName].ToString().Trim();
                string danhSachGio = row["DanhSachGio"].ToString().Trim(); // "06,07,08,09"
                string ma = MapMaGio(moTa, danhSachGio);
                list.Add(new GioXuat(ma, moTa));
            }
            return list;
        }

        // ════════════════════════════════════════════════════════════════════
        // LoadDict — map GIOHVN (giờ Oracle "06") → moTa ("(6+7+8+9)H")
        // Dùng trong InPhieuService để fill cột KGX
        // Form gốc: "select GioFCCVP,GIOHVN from QRCODE_CHANGETIME"
        // ════════════════════════════════════════════════════════════════════
        private IReadOnlyDictionary<string, string> LoadDict(string colName)
        {
            string sql =
                $"SELECT {colName}, " +
                $"RIGHT('0' + CAST(GIOHVN AS VARCHAR(2)), 2) AS GIOHVN " + // ← đảm bảo 2 chữ số
                $"FROM QRCODE_CHANGETIME";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in dt.Rows)
            {
                string key = row["GIOHVN"].ToString().Trim(); // "06"
                string val = row[colName].ToString().Trim();  // "(6+7+8+9)H"
                if (!dict.ContainsKey(key))
                    dict[key] = val;
            }
            return dict;
        }

        // ════════════════════════════════════════════════════════════════════
        // MapMaGio — convert MoTa + DanhSachGio → Ma filter IFS
        // Form gốc loadKGXSQL():
        //   if (VL == "(O TYPE 2)") GT = "'02'";
        //   else if (VL == "(GIAO DB)") GT = "#";
        //   else → split '+', replace H → pad 2 digits
        // ════════════════════════════════════════════════════════════════════
        public static string MapMaGio(string moTa, string danhSachGio = "")
        {
            switch (moTa.Trim())
            {
                case "(O TYPE 2)": return "'02'";
                case "(O TYPE 3)": return "'03'";
                case "(O TYPE 4)": return "'04'";
                case "(O TYPE #)": return "'01'";
                case "(O TYPE 6)": return "'00'";
                case "(SP6)": return "SP6";
                case "(SP#)": return "SP#";
                case "(GIAO DB)": return "#";

                default:
                    // Ưu tiên dùng DanhSachGio từ DB — đã đúng format 2 chữ số
                    // "06,07,08,09" → "'06','07','08','09'"
                    if (!string.IsNullOrWhiteSpace(danhSachGio))
                    {
                        var tokens = danhSachGio
                            .Split(',')
                            .Where(g => !string.IsNullOrWhiteSpace(g))
                            .Select(g =>
                            {
                                string gio = g.Trim();
                                // Đảm bảo 2 chữ số
                                if (gio.Length == 1) gio = "0" + gio;
                                return $"'{gio}'";
                            });
                        return string.Join(",", tokens);
                    }

                    // Fallback: parse chuỗi moTa nếu không có danhSachGio
                    return ParseGioThuong(moTa);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Fallback — parse moTa khi không có danhSachGio từ DB
        // Form gốc: split '+', replace các ký tự, pad 2 digits
        // ════════════════════════════════════════════════════════════════════
        public static string ParseGioThuong(string vl)
        {
            string cleaned = vl
                .Replace("(", "")
                .Replace(")", "")
                .Replace("H", "")
                .Trim();

            var parts = cleaned
                .Split('+')
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g =>
                {
                    string gt = g.Trim();
                    if (gt.Length == 1) gt = "0" + gt;
                    return $"'{gt}'";
                });

            return string.Join(",", parts);
        }
        // GioXuatRepository — thêm method cho YMVN
        public List<string> GetDanhSachGioYMVN(string ngayGiao)
        {
            // YMVN: lấy danh sách giờ từ Purchase_Order theo ngày
            // Format giờ: "14:30", "15:00"... 
            string sql = $@"
        SELECT DISTINCT 
            RIGHT('0' + CAST(DATEPART(HOUR, DELIVERY_TIME) AS VARCHAR(2)), 2) + ':' +
            RIGHT('0' + CAST(DATEPART(MINUTE, DELIVERY_TIME) AS VARCHAR(2)), 2) AS GIO
        FROM PURCHASE_ORDER_YMVN
        WHERE CAST(DELIVERY_DATE AS DATE) = CAST('{ngayGiao}' AS DATE)
          AND GIO IS NOT NULL
        ORDER BY GIO";

            DataTable dt = _sql.ExecuteQuery(_sql.B7R2_FCCdb, sql);
            return dt.Rows.Cast<DataRow>()
                .Select(r => r["GIO"].ToString().Trim())
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList();
        }
    }
}
