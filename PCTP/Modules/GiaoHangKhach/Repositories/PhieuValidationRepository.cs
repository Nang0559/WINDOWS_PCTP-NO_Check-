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
        public PhieuValidationRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
            : base(db, uow)
        {
        }

        // ============================================================
        // FIFO
        // ============================================================
        /// <summary>
        /// Đối chiếu từng dòng trong bảng TMP đang bắn QR với LOT KEY lẽ ra phải xuất
        /// trước theo FIFO. CHỈ áp dụng cho mã hàng có <c>ItemFifoConfig.EnforceFifo = true</c>
        /// (Bước 1 thiết kế FIFO) — không ép FIFO toàn hệ thống.
        ///
        /// Quy tắc FIFO khớp 1-1 với 2 SP <c>Usp_Qrcode_Update_Stock2405</c> /
        /// <c>Usp_Qrcode_Update_Stock_SP</c> — TUYỆT ĐỐI không dùng <c>SlotLot.ImportDate</c>:
        ///   - LOT KEY = <see cref="PCTP.Common.LotCodeHelper.LEN_LEGACY_KEY"/> (13) ký tự đầu
        ///     của LotNo = YYMMDD(6) + ItemCode(5) + ShiftCode(1) + Gear(1).
        ///   - Thứ tự FIFO: YYMMDD → ShiftCode → LOT KEY (không phải NGAYNHAP/ImportDate).
        ///   - Nhiều dòng SlotLot vật lý CÙNG LOT KEY (khác Line/Machine) phải CỘNG TỔNG
        ///     tồn lại rồi mới xếp hạng FIFO theo LOT KEY đó — không xếp hạng theo từng
        ///     dòng vật lý riêng lẻ.
        ///   - So khớp với TMP: so theo LOT KEY 13 ký tự đầu của tmp.LOT, KHÔNG so full LOT
        ///     (full LOT luôn khác nhau do Counter/Qty/Tem riêng từng cuộn, so full sẽ luôn
        ///     báo vi phạm kể cả khi chọn đúng).
        ///
        /// ⚠️ Cần bảng <c>ItemFifoConfig(ItemCode NVARCHAR(60) PK, EnforceFifo BIT)</c>.
        /// Nếu bảng chưa tồn tại, hàm coi như KHÔNG mã hàng nào bị ép FIFO (an toàn — không
        /// chặn nhầm CNK), nhưng ghi Debug log để biết cần tạo bảng.
        /// </summary>
        public List<FifoViolation> CheckFifoViolations(string tmpTable)
        {
            Db.ValidateTableName(tmpTable);   // ✅ method đặc thù → qua Db (field kế thừa)

            if (!ItemFifoConfigTableExists())
            {
                System.Diagnostics.Debug.WriteLine(
                    "[CheckFifoViolations] Bảng ItemFifoConfig chưa tồn tại — bỏ qua kiểm tra FIFO. " +
                    "Tạo bảng: CREATE TABLE ItemFifoConfig (ItemCode NVARCHAR(60) PRIMARY KEY, EnforceFifo BIT NOT NULL DEFAULT 0);");
                return new List<FifoViolation>();
            }

            const int keyLen = PCTP.Common.LotCodeHelper.LEN_LEGACY_KEY; // 13 — SỬA DUY NHẤT Ở ĐÂY nếu công thức LOT đổi

            string sql = $@"
                ;WITH SlotLotKey AS (
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
                -- Nhiều dòng vật lý cùng LOT KEY (khác Line/Machine) -> cộng tổng tồn lại.
                LotKeyTon AS (
                    SELECT
                        ItemCode,
                        LotKey,
                        SUM(Quantity) AS TongTon,
                        MIN(SlotId) AS SlotIdDaiDien
                    FROM SlotLotKey
                    GROUP BY ItemCode, LotKey
                ),
                -- FIFO: YYMMDD -> ShiftCode -> LOT KEY. KHÔNG dùng ImportDate.
                LotDungFifo AS (
                    SELECT
                        ItemCode,
                        LotKey,
                        SlotIdDaiDien,
                        ROW_NUMBER() OVER (
                            PARTITION BY ItemCode
                            ORDER BY
                                LEFT(LotKey, 6) ASC,
                                CASE SUBSTRING(LotKey, 12, 1)
                                    WHEN '0' THEN 0 WHEN '1' THEN 1
                                    WHEN '2' THEN 2 WHEN '3' THEN 3
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
                INNER JOIN LotDungFifo fifo
                    ON fifo.ItemCode = tmp.MAHANG AND fifo.Rn = 1
                INNER JOIN ItemFifoConfig cfg
                    ON cfg.ItemCode = tmp.MAHANG AND cfg.EnforceFifo = 1
                WHERE LEFT(ISNULL(tmp.LOT, ''), {keyLen}) <> fifo.LotKey
                  AND ISNULL(tmp.STATUS, '') <> 'NG';";

            DataTable dt = LoadData(sql);   // ✅ 4 method CRUD chung → gọi trực tiếp, không tiền tố

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

        private bool ItemFifoConfigTableExists()
        {
            object raw = ExecuteScalar(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ItemFifoConfig]') AND type = 'U'");
            return DbValueHelper.SafeInt(raw) == 1;
        }

        #region ═══════════════════════════════════════════════════════════════
        #region IPhieuValidationRepository
        #endregion ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Đếm số dòng hiện có trong bảng DOCQRCODE.
        /// </summary>
        public int CountDocQRCode(string docQRTable)
        {
            Db.ValidateTableName(docQRTable);

            object raw = Db.ExecuteScalar(
                $"SELECT COUNT(*) FROM [{docQRTable}]");

            return DbValueHelper.SafeInt(raw);
        }

        /// <summary>
        /// Kiểm tra hệ thống có mã NG hay không.
        ///
        /// Giá trị trả về từ dbo.ufn_QRcode_ADD_CMD_MANG():
        ///     1 hoặc 2 => có mã NG
        ///     khác     => không có
        /// </summary>
        public bool CheckCoMaNG(string tenBan)
        {
            // Giữ nguyên logic cũ.
            //
            // tenBan hiện không được sử dụng trong SQL vì function
            // dbo.ufn_QRcode_ADD_CMD_MANG() tự xác định trạng thái.
            object raw = Db.ExecuteScalar(
                "SELECT dbo.ufn_QRcode_ADD_CMD_MANG()");

            string value = raw?.ToString() ?? "0";

            return int.TryParse(value, out int result)
                   && (result == 1 || result == 2);
        }

        /// <summary>
        /// Kiểm tra mã hàng đã tồn tại trong bảng phiếu hay chưa.
        /// </summary>
        public bool KiemTraMaTrongPhieu(
            string maHang,
            string tenBan)
        {
            Db.ValidateTableName(tenBan);

            object raw = Db.ExecuteScalar(
                $"SELECT COUNT(*) " +
                $"FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma",
                new SqlParameter(
                    "@ma",
                    maHang ?? (object)DBNull.Value));

            return DbValueHelper.SafeInt(raw) > 0;
        }

        // ====================================================================
        // GetDanhSachTrungMaSl
        // ====================================================================

        /// <summary>
        /// Overload sử dụng PhieuTableSet.
        /// </summary>
        public DataTable GetDanhSachTrungMaSl(
            string maHang,
            int sl,
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return GetDanhSachTrungMaSl(
                maHang,
                sl,
                tables.TmpTable,
                tables.DocQRTable);
        }

        /// <summary>
        /// Lấy danh sách phiếu có:
        ///
        ///     MAHANG = maHang
        ///     SOLUONG = sl
        ///     LOT chưa có
        ///     MAHANG tồn tại trong DOCQRCODE
        ///     KETQUA khác DG
        ///
        /// Đây là logic được cut nguyên từ PhieuRepository cũ.
        /// </summary>
        public DataTable GetDanhSachTrungMaSl(
            string maHang,
            int sl,
            string tenBan,
            string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            string sqlTemplate =
                "SELECT " +
                "    STT, " +
                "    MAHANG, " +
                "    TENHANG, " +
                "    GIOGIAO, " +
                "    SOLUONG, " +
                "    CASE " +
                "        WHEN STATUS IS NULL OR STATUS = '' " +
                "            THEN N'Chưa Bắn QRCODE' " +
                "        WHEN STATUS = '0' " +
                "            THEN N'Đang Bắn QRCODE' " +
                "        WHEN STATUS = '1' " +
                "            THEN N'Đã Bắn QRCODE' " +
                "        ELSE STATUS " +
                "    END AS STATUS " +
                $"FROM [{0}] " +
                "WHERE MAHANG = @ma " +
                "  AND SOLUONG = @sl " +
                "  AND (LOT = '' OR LOT IS NULL) " +
                "  AND MAHANG IN (" +
                $"      SELECT MAHANGFCC " +
                $"      FROM [{1}] " +
                "      WHERE ISNULL(KETQUA,'') <> 'DG' " +
                "      GROUP BY MAHANGFCC" +
                "  )";

            string sql = string.Format(
                sqlTemplate,
                tenBan,
                docQRTable);

            return Db.LoadData(
                sql,
                new SqlParameter("@ma", maHang ?? ""),
                new SqlParameter("@sl", sl));
        }

        // ====================================================================
        // CountTrungMaSl
        // ====================================================================

        /// <summary>
        /// Overload sử dụng PhieuTableSet.
        /// </summary>
        public int CountTrungMaSl(
            string maHang,
            int sl,
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return CountTrungMaSl(
                maHang,
                sl,
                tables.TmpTable,
                tables.DocQRTable);
        }

        /// <summary>
        /// Đếm số phiếu trùng MAHANG + SOLUONG chưa có LOT
        /// và còn tồn tại trong DOCQRCODE chưa DG.
        /// </summary>
        public int CountTrungMaSl(
            string maHang,
            int sl,
            string tenBan,
            string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            string sql =
                $"SELECT COUNT(*) " +
                $"FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma " +
                $"  AND SOLUONG = @sl " +
                $"  AND (LOT = '' OR LOT IS NULL) " +
                $"  AND MAHANG IN (" +
                $"      SELECT MAHANGFCC " +
                $"      FROM [{docQRTable}] " +
                $"      WHERE KETQUA <> 'DG' " +
                $"      GROUP BY MAHANGFCC" +
                $"  )";

            object raw = Db.ExecuteScalar(
                sql,
                new SqlParameter("@ma", maHang ?? ""),
                new SqlParameter("@sl", sl));

            return DbValueHelper.SafeInt(raw);
        }

        // ====================================================================
        // GetDonHangChuaLot
        // ====================================================================

        /// <summary>
        /// Overload sử dụng PhieuTableSet.
        /// </summary>
        public DataTable GetDonHangChuaLot(
            PhieuTableSet tables)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return GetDonHangChuaLot(
                tables.TmpTable,
                tables.DocQRTable);
        }

        /// <summary>
        /// Lấy các dòng đơn hàng chưa có LOT
        /// và MAHANG vẫn còn trong DOCQRCODE chưa DG.
        /// </summary>
        public DataTable GetDonHangChuaLot(
            string tenBan,
            string docQRTable)
        {
            Db.ValidateTableName(tenBan);
            Db.ValidateTableName(docQRTable);

            string sql =
                $"SELECT " +
                $"    STT, " +
                $"    MAHANG, " +
                $"    LOT, " +
                $"    SOLUONG " +
                $"FROM [{tenBan}] " +
                $"WHERE (LOT = '' OR LOT IS NULL) " +
                $"  AND MAHANG IN (" +
                $"      SELECT MAHANGFCC " +
                $"      FROM [{docQRTable}] " +
                $"      WHERE ISNULL(KETQUA,'') <> 'DG' " +
                $"      GROUP BY MAHANGFCC" +
                $"  ) " +
                $"ORDER BY STT";

            return Db.LoadData(sql);
        }
        public Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (maHangList == null || maHangList.Count == 0) return result;

            string inClause = string.Join(",", maHangList.Select(m => $"'{m.Replace("'", "''")}'"));
            DataTable dt = Db.LoadData(
                $"SELECT Code, ISNULL(CAST(MinCloseQty AS INT), 0) AS QC FROM B20Item WHERE Code IN ({inClause})");

            foreach (DataRow row in dt.Rows)
                result[row["Code"].ToString().Trim()] = Convert.ToInt32(row["QC"]);

            return result;
        }
        public DataTable TinhHangThieuTuDonHang(DataTable donHang)
        {
            var result = new DataTable();
            result.Columns.Add("MH", typeof(string));
            result.Columns.Add("GIOGIAO", typeof(string));
            result.Columns.Add("SLGIAO", typeof(int));
            result.Columns.Add("SLTHIEU", typeof(int));

            if (donHang == null || donHang.Rows.Count == 0)
                return result;

            // ── Gom SL cần giao theo (MAHANG, GIOGIAO) — chỉ tính dòng CHƯA CNK xong ──
            var rows = new List<(string MaHang, string GioGiao, int Sl)>();

            foreach (DataRow row in donHang.Rows)
            {
                string status = row.Table.Columns.Contains("STATUS")
                    ? row["STATUS"]?.ToString().Trim() ?? "" : "";
                if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    continue;   // đã CNK xong — không còn "thiếu" nữa

                string maHang = row["MAHANG"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(maHang)) continue;

                string gioGiao = row.Table.Columns.Contains("GIOGIAO")
                    ? row["GIOGIAO"]?.ToString().Trim() ?? "" : "";

                int sl = row.Table.Columns.Contains("SOLUONG") && row["SOLUONG"] != DBNull.Value
                    ? Convert.ToInt32(row["SOLUONG"]) : 0;

                rows.Add((maHang, gioGiao, sl));
            }

            if (rows.Count == 0)
                return result;

            // ── Batch query tồn STOCKTP theo danh sách MAHANG (1 lần, tránh N+1) ──
            var maHangList = rows.Select(r => r.MaHang).Distinct().ToList();
            string inClause = string.Join(",", maHangList.Select(m => $"'{SqlHelper.Esc(m)}'"));
            DataTable tonDt = LoadData(
                $"SELECT PART, ISNULL(SUM(SLCONLAI),0) AS TONG_TON " +
                $"FROM STOCKTP WHERE PART IN ({inClause}) GROUP BY PART");

            var tonMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in tonDt.Rows)
                tonMap[row["PART"].ToString().Trim()] = Convert.ToInt32(row["TONG_TON"]);

            // ── Gom theo (MAHANG, GIOGIAO), sắp theo GIOGIAO tăng dần trong từng MAHANG ──
            var grouped = rows
                .GroupBy(r => new { r.MaHang, r.GioGiao })
                .Select(g => new { g.Key.MaHang, g.Key.GioGiao, SlGiao = g.Sum(x => x.Sl) })
                .ToList();

            // ── Phân bổ tồn kho theo thứ tự giờ giao sớm nhất trước (ưu tiên FIFO theo giờ) ──
            foreach (var maHangGroup in grouped.GroupBy(x => x.MaHang))
            {
                int tonConLai = tonMap.TryGetValue(maHangGroup.Key, out int t) ? t : 0;

                foreach (var gio in maHangGroup.OrderBy(x => x.GioGiao, StringComparer.OrdinalIgnoreCase))
                {
                    int slDuocCap = Math.Min(gio.SlGiao, Math.Max(tonConLai, 0));
                    int slThieu = gio.SlGiao - slDuocCap;
                    tonConLai -= slDuocCap;

                    if (slThieu > 0)
                        result.Rows.Add(gio.MaHang, gio.GioGiao, gio.SlGiao, slThieu);
                }
            }

            return result;
        }
        #endregion

    }
}