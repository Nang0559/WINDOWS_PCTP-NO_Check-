using PCTP.Common;
using PCTP.FuctionMain;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
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
            DataSet ds = Db.ExecuteStoredProcedureDataSet(
                "Usp_Qrcode_Update_Stock2405",
                new SqlParameter("@GIOGIAOFCC", SqlDbType.NVarChar, 200) { Value = (object)(gioGiaoFcc ?? "") },
                new SqlParameter("@NHAMAY", SqlDbType.NVarChar, 200) { Value = (object)(nhaMay ?? "") },
                new SqlParameter("@TMPTABLE", SqlDbType.NVarChar, 128) { Value = tmpTable },
                new SqlParameter("@DOCQRTABLE", SqlDbType.NVarChar, 128) { Value = docQRTable }
            );

            DataTable stok = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
            errors = ds != null && ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();

            bool coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);
            if (coAnhHuongA0) StockChangedNotifier.RaiseStockChanged();
            HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);

            if (_cfg != null && _cfg.Delivery.LoadTuBangRieng && !string.IsNullOrEmpty(_cfg.Delivery.OrderTable) && stok.Rows.Count > 0)
            {
                foreach (DataRow row in stok.Rows)
                {
                    string maHang = row["MH"]?.ToString() ?? "";
                    int stt = 0;
                    if (row.Table.Columns.Contains("STT") && row["STT"] != DBNull.Value)
                        int.TryParse(row["STT"].ToString(), out stt);
                    if (string.IsNullOrEmpty(maHang)) continue;

                    string whereClause = stt > 0 ? $"STT={stt}" : $"MAHANG='{SqlHelper.Esc(maHang)}' AND STATUS='OK'";
                    string ngayGiao = Convert.ToString(ExecuteScalar($"SELECT CONVERT(varchar, NGAYGIAO, 23) FROM [{tmpTable}] WHERE {whereClause}"))?.Trim() ?? "";
                    string poNo = Convert.ToString(ExecuteScalar($"SELECT ISNULL(PO_NO,'') FROM [{tmpTable}] WHERE {whereClause}"))?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(poNo) && !string.IsNullOrEmpty(ngayGiao))
                        DanhDauDaGiao(poNo, maHang, ngayGiao, _cfg);
                }
            }
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
            DataSet ds = Db.ExecuteStoredProcedureDataSet(
                "Usp_Qrcode_Update_Stock_SP",
                new SqlParameter("@GIOGIAOFCC", SqlDbType.NVarChar, 200) { Value = (object)(gioGiaoFcc ?? "") },
                new SqlParameter("@NHAMAY", SqlDbType.NVarChar, 200) { Value = (object)(nhaMay ?? "") }
            );
            DataTable stok = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
            errors = ds != null && ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
            bool coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);
            if (coAnhHuongA0) StockChangedNotifier.RaiseStockChanged();
            HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);
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
            foreach (string part in lotSl.Split(','))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                string[] tach = part.Trim().Split('-');
                if (tach.Length < 2) continue;
                string lot = LotCodeHelper.TrimTo(tach[0], LotCodeHelper.LEN_HEAD_FIXED);
                if (!int.TryParse(tach[1], out int sl) || sl <= 0) continue;

                string matchCondition = LotCodeHelper.BuildLotMatchSql("LOT", $"'{SqlHelper.Esc(lot)}'");
                object slConlaiRaw = ExecuteScalar($"SELECT ISNULL(SUM(SLCONLAI),0) FROM STOCKTP WHERE {matchCondition}");
                int slConlai = slConlaiRaw == null || slConlaiRaw == DBNull.Value ? 0 : Convert.ToInt32(slConlaiRaw);
                if (slConlai < sl)
                {
                    error = new DS_ERR_CNK { MH = maHang, LOT = lot, SLC = sl, SLTK = slConlai, SLT = sl - slConlai, Ms = "Không đủ tồn kho" };
                    return false;
                }
                lotsToProcess.Add((lot, sl));
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

                ExecuteNonQuery($"UPDATE [{tmpTable}] SET STATUS = 'OK' WHERE STT = @stt", new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                ExecuteNonQuery($"DELETE FROM [{docQRTable}] WHERE MAHANGFCC = @maHang AND KETQUA = 'DG'", new SqlParameter("@maHang", SqlDbType.NVarChar, 100) { Value = maHang ?? "" });
                Uow.Commit();
            }
            catch
            {
                Uow.Rollback();
                throw;
            }

            HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);
            if (coAnhHuongA0) StockChangedNotifier.RaiseStockChanged();

            string poNo = Convert.ToString(ExecuteScalar(
                $"SELECT ISNULL(PO_NO,'') FROM [{tmpTable}] WHERE STT = @stt",
                new SqlParameter("@stt", SqlDbType.Int) { Value = stt }))?.Trim() ?? "";
            if (!string.IsNullOrEmpty(poNo) && !string.IsNullOrEmpty(_cfg.Delivery.OrderTable))
                DanhDauDaGiao(poNo, maHang, ngayGiao, _cfg);
            return true;
        }
        #endregion

        #region DanhDauDaGiao
        public void DanhDauDaGiao(string poNo, string maHang, string ngayGiao, CustomerConfig cfg)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.Delivery.OrderTable)) return;
            Db.ValidateTableName(cfg.Delivery.OrderTable);
            ExecuteNonQuery(
                $"UPDATE [{cfg.Delivery.OrderTable}] SET IsDelivered = 1, DeliveredDate = GETDATE() " +
                "WHERE Oder_no = @po AND Part_no = @pno AND CAST(NgayGiao AS DATE) = @ng AND IsDelivered = 0",
                new SqlParameter("@po", SqlDbType.NVarChar, 100) { Value = poNo ?? "" },
                new SqlParameter("@pno", SqlDbType.NVarChar, 100) { Value = maHang ?? "" },
                new SqlParameter("@ng", SqlDbType.Date) { Value = ngayGiao ?? "" });
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
            if (stok == null || stok.Rows.Count == 0 || !stok.Columns.Contains("LOT") || !stok.Columns.Contains("SOLUONG")) return false;

            var bulkService = CreateBulkService();
            foreach (DataRow row in stok.Rows)
            {
                string lot = LotCodeHelper.TrimTo(row["LOT"]?.ToString(), LotCodeHelper.LEN_HEAD_FIXED);
                int sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                if (string.IsNullOrWhiteSpace(lot) || sl <= 0) continue;
                if (bulkService.TruKhoAoTheoLot(lot, sl)) coAnhHuongA0 = true;
                lotsDaXuatThanhCong.Add(lot);
            }
            return coAnhHuongA0;
        }

        private bool TruStockTpFifo(string matchCondition, int slCan)
        {
            DataTable rows = LoadData($"SELECT LOT, SLCONLAI FROM STOCKTP WHERE {matchCondition} AND SLCONLAI > 0 ORDER BY NGAYNHAP ASC");
            int conLaiCanTru = slCan;
            foreach (DataRow row in rows.Rows)
            {
                if (conLaiCanTru <= 0) break;
                string lotThat = row["LOT"].ToString();
                int slDongNay = Convert.ToInt32(row["SLCONLAI"]);
                int slTruDongNay = Math.Min(slDongNay, conLaiCanTru);
                ExecuteNonQuery(
                    "UPDATE STOCKTP SET SLXUAT = ISNULL(SLXUAT,0) + @sl, SLCONLAI = SLCONLAI - @sl, NGAYXUAT = GETDATE() WHERE LOT = @lot",
                    new SqlParameter("@sl", slTruDongNay), new SqlParameter("@lot", lotThat));
                conLaiCanTru -= slTruDongNay;
            }
            return conLaiCanTru == 0;
        }

        private void HoanTatSauKhiTruKho(List<string> lotsDaXuatThanhCong, string performedBy)
        {
            if (lotsDaXuatThanhCong == null || lotsDaXuatThanhCong.Count == 0 || _hangChoGiaoRepo == null) return;
            try
            {
                Uow.Begin();
                try
                {
                    List<HangChoGiao> closedItems = _hangChoGiaoRepo.CloseChoGiaoTheoLotAndReturn(Connection, Transaction, lotsDaXuatThanhCong, performedBy);
                    if (closedItems != null)
                    {
                        foreach (var it in closedItems.Where(x => x.SlotIdNguon.HasValue))
                        {
                            _historyRepo.SaveHistory(
                                "EXPORT_CONFIRMED_HVN", it.MaHang,
                                new LotInfo { LotNo = it.LotGoc, Quantity = it.SoLuong, TemCode = it.LotThung },
                                fromSlotId: it.SlotIdNguon, toSlotId: null, performedBy: performedBy);
                        }
                    }
                    Uow.Commit();
                }
                catch
                {
                    Uow.Rollback();
                    throw;
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
