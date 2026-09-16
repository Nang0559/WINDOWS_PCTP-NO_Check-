using PCTP.FuctionMain;
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
    public sealed class PhieuValidationRepository
    : SqlRepositoryBase, IPhieuValidationRepository
    {
        public PhieuValidationRepository(PhieuSqlExecutor db, IUnitOfWork uow) : base(db, uow) { }

        // ============================================================
        // FIFO - FVN_ItemFifoConfig is the single source of truth.
        //
        // IMPORTANT ARCHITECTURE RULE:
        // - This method is the ONLY FIFO business-rule gate before stock update.
        // - EnforceFifo = 0 -> this item is not blocked by FIFO.
        // - EnforceFifo = 1 -> the selected LOT KEY must be the current FIFO LOT KEY.
        // - If this method returns violations, the caller MUST NOT call any stock SP.
        // - Usp_Qrcode_Update_Stock2405 / Usp_Qrcode_Update_Stock_SP must NOT duplicate
        //   FIFO business logic; they only process stock that has already passed this gate.
        //
        // FIFO calculation rules:
        // - LOT KEY = LEFT(LOT, 13).
        // - Only SlotLot.Quantity > 0 is considered available stock.
        // - Multiple SlotLot rows with the same ItemCode + LOT KEY are aggregated first.
        // - FIFO order = YYMMDD -> ShiftCode -> LOT KEY.
        // ============================================================
        public List<FifoViolation> CheckFifoViolations(string tmpTable)
        {
            Db.ValidateTableName(tmpTable);

            // FIFO is optional by deployment/database version.
            // If the authoritative config table is not installed, do not block legacy stock flow.
            if (!FvnItemFifoConfigTableExists())
            {
                System.Diagnostics.Debug.WriteLine(
                    "[CheckFifoViolations] FVN_ItemFifoConfig chưa tồn tại. Bỏ qua FIFO để không chặn nhầm xuất kho.");
                return new List<FifoViolation>();
            }

            const int keyLen = PCTP.Common.LotCodeHelper.LEN_LEGACY_KEY;

            string sql = $@"
                ;WITH SlotLotKey AS
                (
                    SELECT
                        sl.ItemCode,
                        LEFT(sl.LotNo, {keyLen}) AS LotKey,
                        sl.SlotId,
                        sl.Quantity
                    FROM SlotLot sl
                    WHERE sl.PhieuStatus = 0
                      AND sl.Quantity > 0
                      AND LEN(ISNULL(sl.LotNo, '')) >= {keyLen}
                ),
                LotKeyTon AS
                (
                    -- IMPORTANT: aggregate all physical SlotLot rows sharing one LOT KEY
                    -- before calculating FIFO. FIFO is decided at LOT KEY level, not SlotId level.
                    SELECT
                        ItemCode,
                        LotKey,
                        SUM(Quantity) AS TongTon,
                        MIN(SlotId) AS SlotIdDaiDien
                    FROM SlotLotKey
                    GROUP BY ItemCode, LotKey
                ),
                LotDungFifo AS
                (
                    SELECT
                        ItemCode,
                        LotKey,
                        SlotIdDaiDien,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY ItemCode
                            ORDER BY
                                LEFT(LotKey, 6) ASC,
                                CASE SUBSTRING(LotKey, 12, 1)
                                    WHEN '0' THEN 0
                                    WHEN '1' THEN 1
                                    WHEN '2' THEN 2
                                    WHEN '3' THEN 3
                                    ELSE 9
                                END ASC,
                                LotKey ASC
                        ) AS Rn
                    FROM LotKeyTon
                    WHERE TongTon > 0
                )
                SELECT
                    tmp.MAHANG AS MaHang,
                    tmp.LOT AS LotDaChon,
                    fifo.LotKey AS LotDungRaPhaiChon,
                    fifo.SlotIdDaiDien AS SlotIdDungRaPhaiChon
                FROM [{tmpTable}] tmp
                INNER JOIN FVN_ItemFifoConfig cfg
                    ON cfg.ItemCode = tmp.MAHANG
                   AND cfg.EnforceFifo = 1
                INNER JOIN LotDungFifo fifo
                    ON fifo.ItemCode = tmp.MAHANG
                   AND fifo.Rn = 1
                WHERE ISNULL(tmp.STATUS, '') <> 'NG'
                  AND LEFT(ISNULL(tmp.LOT, ''), {keyLen}) <> fifo.LotKey;";

            // NOTE: This query only validates the business rule.
            // It does NOT change STOCKTP/SlotLot and does NOT call the stock SP.
            DataTable dt = LoadData(sql);
            var result = new List<FifoViolation>();

            foreach (DataRow row in dt.Rows)
            {
                result.Add(new FifoViolation
                {
                    MaHang = row["MaHang"]?.ToString(),
                    LotDaChon = row["LotDaChon"]?.ToString(),
                    LotDungRaPhaiChon = row["LotDungRaPhaiChon"]?.ToString(),
                    SlotIdDungRaPhaiChon = Convert.ToInt32(row["SlotIdDungRaPhaiChon"])
                });
            }

            return result;
        }

        private bool FvnItemFifoConfigTableExists()
        {
            object raw = ExecuteScalar(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FVN_ItemFifoConfig]') AND type = 'U'");
            return DbValueHelper.SafeInt(raw) == 1;
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
            object raw = Db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{tenBan}] WHERE MAHANG = @ma",
                new SqlParameter("@ma", maHang ?? (object)DBNull.Value));
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
            string sql =
                "SELECT STT, MAHANG, TENHANG, GIOGIAO, SOLUONG, " +
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
            object raw = Db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{tenBan}] WHERE MAHANG = @ma AND SOLUONG = @sl " +
                $"AND (LOT = '' OR LOT IS NULL) AND MAHANG IN (SELECT MAHANGFCC FROM [{docQRTable}] WHERE KETQUA <> 'DG' GROUP BY MAHANGFCC)",
                new SqlParameter("@ma", maHang ?? ""), new SqlParameter("@sl", sl));
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
            string sql =
                $"SELECT STT, MAHANG, LOT, SOLUONG FROM [{tenBan}] " +
                $"WHERE (LOT = '' OR LOT IS NULL) AND MAHANG IN (SELECT MAHANGFCC FROM [{docQRTable}] WHERE ISNULL(KETQUA,'') <> 'DG' GROUP BY MAHANGFCC) ORDER BY STT";
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
            foreach (var kv in maBangRieng) if (!maIfs.ContainsKey(kv.Key)) result.Rows.Add(kv.Key, kv.Value.TenHang, kv.Value.SoLuong, "Chỉ có ở Bảng Riêng");
            foreach (var kv in maIfs) if (!maBangRieng.ContainsKey(kv.Key)) result.Rows.Add(kv.Key, kv.Value.TenHang, kv.Value.SoLuong, "Chỉ có ở IFS");
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
                int sl = dt.Columns.Contains("SOLUONG") && row["SOLUONG"] != DBNull.Value ? Convert.ToInt32(row["SOLUONG"]) : 0;
                map[ma] = (ten, sl);
            }
            return map;
        }
        #endregion
    }
}