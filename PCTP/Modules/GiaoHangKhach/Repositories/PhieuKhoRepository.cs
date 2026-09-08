using PCTP.Common;
using PCTP.FuctionMain;
using PCTP.Models;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.Services;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuKhoRepository : SqlRepositoryBase, IPhieuKhoRepository
    {
        private readonly CustomerConfig _cfg;
        private readonly IHangChoGiaoRepository _hangChoGiaoRepo;
        private readonly IBulkStockSlotRepository _bulkStockSlotRepo;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly IPhieuValidationRepository _validationRepo;

        public PhieuKhoRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            IBulkStockSlotRepository bulkStockSlotRepo,
            IStockHistoryRepository historyRepo,
            IPhieuValidationRepository validationRepo,
            CustomerConfig cfg = null,
            IHangChoGiaoRepository hangChoGiaoRepo = null)
            : base(db, uow)
        {
            _bulkStockSlotRepo = bulkStockSlotRepo ?? throw new ArgumentNullException(nameof(bulkStockSlotRepo));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _validationRepo = validationRepo ?? throw new ArgumentNullException(nameof(validationRepo));
            _cfg = cfg;
            _hangChoGiaoRepo = hangChoGiaoRepo;
        }

        private BulkStockAdjustService CreateBulkService()
            => new BulkStockAdjustService(_bulkStockSlotRepo, _historyRepo, Uow);

        // ================================================================
        // CapNhapKho — nhánh HVN chính (SP atomic, có FIFO check, có ChoGiao)
        // ================================================================

        #region CapNhapKho

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, PhieuTableSet tables, out DataTable errors)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return CapNhapKho(gioGiaoFcc, nhaMay, tables.TmpTable, tables.DocQRTable, out errors);
        }

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            // ══════════════════════════════════════════════════════════
            // BƯỚC 3 (FIFO): chặn cứng TRƯỚC khi chạm STOCKTP/Slot.
            // Áp dụng ở đây vì operator TỰ CHỌN Lot qua quét QR → có khả năng
            // chọn sai Lot so với thứ tự nhập kho.
            // ══════════════════════════════════════════════════════════
            var fifoViolations = _validationRepo.CheckFifoViolations(tmpTable);
            if (fifoViolations.Count > 0)
            {
                errors = BuildFifoErrorTable(fifoViolations);
                return 0;
            }

            // SP trả DataSet đa bảng (stok + errors) — atomic ở tầng DB, gọi thẳng Db.
            DataSet ds = Db.ExecuteStoredProcedureDataSet(
                "Usp_Qrcode_Update_Stock2405",
                new SqlParameter("@GIOGIAOFCC", SqlDbType.NVarChar, 200) { Value = (object)(gioGiaoFcc ?? "") },
                new SqlParameter("@NHAMAY", SqlDbType.NVarChar, 200) { Value = (object)(nhaMay ?? "") },
                new SqlParameter("@TMPTABLE", SqlDbType.NVarChar, 128) { Value = tmpTable },
                new SqlParameter("@DOCQRTABLE", SqlDbType.NVarChar, 128) { Value = docQRTable },
                new SqlParameter("@LOT_KEY_LEN", SqlDbType.Int) { Value = LotCodeHelper.LEN_HEAD_FIXED }
            );

            DataTable stok = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
            errors = ds != null && ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();

            // ✅ FIX (nhỏ): dùng chung helper thay vì lặp code trừ A0 ở 3 nhánh
            bool coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);
            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();

            // ✅ FIX (nghiêm trọng #2): trước đây logic đóng ChoGiao + audit nằm
            // Ở NGUYÊN TẠI ĐÂY, giờ rút thành helper dùng chung cho cả 3 nhánh.
            HoanTatSauKhiTruKho(lotsDaXuatThanhCong, "SYSTEM_HVN_CNK");

            if (_cfg != null && _cfg.LoadTuBangRieng && !string.IsNullOrEmpty(_cfg.OrderTable) && stok.Rows.Count > 0)
            {
                foreach (DataRow row in stok.Rows)
                {
                    string maHang = row["MH"]?.ToString() ?? "";

                    int stt = 0;
                    if (row.Table.Columns.Contains("STT") && row["STT"] != DBNull.Value)
                        int.TryParse(row["STT"].ToString(), out stt);

                    if (string.IsNullOrEmpty(maHang))
                        continue;

                    string whereClause = stt > 0
                        ? $"STT={stt}"
                        : $"MAHANG='{SqlHelper.Esc(maHang)}' AND STATUS='OK'";

                    string ngayGiao = Convert.ToString(ExecuteScalar(
                        $"SELECT CONVERT(varchar, NGAYGIAO, 23) FROM [{tmpTable}] WHERE {whereClause}"))?.Trim() ?? "";

                    string poNo = Convert.ToString(ExecuteScalar(
                        $"SELECT ISNULL(PO_NO,'') FROM [{tmpTable}] WHERE {whereClause}"))?.Trim() ?? "";

                    if (!string.IsNullOrEmpty(poNo) && !string.IsNullOrEmpty(ngayGiao))
                        DanhDauDaGiao(poNo, maHang, ngayGiao, _cfg);
                }
            }

            return stok.Rows.Count;
        }

        #endregion

        // ================================================================
        // CapNhapKhoHTN — chỉ forward sang CapNhapKho, tự động thừa hưởng mọi fix
        // ================================================================

        #region CapNhapKhoHTN

        public int CapNhapKhoHTN(string nhaMay, PhieuTableSet tables, out DataTable errors)
        {
            if (tables == null)
                throw new ArgumentNullException(nameof(tables));

            return CapNhapKho("", nhaMay, tables.TmpTable, tables.DocQRTable, out errors);
        }

        public int CapNhapKhoHTN(string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
            => CapNhapKho("", nhaMay, tmpTable, docQRTable, out errors);

        #endregion

        // ================================================================
        // CapNhapKhoSP — SP đã tự chọn Lot FIFO + tự trừ STOCKTP.
        // KHÔNG check FIFO ở đây (operator không chọn Lot, không có khả năng
        // chọn sai). NHƯNG giờ có đóng ChoGiao + audit (trước đây thiếu).
        // ================================================================

        #region CapNhapKhoSP

        public int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors)
        {
            DataSet ds = Db.ExecuteStoredProcedureDataSet(
                "Usp_Qrcode_Update_Stock_SP",
                new SqlParameter("@GIOGIAOFCC", SqlDbType.NVarChar, 200) { Value = (object)(gioGiaoFcc ?? "") },
                new SqlParameter("@NHAMAY", SqlDbType.NVarChar, 200) { Value = (object)(nhaMay ?? "") }
            );

            DataTable stok = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
            errors = ds != null && ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();

            bool coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);
            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();

            // ✅ FIX (nghiêm trọng #2): trước đây HOÀN TOÀN THIẾU — ChoGiao bị treo
            // vĩnh viễn nếu nhánh SP cũng đi qua Pick→ChoGiao giống HVN.
            // ⚠️ Nếu xác nhận nhánh SP KHÔNG BAO GIỜ đi qua Pick→ChoGiao (luồng xuất
            // thẳng, không qua Slot), dòng dưới vẫn AN TOÀN (không có gì để đóng),
            // chỉ dư 1 lệnh query rỗng — không cần xoá.
            HoanTatSauKhiTruKho(lotsDaXuatThanhCong, "SYSTEM_SP_CNK");

            return stok.Rows.Count;
        }

        #endregion

        // ================================================================
        // CapNhapKhoYMVN — FIX nghiêm trọng #1 (transaction) + #3 (UPDATE TOP 1)
        // + có FIFO check (operator chọn Lot qua quét QR, giống HVN)
        // + có ChoGiao/audit (FIX #2)
        // ================================================================

        #region CapNhapKhoYMVN

        public bool CapNhapKhoYMVN(
            int stt,
            string lotSl,
            string maHang,
            string ngayGiao,
            string gioGiao,
            string nhaMay,
            out DS_ERR_CNK error)
        {
            error = null;

            if (_cfg == null)
                throw new InvalidOperationException("PhieuKhoRepository cần CustomerConfig để thực hiện CapNhapKhoYMVN.");

            string tmpTable = _cfg.TmpTable;
            string docQRTable = _cfg.DocQRTable;

            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            if (string.IsNullOrWhiteSpace(lotSl))
                return false;

            // ── BƯỚC 3 (FIFO): chặn TRƯỚC transaction — operator tự chọn Lot ──
            var fifoViolations = _validationRepo.CheckFifoViolations(tmpTable);
            if (fifoViolations.Count > 0)
            {
                var v = fifoViolations.FirstOrDefault(x =>
                    string.Equals(x.MaHang, maHang, StringComparison.OrdinalIgnoreCase));
                if (v != null)
                {
                    error = new DS_ERR_CNK
                    {
                        MH = v.MaHang,
                        LOT = v.LotDaChon,
                        Ms = $"Vi phạm FIFO — phải xuất Lot {v.LotDungRaPhaiChon} (Slot {v.SlotIdDungRaPhaiChon}) trước."
                    };
                    return false;
                }
            }

            // ── Kiểm tra đủ tồn TRƯỚC KHI mở transaction (fail fast, không giữ lock lâu) ──
            var lotsToProcess = new List<(string LotKey, int SoLuong)>();
            string[] lotParts = lotSl.Split(',');

            foreach (string part in lotParts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                string[] tach = part.Trim().Split('-');
                if (tach.Length < 2)
                    continue;

                string lot = LotCodeHelper.TrimTo(tach[0], LotCodeHelper.LEN_HEAD_FIXED);

                if (!int.TryParse(tach[1], out int sl) || sl <= 0)
                    continue;

                string matchCondition = LotCodeHelper.BuildLotMatchSql("LOT", $"'{SqlHelper.Esc(lot)}'");

                // ✅ FIX #3: SUM thay vì đọc 1 dòng — matchCondition (dựa theo prefix)
                // có thể khớp NHIỀU dòng STOCKTP cùng lúc, đọc 1 dòng sẽ ra tồn sai.
                object slConlaiRaw = ExecuteScalar($"SELECT ISNULL(SUM(SLCONLAI),0) FROM STOCKTP WHERE {matchCondition}");
                int slConlai = slConlaiRaw == null || slConlaiRaw == DBNull.Value ? 0 : Convert.ToInt32(slConlaiRaw);

                if (slConlai < sl)
                {
                    error = new DS_ERR_CNK
                    {
                        MH = maHang,
                        LOT = lot,
                        SLC = sl,
                        SLTK = slConlai,
                        SLT = sl - slConlai,
                        Ms = "Không đủ tồn kho"
                    };
                    return false;
                }

                lotsToProcess.Add((lot, sl));
            }

            // ══════════════════════════════════════════════════════════
            // FIX #1 (nghiêm trọng): TOÀN BỘ thao tác ghi bên dưới trước đây là
            // các ExecuteNonQuery rời rạc KHÔNG transaction — nếu lỗi giữa chừng
            // (vd. mất kết nối sau khi trừ STOCKTP nhưng trước khi ghi
            // LUUPHIEUGIAOHANG), dữ liệu rơi vào trạng thái nửa vời không thể
            // phục hồi. Giờ gói toàn bộ trong Uow.Begin()/Commit()/Rollback().
            // ══════════════════════════════════════════════════════════
            var bulkService = CreateBulkService();
            bool coAnhHuongA0 = false;
            var lotsDaXuatThanhCong = new List<string>();

            Uow.Begin();
            try
            {
                foreach (var (lotKey, sl) in lotsToProcess)
                {
                    string matchCondition = LotCodeHelper.BuildLotMatchSql("LOT", $"'{SqlHelper.Esc(lotKey)}'");

                    // ✅ FIX #3: trừ tuần tự theo FIFO (NGAYNHAP ASC) qua TỪNG dòng
                    // khớp, thay vì "UPDATE TOP 1" chọn 1 dòng không xác định thứ tự
                    // khi matchCondition khớp nhiều dòng STOCKTP cùng lúc.
                    if (!TruStockTpFifo(matchCondition, sl))
                        throw new InvalidOperationException(
                            $"Tồn kho Lot [{lotKey}] đã thay đổi trong lúc xử lý — vui lòng thử lại.");

                    if (bulkService.TruKhoAoTheoLot(lotKey, sl))
                        coAnhHuongA0 = true;

                    lotsDaXuatThanhCong.Add(lotKey);
                }

                ExecuteNonQuery(
                    @"INSERT INTO LUUDOCQRCODE
                    (LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
                     STATUS, MAFCC, STT, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY)
                    SELECT
                        LEFT(LOTFCC, 500), LEFT(MAHANGFCC, 60), SLTEMFCC,
                        LEFT(LOTHVN, 500), LEFT(MAHANGHVN, 60), SLTEMHVN,
                        STATUS, LEFT(MAFCC, 50), STT, KETQUA,
                        @ngayGiao, @gioGiao, @nhaMay
                    FROM [" + docQRTable + @"]
                    WHERE MAHANGFCC = @maHang AND KETQUA = 'DG'",
                    new SqlParameter("@ngayGiao", SqlDbType.NVarChar, 50) { Value = ngayGiao ?? "" },
                    new SqlParameter("@gioGiao", SqlDbType.NVarChar, 50) { Value = gioGiao ?? "" },
                    new SqlParameter("@nhaMay", SqlDbType.NVarChar, 200) { Value = nhaMay ?? "" },
                    new SqlParameter("@maHang", SqlDbType.NVarChar, 100) { Value = maHang ?? "" });

                ExecuteNonQuery(
                    @"INSERT INTO LUUPHIEUGIAOHANG
                    (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                     NGAYGIAO, GIOGIAO, STATUS, GearYMVN, NHAMAY, GIOGIAOFCC, PO_NO, TTPHIEU)
                    SELECT
                        STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                        NGAYGIAO, GIOGIAO, 'OK', ISNULL(GEAR,''),
                        @nhaMay, CONVERT(VARCHAR(8), GETDATE(), 108),
                        ISNULL(PO_NO,''), ISNULL(TTPHIEU,'')
                    FROM [" + tmpTable + @"]
                    WHERE STT = @stt AND STATUS = 'NG'",
                    new SqlParameter("@nhaMay", SqlDbType.NVarChar, 200) { Value = nhaMay ?? "" },
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt });

                ExecuteNonQuery(
                    $"UPDATE [{tmpTable}] SET STATUS = 'OK' WHERE STT = @stt",
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt });

                ExecuteNonQuery(
                    $"DELETE FROM [{docQRTable}] WHERE MAHANGFCC = @maHang AND KETQUA = 'DG'",
                    new SqlParameter("@maHang", SqlDbType.NVarChar, 100) { Value = maHang ?? "" });

                Uow.Commit();
            }
            catch
            {
                Uow.Rollback();
                throw;
            }

            // ✅ FIX (nghiêm trọng #2): trước đây HOÀN TOÀN THIẾU ở nhánh YMVN.
            // Chạy SAU KHI transaction chính đã commit — là 1 transaction riêng,
            // không lồng vào transaction trên.
            HoanTatSauKhiTruKho(lotsDaXuatThanhCong, "SYSTEM_YMVN_CNK");

            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();

            string poNo = Convert.ToString(ExecuteScalar(
                $"SELECT ISNULL(PO_NO,'') FROM [{tmpTable}] WHERE STT = @stt",
                new SqlParameter("@stt", SqlDbType.Int) { Value = stt }))?.Trim() ?? "";

            if (!string.IsNullOrEmpty(poNo) && !string.IsNullOrEmpty(_cfg.OrderTable))
                DanhDauDaGiao(poNo, maHang, ngayGiao, _cfg);

            return true;
        }

        #endregion

        // ================================================================
        // DanhDauDaGiao
        // ================================================================

        #region DanhDauDaGiao

        public void DanhDauDaGiao(string poNo, string maHang, string ngayGiao, CustomerConfig cfg)
        {
            if (cfg == null) return;
            if (string.IsNullOrEmpty(cfg.OrderTable)) return;

            Db.ValidateTableName(cfg.OrderTable);

            ExecuteNonQuery(
                $"UPDATE [{cfg.OrderTable}] " +
                "SET IsDelivered = 1, DeliveredDate = GETDATE() " +
                "WHERE Oder_no = @po AND Part_no = @pno " +
                "  AND CAST(NgayGiao AS DATE) = @ng AND IsDelivered = 0",
                new SqlParameter("@po", SqlDbType.NVarChar, 100) { Value = poNo ?? "" },
                new SqlParameter("@pno", SqlDbType.NVarChar, 100) { Value = maHang ?? "" },
                new SqlParameter("@ng", SqlDbType.Date) { Value = ngayGiao ?? "" });
        }

        #endregion

        public DataTable LoadHangThieu(bool isMayBanQR, string tenBan)
        {
            if (isMayBanQR)
                return ExecuteStoredProcedure("Usp_Qrcode_LOAD_HANGTHIEU");

            if (string.IsNullOrWhiteSpace(tenBan))
                throw new ArgumentException("Tên bảng không được rỗng.", nameof(tenBan));

            Db.ValidateTableName(tenBan);

            return ExecuteStoredProcedure(
                "Usp_Qrcode_LOAD_HANGTHIEUView",
                new SqlParameter("@TENBAN", tenBan));
        }

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        #region Private Helpers

        private DataTable BuildFifoErrorTable(List<FifoViolation> violations)
        {
            var dt = new DataTable();
            dt.Columns.Add("MH", typeof(string));
            dt.Columns.Add("LOT", typeof(string));
            dt.Columns.Add("Ms", typeof(string));

            foreach (var v in violations)
                dt.Rows.Add(v.MaHang, v.LotDaChon,
                    $"Vi phạm FIFO — phải xuất Lot {v.LotDungRaPhaiChon} (Slot {v.SlotIdDungRaPhaiChon}) trước.");

            return dt;
        }

        /// <summary>
        /// FIX (nhỏ): rút gọn khối "loop stok.Rows → TrimTo → TruKhoAoTheoLot →
        /// cờ ảnh hưởng A0" — trước đây lặp lại y hệt ở CapNhapKho và CapNhapKhoSP.
        /// Trả về danh sách Lot đã trừ thành công để HoanTatSauKhiTruKho dùng tiếp.
        /// </summary>
        private bool TruKhoAoTuKetQuaSP(DataTable stok, out List<string> lotsDaXuatThanhCong)
        {
            lotsDaXuatThanhCong = new List<string>();
            bool coAnhHuongA0 = false;

            if (stok == null || stok.Rows.Count == 0
                || !stok.Columns.Contains("LOT") || !stok.Columns.Contains("SOLUONG"))
                return false;

            var bulkService = CreateBulkService();

            foreach (DataRow row in stok.Rows)
            {
                string lot = LotCodeHelper.TrimTo(row["LOT"]?.ToString(), LotCodeHelper.LEN_HEAD_FIXED);
                int sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                if (string.IsNullOrWhiteSpace(lot) || sl <= 0) continue;

                if (bulkService.TruKhoAoTheoLot(lot, sl))
                    coAnhHuongA0 = true;

                lotsDaXuatThanhCong.Add(lot);
            }

            return coAnhHuongA0;
        }

        /// <summary>
        /// FIX (nghiêm trọng #3): trừ STOCKTP theo FIFO qua TỪNG dòng khớp
        /// matchCondition (thay vì "UPDATE TOP 1" chọn 1 dòng không xác định thứ
        /// tự khi có nhiều dòng cùng khớp — do matchCondition so theo khoá rút gọn,
        /// không phải khớp tuyệt đối 1-1 với PK LOT thật).
        /// </summary>
        private bool TruStockTpFifo(string matchCondition, int slCan)
        {
            DataTable rows = LoadData(
                $"SELECT LOT, SLCONLAI FROM STOCKTP WHERE {matchCondition} AND SLCONLAI > 0 ORDER BY NGAYNHAP ASC");

            int conLaiCanTru = slCan;
            foreach (DataRow row in rows.Rows)
            {
                if (conLaiCanTru <= 0) break;

                string lotThat = row["LOT"].ToString();
                int slDongNay = Convert.ToInt32(row["SLCONLAI"]);
                int slTruDongNay = Math.Min(slDongNay, conLaiCanTru);

                ExecuteNonQuery(
                    "UPDATE STOCKTP SET SLXUAT = ISNULL(SLXUAT,0) + @sl, SLCONLAI = SLCONLAI - @sl, NGAYXUAT = GETDATE() " +
                    "WHERE LOT = @lot",
                    new SqlParameter("@sl", slTruDongNay),
                    new SqlParameter("@lot", lotThat));

                conLaiCanTru -= slTruDongNay;
            }

            return conLaiCanTru == 0;
        }

        /// <summary>
        /// FIX (nghiêm trọng #2): bước hoàn tất DÙNG CHUNG sau khi đã trừ STOCKTP
        /// (bất kể qua SP hay qua vòng lặp C#) — đóng các dòng FVN_HangChoGiao
        /// tương ứng và ghi StockHistory audit. Trước đây CHỈ tồn tại (inline)
        /// trong CapNhapKho — CapNhapKhoSP/CapNhapKhoYMVN thiếu hoàn toàn, khiến
        /// FVN_HangChoGiao có thể treo "chờ giao" vĩnh viễn dù hàng đã CNK xong.
        ///
        /// An toàn khi gọi cho nhánh KHÔNG đi qua Pick→ChoGiao: nếu không có dòng
        /// FVN_HangChoGiao nào khớp Lot, CloseChoGiaoTheoLotAndReturn trả về danh
        /// sách rỗng — không có tác dụng phụ ngoài ý muốn.
        /// </summary>
        private void HoanTatSauKhiTruKho(List<string> lotsDaXuatThanhCong, string performedBy)
        {
            if (lotsDaXuatThanhCong == null || lotsDaXuatThanhCong.Count == 0 || _hangChoGiaoRepo == null)
                return;

            try
            {
                List<HangChoGiao> closedItems;

                Uow.Begin();
                try
                {
                    closedItems = _hangChoGiaoRepo.CloseChoGiaoTheoLotAndReturn(
                        Connection, Transaction, lotsDaXuatThanhCong, performedBy);
                    Uow.Commit();
                }
                catch
                {
                    Uow.Rollback();
                    throw;
                }

                if (closedItems != null)
                {
                    foreach (var it in closedItems.Where(x => x.SlotIdNguon.HasValue))
                    {
                        _historyRepo.SaveHistory(
                            "EXPORT_CONFIRMED_HVN",
                            it.MaHang,
                            new LotInfo
                            {
                                LotNo = it.LotGoc,
                                Quantity = it.SoLuong,
                                TemCode = it.LotThung
                            },
                            fromSlotId: it.SlotIdNguon,
                            toSlotId: null,
                            performedBy: performedBy);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HoanTatSauKhiTruKho] Không đóng được TMPCHOGIAO: {ex.Message}");
            }
        }

        #endregion
    }
}