using PCTP.Common;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using PCTP.Shared.Notifiers;
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
        private readonly IStockMovementService _stockMovement;
        private const string SYSTEM_PERFORMED_BY = "SYSTEM_GIAOHANG_CNK";

        public PhieuKhoRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            IBulkStockSlotRepository bulkStockSlotRepo,
            IStockHistoryRepository historyRepo,
            IPhieuValidationRepository validationRepo,
            CustomerConfig cfg = null,
            IHangChoGiaoRepository hangChoGiaoRepo = null,
            IStockMovementService stockMovement = null)
            : base(db, uow)
        {
            _bulkStockSlotRepo = bulkStockSlotRepo ?? throw new ArgumentNullException(nameof(bulkStockSlotRepo));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _validationRepo = validationRepo ?? throw new ArgumentNullException(nameof(validationRepo));
            _cfg = cfg;
            _hangChoGiaoRepo = hangChoGiaoRepo;
            _stockMovement = stockMovement;
        }

        private BulkStockAdjustService CreateBulkService()
            => new BulkStockAdjustService(_bulkStockSlotRepo, _historyRepo, Uow, _stockMovement);

        #region CapNhapKho
        public int CapNhapKho(string gioGiaoFcc, string nhaMay, PhieuTableSet tables, out DataTable errors)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return CapNhapKho(gioGiaoFcc, nhaMay, tables.TmpTable, tables.DocQRTable, out errors);
        }

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            // FIFO is the authoritative gate in PhieuKhoService/PhieuRepository.ReleaseFifoViolations.
            // Do NOT call CheckFifoViolations here: doing so would recreate the old hard-stop path
            // and would prevent valid QR rows from being exported when another row violates FIFO.
            DataTable stok;
            bool coAnhHuongA0 = false;
            Uow.Begin();
            try
            {
                DataSet ds = ExecuteStoredProcedureDataSet(
                    "Usp_Qrcode_Update_Stock2405",
                    new SqlParameter("@GIOGIAOFCC", SqlDbType.NVarChar, 200) { Value = (object)(gioGiaoFcc ?? "") },
                    new SqlParameter("@NHAMAY", SqlDbType.NVarChar, 200) { Value = (object)(nhaMay ?? "") },
                    new SqlParameter("@TMPTABLE", SqlDbType.NVarChar, 128) { Value = tmpTable },
                    new SqlParameter("@DOCQRTABLE", SqlDbType.NVarChar, 128) { Value = docQRTable }
                );

                stok = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
                errors = ds != null && ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
                coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);
                HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);

                if (_cfg != null && _cfg.Delivery.LoadTuBangRieng &&
                    !string.IsNullOrEmpty(_cfg.Delivery.OrderTable) && stok.Rows.Count > 0)
                {
                    foreach (DataRow row in stok.Rows)
                    {
                        string maHang = row["MH"]?.ToString() ?? "";
                        int stt = 0;
                        if (row.Table.Columns.Contains("STT") && row["STT"] != DBNull.Value)
                            int.TryParse(row["STT"].ToString(), out stt);
                        if (string.IsNullOrEmpty(maHang)) continue;

                        string whereClause = stt > 0 ? $"STT={stt}" : $"MAHANG='{SqlHelper.Esc(maHang)}' AND STATUS='OK'";
                        // Một dòng TMP duy nhất cho cả PO/NGAYGIAO(có giờ)/GIOGIAO: lấy cùng một lần để
                        // không ghép PO của dòng này với giờ của dòng khác.
                        DataTable tmpRow = LoadData(
                            $"SELECT TOP 1 ISNULL(PO_NO,'') AS PO_NO, NGAYGIAO, ISNULL(GIOGIAO,'') AS GIOGIAO FROM [{tmpTable}] WHERE {whereClause} ORDER BY STT");
                        OrderLineKey key = BuildOrderLineKeyFromTmp(tmpRow, maHang);
                        if (key != null)
                            DanhDauDaGiao(key, _cfg);
                    }
                }
                Uow.Commit();
            }
            catch
            {
                Uow.Rollback();
                throw;
            }

            if (coAnhHuongA0) StockChangedNotifier.RaiseStockChanged();
            return stok.Rows.Count;
        }
        #endregion

        #region CapNhapKhoHTN
        public int CapNhapKhoHTN(string nhaMay, PhieuTableSet tables, out DataTable errors)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            return CapNhapKho("", nhaMay, tables.TmpTable, tables.DocQRTable, out errors);
        }

        public int CapNhapKhoHTN(string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
            => CapNhapKho("", nhaMay, tmpTable, docQRTable, out errors);
        #endregion

        #region CapNhapKhoSP
        public int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors)
        {
            string tmpTable = _cfg != null ? _cfg.Delivery.TmpTable : "TMPPHIEUGIAOHANG";
            string docQRTable = _cfg != null ? _cfg.Delivery.DocQRTable : "DOCQRCODE";
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

            // The same DB FIFO gate is executed by PhieuKhoService before this repository call.
            // This method must only execute the stock SP with the already-released TMP rows.
            DataTable stok;
            bool coAnhHuongA0 = false;
            Uow.Begin();
            try
            {
                DataSet ds = ExecuteStoredProcedureDataSet(
                    "Usp_Qrcode_Update_Stock_SP",
                    new SqlParameter("@GIOGIAOFCC", SqlDbType.NVarChar, 200) { Value = (object)(gioGiaoFcc ?? "") },
                    new SqlParameter("@NHAMAY", SqlDbType.NVarChar, 200) { Value = (object)(nhaMay ?? "") }
                );
                stok = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
                errors = ds != null && ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
                coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);
                HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);
                Uow.Commit();
            }
            catch
            {
                Uow.Rollback();
                throw;
            }

            if (coAnhHuongA0) StockChangedNotifier.RaiseStockChanged();
            return stok.Rows.Count;
        }
        #endregion

        #region CapNhapKhoYMVN
        public bool CapNhapKhoYMVN(int stt, string lotSl, string maHang, string ngayGiao, string gioGiao, string nhaMay, out DS_ERR_CNK error)
        {
            error = null;
            if (_cfg == null) throw new InvalidOperationException("PhieuKhoRepository cần CustomerConfig để thực hiện CapNhapKhoYMVN.");

            string tmpTable = _cfg.Delivery.TmpTable;
            string docQRTable = _cfg.Delivery.DocQRTable;
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);
            if (string.IsNullOrWhiteSpace(lotSl)) return false;

            // CapNhapKhoYMVN is a direct/manual stock path. Keep the same final FIFO rule:
            // the caller must release invalid TMP rows before invoking this method.
            var lotsToProcess = new List<(string LotKey, int SoLuong)>();
            foreach (string rawPart in lotSl.Split(','))
            {
                if (string.IsNullOrWhiteSpace(rawPart)) continue;

                string part = rawPart.Trim();
                int separator = part.LastIndexOf('-');
                if (separator <= 0 || separator >= part.Length - 1 ||
                    !int.TryParse(part.Substring(separator + 1).Trim(), out int sl) || sl <= 0)
                {
                    error = new DS_ERR_CNK
                    {
                        MH = maHang,
                        LOT = part,
                        SLC = 0,
                        SLTK = 0,
                        SLT = 0,
                        Ms = "Định dạng LOT-số lượng không hợp lệ"
                    };
                    return false;
                }

                string lot = LotCodeHelper.TrimTo(part.Substring(0, separator).Trim(), LotCodeHelper.LEN_HEAD_FIXED);
                if (string.IsNullOrWhiteSpace(lot))
                {
                    error = new DS_ERR_CNK
                    {
                        MH = maHang,
                        LOT = part,
                        SLC = sl,
                        SLTK = 0,
                        SLT = sl,
                        Ms = "LOT không hợp lệ"
                    };
                    return false;
                }

                lotsToProcess.Add((lot, sl));
            }

            if (lotsToProcess.Count == 0)
            {
                error = new DS_ERR_CNK
                {
                    MH = maHang,
                    LOT = lotSl,
                    SLC = 0,
                    SLTK = 0,
                    SLT = 0,
                    Ms = "Không có LOT hợp lệ để xuất kho"
                };
                return false;
            }

            var bulkService = CreateBulkService();
            bool coAnhHuongA0 = false;
            var lotsDaXuatThanhCong = new List<string>();

            Uow.Begin();
            try
            {
                foreach (var item in lotsToProcess)
                {
                    string lotKey = item.LotKey;
                    int sl = item.SoLuong;
                    string matchCondition = LotCodeHelper.BuildLotMatchSql("LOT", $"'{SqlHelper.Esc(lotKey)}'");
                    object slConlaiRaw = ExecuteScalar(
                        $"SELECT ISNULL(SUM(SLCONLAI),0) FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE {matchCondition}");
                    int slConlai = slConlaiRaw == null || slConlaiRaw == DBNull.Value ? 0 : Convert.ToInt32(slConlaiRaw);
                    if (slConlai < sl)
                    {
                        error = new DS_ERR_CNK { MH = maHang, LOT = lotKey, SLC = sl, SLTK = slConlai, SLT = sl - slConlai, Ms = "Không đủ tồn kho" };
                        Uow.Rollback();
                        return false;
                    }
                    if (!TruStockTpFifo(matchCondition, sl))
                        throw new InvalidOperationException($"Tồn kho Lot [{lotKey}] đã thay đổi trong lúc xử lý — vui lòng thử lại.");

                    if (bulkService.TruKhoAoTheoLot(lotKey, sl, manageTransaction: false)) coAnhHuongA0 = true;
                    lotsDaXuatThanhCong.Add(lotKey);
                }

                ExecuteNonQuery(
                    @"INSERT INTO LUUDOCQRCODE
                    (LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
                     STATUS, MAFCC, STT, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY)
                    SELECT LEFT(LOTFCC, 500), LEFT(MAHANGFCC, 60), SLTEMFCC,
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
                    SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                           NGAYGIAO, GIOGIAO, 'OK', ISNULL(GEAR,''), @nhaMay,
                           CONVERT(VARCHAR(8), GETDATE(), 108), ISNULL(PO_NO,''), ISNULL(TTPHIEU,'')
                    FROM [" + tmpTable + @"]
                    WHERE STT = @stt AND STATUS = 'NG'",
                    new SqlParameter("@nhaMay", SqlDbType.NVarChar, 200) { Value = nhaMay ?? "" },
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt });

                int updatedTmp = ExecuteNonQuery($"UPDATE [{tmpTable}] SET STATUS = 'OK' WHERE STT = @stt AND STATUS = 'NG'", new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                if (updatedTmp != 1)
                    throw new InvalidOperationException($"Không thể xác nhận phiếu giao STT={stt}: trạng thái đã thay đổi hoặc không còn tồn tại.");
                ExecuteNonQuery($"DELETE FROM [{docQRTable}] WHERE MAHANGFCC = @maHang AND KETQUA = 'DG'", new SqlParameter("@maHang", SqlDbType.NVarChar, 100) { Value = maHang ?? "" });

                HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);

                if (!string.IsNullOrEmpty(_cfg.Delivery.OrderTable))
                {
                    DataTable tmpRow = LoadData(
                        $"SELECT ISNULL(PO_NO,'') AS PO_NO, NGAYGIAO, ISNULL(GIOGIAO,'') AS GIOGIAO FROM [{tmpTable}] WHERE STT = @stt",
                        new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                    OrderLineKey key = BuildOrderLineKeyFromTmp(tmpRow, maHang, gioGiao);
                    if (key != null)
                        DanhDauDaGiao(key, _cfg);
                }

                Uow.Commit();
            }
            catch
            {
                Uow.Rollback();
                throw;
            }

            if (coAnhHuongA0) StockChangedNotifier.RaiseStockChanged();
            return true;
        }
        #endregion

        #region DanhDauDaGiao
        /// <summary>
        /// Đánh dấu đã giao đúng một dòng đơn (PO + mã hàng + ngày + giờ:phút).
        /// Trước đây chỉ lọc PO + mã hàng + NGÀY nên giao đợt 08:00 cũng đánh dấu luôn đợt 14:00.
        /// </summary>
        public int DanhDauDaGiao(OrderLineKey key, CustomerConfig cfg)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (cfg == null || string.IsNullOrEmpty(cfg.Delivery.OrderTable)) return 0;
            Db.ValidateTableName(cfg.Delivery.OrderTable);
            return ExecuteNonQuery(
                OrderTableSql.MarkDelivered(cfg.Delivery.OrderTable),
                new SqlParameter("@po", SqlDbType.NVarChar, 100) { Value = key.PoNo },
                new SqlParameter("@pno", SqlDbType.NVarChar, 100) { Value = key.MaHang },
                new SqlParameter("@ng", SqlDbType.Date) { Value = key.NgayGiao },
                new SqlParameter("@hh", SqlDbType.Int) { Value = key.GioGiao.Hours },
                new SqlParameter("@mi", SqlDbType.Int) { Value = key.GioGiao.Minutes });
        }

        /// <summary>
        /// Dựng khoá từ dòng TMP. Trả về null khi dòng không có PO hoặc không có ngày giao
        /// (giữ nguyên hành vi cũ: không đánh dấu). PO + ngày có nhưng không đọc được thì ném lỗi,
        /// vì lặng lẽ đoán giờ sẽ đánh dấu nhầm đợt giao khác.
        /// </summary>
        private static OrderLineKey BuildOrderLineKeyFromTmp(DataTable tmpRow, string maHang, string gioGiaoOverride = null)
        {
            if (tmpRow == null || tmpRow.Rows.Count == 0) return null;
            DataRow row = tmpRow.Rows[0];

            string poNo = Convert.ToString(row["PO_NO"])?.Trim() ?? "";
            if (poNo.Length == 0 || string.IsNullOrWhiteSpace(maHang) || row["NGAYGIAO"] == DBNull.Value) return null;

            string gio = string.IsNullOrWhiteSpace(gioGiaoOverride)
                ? Convert.ToString(row["GIOGIAO"])
                : gioGiaoOverride;
            return OrderLineKey.FromTmpValues(poNo, maHang, row["NGAYGIAO"], gio);
        }
        #endregion

        public DataTable LoadHangThieu(bool isMayBanQR, string tenBan)
        {
            if (isMayBanQR) return ExecuteStoredProcedure("Usp_Qrcode_LOAD_HANGTHIEU");
            if (string.IsNullOrWhiteSpace(tenBan)) throw new ArgumentException("Tên bảng không được rỗng.", nameof(tenBan));
            Db.ValidateTableName(tenBan);
            return ExecuteStoredProcedure("Usp_Qrcode_LOAD_HANGTHIEUView", new SqlParameter("@TENBAN", tenBan));
        }

        #region Private Helpers
        private bool TruKhoAoTuKetQuaSP(DataTable stok, out List<string> lotsDaXuatThanhCong)
        {
            lotsDaXuatThanhCong = new List<string>();
            bool coAnhHuongA0 = false;
            if (stok == null || stok.Rows.Count == 0) return false;
            if (!stok.Columns.Contains("LOT") || !stok.Columns.Contains("SOLUONG"))
                throw new InvalidOperationException(
                    "Stored procedure trả về dữ liệu xuất kho nhưng thiếu cột LOT/SOLUONG; huỷ giao dịch để tránh lệch STOCKTP và SlotLot.");

            var bulkService = CreateBulkService();
            foreach (DataRow row in stok.Rows)
            {
                string lot = LotCodeHelper.TrimTo(row["LOT"]?.ToString(), LotCodeHelper.LEN_HEAD_FIXED);
                int sl;
                try
                {
                    sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Stored procedure trả về SOLUONG không hợp lệ; huỷ giao dịch xuất kho.", ex);
                }

                if (string.IsNullOrWhiteSpace(lot) || sl <= 0)
                    throw new InvalidOperationException(
                        $"Stored procedure trả về LOT/SOLUONG không hợp lệ (LOT='{lot}', SOLUONG={sl}); huỷ giao dịch xuất kho.");

                if (bulkService.TruKhoAoTheoLot(lot, sl, manageTransaction: false))
                    coAnhHuongA0 = true;
                lotsDaXuatThanhCong.Add(lot);
            }
            return coAnhHuongA0;
        }

        private bool TruStockTpFifo(string matchCondition, int slCan)
        {
            DataTable rows = LoadData($"SELECT LOT, SLCONLAI FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE {matchCondition} AND SLCONLAI > 0 ORDER BY NGAYNHAP ASC");
            int conLaiCanTru = slCan;
            foreach (DataRow row in rows.Rows)
            {
                if (conLaiCanTru <= 0) break;
                string lotThat = row["LOT"].ToString();
                int slDongNay = Convert.ToInt32(row["SLCONLAI"]);
                int slTruDongNay = Math.Min(slDongNay, conLaiCanTru);
                int affected = ExecuteNonQuery(
                    "UPDATE STOCKTP SET SLXUAT = ISNULL(SLXUAT,0) + @sl, SLCONLAI = SLCONLAI - @sl, NGAYXUAT = GETDATE() WHERE LOT = @lot AND SLCONLAI >= @sl",
                    new SqlParameter("@sl", slTruDongNay), new SqlParameter("@lot", lotThat));
                if (affected != 1) return false;
                conLaiCanTru -= slTruDongNay;
            }
            return conLaiCanTru == 0;
        }

        private void HoanTatSauKhiTruKho(List<string> lotsDaXuatThanhCong, string performedBy)
        {
            if (lotsDaXuatThanhCong == null || lotsDaXuatThanhCong.Count == 0) return;
            if (!Uow.IsInTransaction)
                throw new InvalidOperationException("HoanTatSauKhiTruKho phải tham gia transaction giao hàng hiện tại.");
            if (_hangChoGiaoRepo == null)
                throw new InvalidOperationException("Chưa cấu hình IHangChoGiaoRepository; không thể hoàn tất hàng chờ giao một cách an toàn.");

            List<HangChoGiao> closedItems = _hangChoGiaoRepo.CloseChoGiaoTheoLotAndReturn(
                Connection, Transaction, lotsDaXuatThanhCong, performedBy);
            if (closedItems == null) return;

            foreach (var it in closedItems.Where(x => x.SlotIdNguon.HasValue))
            {
                _historyRepo.SaveHistory(
                    "EXPORT_CONFIRMED_HVN", it.MaHang,
                    new LotInfo { LotNo = it.LotGoc, Quantity = it.SoLuong, TemCode = it.LotThung },
                    fromSlotId: it.SlotIdNguon, toSlotId: null, performedBy: performedBy);
            }
        }
        #endregion
    }
}
