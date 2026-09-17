using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuValidationRepository : SqlRepositoryBase, IPhieuValidationRepository
    {
        public PhieuValidationRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        /// <summary>
        /// Authoritative FIFO re-check immediately before CNK.
        /// Only FIFO-enabled parts are evaluated.
        /// This method is read-only: it never changes LOT/TMP/DOCQR data.
        /// </summary>
        public List<FifoViolation> CheckFifoViolations(string tmpTable)
        {
            Db.ValidateTableName(tmpTable);

            const int keyLen = PCTP.Common.LotCodeHelper.LEN_LEGACY_KEY;
            var result = new List<FifoViolation>();

            DataTable selectedRows = LoadData($@"
SELECT
    tmp.STT,
    tmp.MAHANG AS MaHang,
    tmp.LOT AS LotDaChon,
    ISNULL(tmp.SOLUONG, 0) AS SoLuong
FROM [{tmpTable}] tmp
INNER JOIN FVN_ItemFifoConfig cfg
    ON cfg.ItemCode = tmp.MAHANG
   AND ISNULL(cfg.EnforceFifo, 0) = 1
WHERE ISNULL(tmp.STATUS, '') <> 'NG'
  AND ISNULL(tmp.LOT, '') <> ''
ORDER BY tmp.MAHANG, tmp.STT;");

            foreach (var partGroup in selectedRows.AsEnumerable()
                .GroupBy(r => r["MaHang"]?.ToString()?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                string maHang = partGroup.Key;
                if (string.IsNullOrEmpty(maHang)) continue;

                DataTable fifoRows = LoadData($@"
SELECT
    LEFT(LOT, {keyLen}) AS LOTKEY,
    MIN(LOT) AS LOTDISPLAY,
    SUM(ISNULL(SLCONLAI, 0)) AS SLCONLAI
FROM STOCKTP
WHERE PART = @ma
  AND ISNULL(SLCONLAI, 0) > 0
  AND LEN(ISNULL(LOT, '')) >= {keyLen}
GROUP BY PART, LEFT(LOT, {keyLen})
ORDER BY
    LEFT(LEFT(LOT, {keyLen}), 6),
    CASE SUBSTRING(LEFT(LOT, {keyLen}), 12, 1)
        WHEN '0' THEN 0
        WHEN '1' THEN 1
        WHEN '2' THEN 2
        WHEN '3' THEN 3
        ELSE 9
    END,
    LEFT(LOT, {keyLen});",
                    new SqlParameter("@ma", maHang));

                var fifo = fifoRows.AsEnumerable()
                    .Select(r => new FifoStockLineInternal
                    {
                        Key = r["LOTKEY"]?.ToString()?.Trim() ?? string.Empty,
                        Display = r["LOTDISPLAY"]?.ToString()?.Trim() ?? string.Empty,
                        Stock = r["SLCONLAI"] == DBNull.Value ? 0 : Convert.ToInt32(r["SLCONLAI"])
                    })
                    .Where(x => !string.IsNullOrEmpty(x.Key) && x.Stock > 0)
                    .ToList();

                if (fifo.Count == 0)
                {
                    foreach (DataRow row in partGroup)
                    {
                        result.Add(new FifoViolation
                        {
                            Stt = SafeInt(row["STT"]),
                            MaHang = maHang,
                            LotDaChon = row["LotDaChon"]?.ToString()?.Trim() ?? string.Empty,
                            LotDungRaPhaiChon = string.Empty,
                            SlotIdDungRaPhaiChon = 0,
                            SoLuong = SafeInt(row["SoLuong"])
                        });
                    }
                    continue;
                }

                var selectionsByRow = partGroup
                    .Select(row => new RowSelection
                    {
                        Stt = SafeInt(row["STT"]),
                        LotText = row["LotDaChon"]?.ToString()?.Trim() ?? string.Empty,
                        SoLuong = SafeInt(row["SoLuong"]),
                        Selections = ParseLotSelections(row["LotDaChon"]?.ToString())
                    })
                    .Where(x => x.Selections.Count > 0)
                    .OrderBy(x => x.Stt)
                    .ToList();

                int totalSelectedQty = selectionsByRow.Sum(x => x.Selections.Sum(s => s.Quantity));
                var allowedByLot = BuildAllowedAllocation(fifo, totalSelectedQty, keyLen);

                foreach (RowSelection row in selectionsByRow)
                {
                    var trial = new Dictionary<string, int>(allowedByLot, StringComparer.OrdinalIgnoreCase);
                    string requiredLot = string.Empty;
                    bool rowValid = true;

                    foreach (LotSelection selection in row.Selections)
                    {
                        int remaining;
                        if (!trial.TryGetValue(selection.LotKey, out remaining) || remaining < selection.Quantity)
                        {
                            rowValid = false;
                            requiredLot = FindRequiredLot(fifo, trial);
                            break;
                        }

                        trial[selection.LotKey] = remaining - selection.Quantity;
                    }

                    if (!rowValid)
                    {
                        result.Add(new FifoViolation
                        {
                            Stt = row.Stt,
                            MaHang = maHang,
                            LotDaChon = row.LotText,
                            LotDungRaPhaiChon = requiredLot,
                            SlotIdDungRaPhaiChon = 0,
                            SoLuong = row.SoLuong
                        });
                        continue;
                    }

                    allowedByLot = trial;
                }
            }

            return result
                .GroupBy(x => new { x.Stt, x.MaHang, x.LotDaChon })
                .Select(g => g.First())
                .ToList();
        }

        private static Dictionary<string, int> BuildAllowedAllocation(
            List<FifoStockLineInternal> fifo,
            int requiredQty,
            int keyLen)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int remaining = Math.Max(requiredQty, 0);

            foreach (FifoStockLineInternal row in fifo)
            {
                if (remaining <= 0) break;
                int allowed = Math.Min(remaining, row.Stock);
                if (allowed <= 0) continue;
                result[row.Key] = allowed;
                remaining -= allowed;
            }

            return result;
        }

        private static string FindRequiredLot(
            List<FifoStockLineInternal> fifo,
            Dictionary<string, int> remainingAllowed)
        {
            foreach (FifoStockLineInternal row in fifo)
            {
                int remaining;
                if (remainingAllowed.TryGetValue(row.Key, out remaining) && remaining > 0)
                    return row.Display;
            }

            return fifo.Count == 0 ? string.Empty : fifo[0].Display;
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            int.TryParse(value.ToString(), out int result);
            return result;
        }

        private static List<LotSelection> ParseLotSelections(string value)
        {
            var result = new List<LotSelection>();
            if (string.IsNullOrWhiteSpace(value)) return result;

            foreach (string token in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string part = token.Trim();
                int separator = part.LastIndexOf('-');
                if (separator <= 0 || separator >= part.Length - 1) continue;

                string lot = part.Substring(0, separator).Trim();
                int quantity;
                if (!int.TryParse(part.Substring(separator + 1).Trim(), out quantity) || quantity <= 0)
                    continue;

                string key = lot.Length <= 13 ? lot : lot.Substring(0, 13);
                result.Add(new LotSelection { LotKey = key, Lot = lot, Quantity = quantity });
            }

            return result;
        }

        private sealed class LotSelection
        {
            public string LotKey { get; set; }
            public string Lot { get; set; }
            public int Quantity { get; set; }
        }

        private sealed class RowSelection
        {
            public int Stt { get; set; }
            public string LotText { get; set; }
            public int SoLuong { get; set; }
            public List<LotSelection> Selections { get; set; }
        }

        private sealed class FifoStockLineInternal
        {
            public string Key { get; set; }
            public string Display { get; set; }
            public int Stock { get; set; }
        }

        #region IPhieuValidationRepository

        public int CountDocQRCode(string docQRTable)
        {
            Db.ValidateTableName(docQRTable);
            object raw = Db.ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]");
            return DbValueHelper.SafeInt(raw);
        }

        public bool CheckCoMaNG(string tenBan)
        {
            object raw = Db.ExecuteScalar("SELECT dbo.ufn_QRcode_ADD_CMD_MANG()");
            string value = raw?.ToString() ?? "0";
            return int.TryParse(value, out int result) && (result == 1 || result == 2);
        }

        public bool KiemTraMaTrongPhieu(string maHang, string tenBan)
        {
            Db.ValidateTableName(tenBan);
            object raw = Db.ExecuteScalar($"SELECT COUNT(*) FROM [{tenBan}] WHERE MAHANG = @ma", new SqlParameter("@ma", maHang ?? (object)DBNull.Value));
            return DbValueHelper.SafeInt(raw) > 0;
        }

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return GetDanhSachTrungMaSl(maHang, sl, tables.TmpTable, tables.DocQRTable);
        }

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);
            string sql = "SELECT STT, MAHANG, TENHANG, GIOGIAO, SOLUONG, " +
                         "CASE WHEN STATUS IS NULL OR STATUS = '' THEN N'Chưa Bắn QRCODE' " +
                         "WHEN STATUS = '0' THEN N'Đang Bắn QRCODE' " +
                         "WHEN STATUS = '1' THEN N'Đã Bắn QRCODE' ELSE STATUS END AS STATUS " +
                         $"FROM [{tenBan}] WHERE MAHANG = @ma AND SOLUONG = @sl " +
                         $"AND (LOT = '' OR LOT IS NULL) AND MAHANG IN (SELECT MAHANGFCC FROM [{docQRTable}] WHERE ISNULL(KETQUA,'') <> 'DG' GROUP BY MAHANGFCC)";
            return Db.LoadData(sql, new SqlParameter("@ma", maHang ?? ""), new SqlParameter("@sl", sl));
        }

        public int CountTrungMaSl(string maHang, int sl, PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return CountTrungMaSl(maHang, sl, tables.TmpTable, tables.DocQRTable);
        }

        public int CountTrungMaSl(string maHang, int sl, string tenBan, string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);
            object raw = Db.ExecuteScalar($"SELECT COUNT(*) FROM [{tenBan}] WHERE MAHANG = @ma AND SOLUONG = @sl AND (LOT = '' OR LOT IS NULL) AND MAHANG IN (SELECT MAHANGFCC FROM [{docQRTable}] WHERE KETQUA <> 'DG' GROUP BY MAHANGFCC)", new SqlParameter("@ma", maHang ?? ""), new SqlParameter("@sl", sl));
            return DbValueHelper.SafeInt(raw);
        }

        public DataTable GetDonHangChuaLot(PhieuTableSet tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return GetDonHangChuaLot(tables.TmpTable, tables.DocQRTable);
        }

        public DataTable GetDonHangChuaLot(string tenBan, string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);
            string sql = $"SELECT STT, MAHANG, LOT, SOLUONG FROM [{tenBan}] WHERE (LOT = '' OR LOT IS NULL) AND MAHANG IN (SELECT MAHANGFCC FROM [{docQRTable}] WHERE ISNULL(KETQUA,'') <> 'DG' GROUP BY MAHANGFCC) ORDER BY STT";
            return Db.LoadData(sql);
        }

        public Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (maHangList == null || maHangList.Count == 0) return result;
            string inClause = string.Join(",", maHangList.Select(m => $"'{m.Replace("'", "''")}'"));
            DataTable dt = Db.LoadData($"SELECT Code, ISNULL(CAST(MinCloseQty AS INT), 0) AS QC FROM B20Item WHERE Code IN ({inClause})");
            foreach (DataRow row in dt.Rows) result[row["Code"].ToString().Trim()] = Convert.ToInt32(row["QC"]);
            return result;
        }

        public DataTable TinhHangThieuTuDonHang(DataTable donHang)
        {
            var result = new DataTable();
            result.Columns.Add("MH", typeof(string));
            result.Columns.Add("GIOGIAO", typeof(string));
            result.Columns.Add("SLGIAO", typeof(int));
            result.Columns.Add("SLTHIEU", typeof(int));
            if (donHang == null || donHang.Rows.Count == 0) return result;

            var rows = new List<(string MaHang, string GioGiao, int Sl)>();
            foreach (DataRow row in donHang.Rows)
            {
                string status = row.Table.Columns.Contains("STATUS") ? row["STATUS"]?.ToString().Trim() ?? "" : "";
                if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)) continue;
                string maHang = row["MAHANG"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(maHang)) continue;
                string gioGiao = row.Table.Columns.Contains("GIOGIAO") ? row["GIOGIAO"]?.ToString().Trim() ?? "" : "";
                int sl = row.Table.Columns.Contains("SOLUONG") && row["SOLUONG"] != DBNull.Value ? Convert.ToInt32(row["SOLUONG"]) : 0;
                rows.Add((maHang, gioGiao, sl));
            }
            if (rows.Count == 0) return result;

            var maHangList = rows.Select(r => r.MaHang).Distinct().ToList();
            string inClause = string.Join(",", maHangList.Select(m => $"'{SqlHelper.Esc(m)}'"));
            DataTable tonDt = LoadData($"SELECT PART, ISNULL(SUM(SLCONLAI),0) AS TONG_TON FROM STOCKTP WHERE PART IN ({inClause}) GROUP BY PART");
            var tonMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in tonDt.Rows) tonMap[row["PART"].ToString().Trim()] = Convert.ToInt32(row["TONG_TON"]);

            var grouped = rows.GroupBy(r => new { r.MaHang, r.GioGiao }).Select(g => new { g.Key.MaHang, g.Key.GioGiao, SlGiao = g.Sum(x => x.Sl) }).ToList();
            foreach (var maHangGroup in grouped.GroupBy(x => x.MaHang))
            {
                int tonConLai = tonMap.TryGetValue(maHangGroup.Key, out int t) ? t : 0;
                foreach (var gio in maHangGroup.OrderBy(x => x.GioGiao, StringComparer.OrdinalIgnoreCase))
                {
                    int slDuocCap = Math.Min(gio.SlGiao, Math.Max(tonConLai, 0));
                    int slThieu = gio.SlGiao - slDuocCap;
                    tonConLai -= slDuocCap;
                    if (slThieu > 0) result.Rows.Add(gio.MaHang, gio.GioGiao, gio.SlGiao, slThieu);
                }
            }
            return result;
        }

        public DataTable SoSanhLechIFS(DataTable donHangBangRieng, DataTable ifsData)
        {
            var result = new DataTable();
            result.Columns.Add("MAHANG", typeof(string));
            result.Columns.Add("TENHANG", typeof(string));
            result.Columns.Add("SOLUONG", typeof(int));
            result.Columns.Add("NGUON_LECH", typeof(string));

            var maBangRieng = BuildMaMap(donHangBangRieng);
            var maIfs = BuildMaMap(ifsData);

            foreach (var kv in maBangRieng)
                if (!maIfs.ContainsKey(kv.Key))
                    result.Rows.Add(kv.Key, kv.Value.TenHang, kv.Value.SoLuong, "Chỉ có ở Bảng Riêng");

            foreach (var kv in maIfs)
                if (!maBangRieng.ContainsKey(kv.Key))
                    result.Rows.Add(kv.Key, kv.Value.TenHang, kv.Value.SoLuong, "Chỉ có ở IFS");

            return result;
        }

        private static Dictionary<string, (string TenHang, int SoLuong)> BuildMaMap(DataTable dt)
        {
            var map = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase);
            if (dt == null) return map;

            foreach (DataRow row in dt.Rows)
            {
                string ma = dt.Columns.Contains("MAHANG") ? row["MAHANG"]?.ToString().Trim() ?? "" : "";
                if (string.IsNullOrEmpty(ma) || map.ContainsKey(ma)) continue;

                string ten = dt.Columns.Contains("TENHANG") ? row["TENHANG"]?.ToString().Trim() ?? "" : "";
                int sl = dt.Columns.Contains("SOLUONG") && row["SOLUONG"] != DBNull.Value
                    ? Convert.ToInt32(row["SOLUONG"])
                    : 0;

                map[ma] = (ten, sl);
            }

            return map;
        }

        #endregion
    }
}
