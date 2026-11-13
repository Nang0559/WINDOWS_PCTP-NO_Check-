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
using PCTP.Shared.Enums;
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
            // B0-01: khong co movement service thi khong duoc dung duong tru kho/SlotLot.
            // Production chi co 1 duong dung (GiaoHangKhachFactory -> PhieuRepository) va luon truyen vao.
            _stockMovement = stockMovement ?? throw new ArgumentNullException(nameof(stockMovement));
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

                // The stock procedure rolls back its stock writes when it reports any
                // validation/business error. Never consume its #STOK success resultset
                // as if those rows had committed: doing so would deduct SlotLot and close
                // pending-delivery records after STOCKTP was rolled back.
                if (errors.Rows.Count > 0)
                {
                    Uow.Rollback();
                    return 0;
                }

                if (_cfg != null && _cfg.Delivery.BatBuocPickChoGiao)
                {
                    // Khách bắt buộc pick trước (HTN 100003): hàng đã được trừ SlotLot lúc pick vào
                    // FVN_HangChoGiao. CNK chỉ trừ STOCKTP (SP vừa chạy) + đóng chờ giao, KHÔNG trừ SlotLot.
                    // Thiếu/sai/không pick -> rollback cả phiếu (kể cả phần STOCKTP SP đã trừ) và báo lỗi từng LOT.
                    DataTable pickErrors = XacNhanTheoChoGiao(stok, tmpTable);
                    if (pickErrors.Rows.Count > 0)
                    {
                        Uow.Rollback();
                        errors = pickErrors;
                        return 0;
                    }
                }
                else
                {
                    coAnhHuongA0 = TruKhoAoTuKetQuaSP(stok, out List<string> lotsDaXuatThanhCong);
                    HoanTatSauKhiTruKho(lotsDaXuatThanhCong, SYSTEM_PERFORMED_BY);
                }

                if (_cfg != null && _cfg.Delivery.LoadTuBangRieng &&
                    !string.IsNullOrEmpty(_cfg.Delivery.OrderTable) && stok.Rows.Count > 0)
                {
                    foreach (DataRow row in stok.Rows)
                    {
                        // Resultset #STOK của Usp_Qrcode_Update_Stock2405 chỉ có STT/LOT/SOLUONG/STATUS,
                        // KHÔNG có cột MH -> trước đây row["MH"] ném
                        // "Column 'MH' does not belong to table Table." (HTN - 100003).
                        // Khoá dòng đơn được dựng từ chính dòng TMP theo STT.
                        int stt = 0;
                        if (row.Table.Columns.Contains("STT") && row["STT"] != DBNull.Value)
                            int.TryParse(row["STT"].ToString(), out stt);
                        if (stt <= 0) continue;

                        // SP cũ/khác có thể trả MH; nếu có thì dùng, không có thì lấy MAHANG từ TMP.
                        string maHang = row.Table.Columns.Contains("MH") && row["MH"] != DBNull.Value
                            ? row["MH"].ToString().Trim()
                            : "";

                        // Một dòng TMP duy nhất cho cả PO/NGAYGIAO(có giờ)/GIOGIAO/MAHANG: lấy cùng một lần để
                        // không ghép PO của dòng này với giờ của dòng khác.
                        DataTable tmpRow = LoadData(
                            $"SELECT TOP 1 ISNULL(PO_NO,'') AS PO_NO, NGAYGIAO, ISNULL(GIOGIAO,'') AS GIOGIAO, ISNULL(MAHANG,'') AS MAHANG FROM [{tmpTable}] WHERE STT={stt} ORDER BY STT");
                        if (string.IsNullOrEmpty(maHang) && tmpRow != null && tmpRow.Rows.Count > 0)
                            maHang = Convert.ToString(tmpRow.Rows[0]["MAHANG"])?.Trim() ?? "";
                        if (string.IsNullOrEmpty(maHang)) continue;

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
            string tmpTable = _cfg != null && !string.IsNullOrWhiteSpace(_cfg.Delivery.TmpTableSP)
                ? _cfg.Delivery.TmpTableSP
                : (_cfg != null ? _cfg.Delivery.TmpTable : "TMPPHIEUGIAOHANG");
            string docQRTable = _cfg != null && !string.IsNullOrWhiteSpace(_cfg.Delivery.DocQRTableSP)
                ? _cfg.Delivery.DocQRTableSP
                : (_cfg != null ? _cfg.Delivery.DocQRTable : "DOCQRCODE");
            return CapNhapKhoSP(gioGiaoFcc, nhaMay, tmpTable, docQRTable, out errors);
        }

        public int CapNhapKhoSP(
            string gioGiaoFcc, string nhaMay,
            string tmpTable, string docQRTable, out DataTable errors)
        {
            Db.ValidateTableName(tmpTable);
            Db.ValidateTableName(docQRTable);

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
                    new SqlParameter("@DOCQRTABLE", SqlDbType.NVarChar, 128) { Value = docQRTable },
                    new SqlParameter("@LOTRESOLUTIONMODE", SqlDbType.NVarChar, 20) { Value = "FIFO_SP" }
                );

                stok = ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
                errors = ds != null && ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();

                if (errors.Rows.Count > 0)
                {
                    Uow.Rollback();
                    return 0;
                }

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

                try
                {
                    ExecuteNonQuery(
                        @"INSERT INTO LUUDOCQRCODE
                    (LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
                     STATUS, MAFCC, STT, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY)
                    SELECT LEFT(LOTFCC, 500), LEFT(MAHANGFCC, 60), SLTEMFCC,
                           LEFT(LOTHVN, 500), LEFT(MAHANGHVN, 60), SLTEMHVN,
                           STATUS, LEFT(MAFCC, 50), STT, KETQUA,
                           @ngayGiao, @gioGiao, @nhaMay
                    FROM [" + docQRTable + @"]
                    WHERE MAHANGFCC = @maHang AND KETQUA = 'DG'
                      AND (STTBAN = @stt OR ISNULL(STTBAN, 0) = 0)",
                        new SqlParameter("@stt", SqlDbType.Int) { Value = stt },
                        new SqlParameter("@ngayGiao", SqlDbType.NVarChar, 50) { Value = ngayGiao ?? "" },
                        new SqlParameter("@gioGiao", SqlDbType.NVarChar, 50) { Value = gioGiao ?? "" },
                        new SqlParameter("@nhaMay", SqlDbType.NVarChar, 200) { Value = nhaMay ?? "" },
                        new SqlParameter("@maHang", SqlDbType.NVarChar, 100) { Value = maHang ?? "" });
                }
                catch (SqlException ex) when (ex.Number == 8152)
                {
                    throw new InvalidOperationException(
                        DiagnoseTruncation(ex, docQRTable, "LUUDOCQRCODE",
                            "MAHANGFCC = @maHang AND KETQUA = 'DG' AND (STTBAN = @stt OR ISNULL(STTBAN, 0) = 0)",
                            new[]
                            {
                                new TruncMap("LOTFCC", "LOTFCC", 500), new TruncMap("MAHANGFCC", "MAHANGFCC", 60),
                                new TruncMap("LOTHVN", "LOTHVN", 500), new TruncMap("MAHANGHVN", "MAHANGHVN", 60),
                                new TruncMap("STATUS", "STATUS", 0), new TruncMap("MAFCC", "MAFCC", 50),
                                new TruncMap("KETQUA", "KETQUA", 0)
                            },
                            new[]
                            {
                                new TruncParam("@nhaMay -> NHAMAY", "NHAMAY", (nhaMay ?? "").Length),
                                new TruncParam("@gioGiao -> GIOXUAT", "GIOXUAT", (gioGiao ?? "").Length)
                            },
                            maHang, stt), ex);
                }

                try
                {
                    ExecuteNonQuery(
                        @"INSERT INTO LUUPHIEUGIAOHANG
                    (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                     NGAYGIAO, GIOGIAO, STATUS, STATUSDOC, GearYMVN, NHAMAY, GIOGIAOFCC, PO_NO, TTPHIEU)
                    SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                           NGAYGIAO, GIOGIAO, 'OK', ISNULL(STATUSDOC, ''), ISNULL(GEAR,''), @nhaMay,
                           CONVERT(VARCHAR(8), GETDATE(), 108), ISNULL(PO_NO,''), ISNULL(TTPHIEU,'')
                    FROM [" + tmpTable + @"]
                    WHERE STT = @stt AND STATUS = 'NG'",
                        new SqlParameter("@nhaMay", SqlDbType.NVarChar, 200) { Value = nhaMay ?? "" },
                        new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                }
                catch (SqlException ex) when (ex.Number == 8152)
                {
                    throw new InvalidOperationException(
                        DiagnoseTruncation(ex, tmpTable, "LUUPHIEUGIAOHANG",
                            "STT = @stt AND STATUS = 'NG'",
                            new[]
                            {
                                new TruncMap("CUA", "CUA", 0), new TruncMap("TRUYEN", "TRUYEN", 0),
                                new TruncMap("MAHANG", "MAHANG", 0), new TruncMap("TENHANG", "TENHANG", 0),
                                new TruncMap("LOT", "LOT", 0), new TruncMap("DV", "DV", 0),
                                new TruncMap("GIOGIAO", "GIOGIAO", 0), new TruncMap("STATUSDOC", "STATUSDOC", 0),
                                new TruncMap("GEAR", "GearYMVN", 0), new TruncMap("PO_NO", "PO_NO", 0),
                                new TruncMap("TTPHIEU", "TTPHIEU", 0)
                            },
                            new[]
                            {
                                new TruncParam("@nhaMay -> NHAMAY", "NHAMAY", (nhaMay ?? "").Length),
                                new TruncParam("'OK' -> STATUS", "STATUS", 2),
                                new TruncParam("GETDATE() hh:mm:ss -> GIOGIAOFCC", "GIOGIAOFCC", 8)
                            },
                            maHang, stt), ex);
                }

                int updatedTmp = ExecuteNonQuery($"UPDATE [{tmpTable}] SET STATUS = 'OK' WHERE STT = @stt AND STATUS = 'NG'", new SqlParameter("@stt", SqlDbType.Int) { Value = stt });
                if (updatedTmp != 1)
                    throw new InvalidOperationException($"Không thể xác nhận phiếu giao STT={stt}: trạng thái đã thay đổi hoặc không còn tồn tại.");
                ExecuteNonQuery($"DELETE FROM [{docQRTable}] WHERE MAHANGFCC = @maHang AND KETQUA = 'DG' AND (STTBAN = @stt OR ISNULL(STTBAN, 0) = 0)", new SqlParameter("@maHang", SqlDbType.NVarChar, 100) { Value = maHang ?? "" }, new SqlParameter("@stt", SqlDbType.Int) { Value = stt });

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

        #region Chẩn đoán lỗi "String or binary data would be truncated" (SQL 2016 RTM không báo tên cột)
        private sealed class TruncMap
        {
            public readonly string Src, Dst; public readonly int Cap;
            public TruncMap(string src, string dst, int cap) { Src = src; Dst = dst; Cap = cap; }
        }

        private sealed class TruncParam
        {
            public readonly string Label, Dst; public readonly int Len;
            public TruncParam(string label, string dst, int len) { Label = label; Dst = dst; Len = len; }
        }

        /// <summary>
        /// Chỉ chạy khi INSERT vừa lỗi 8152. So độ dài lớn nhất của dữ liệu nguồn (đúng điều kiện WHERE
        /// của INSERT) với độ dài cột đích và trả về thông báo nêu rõ bảng/cột bị tràn.
        /// Không làm hỏng lỗi gốc: nếu chẩn đoán thất bại thì trả lại thông báo gốc.
        /// </summary>
        private string DiagnoseTruncation(SqlException original, string srcTable, string dstTable,
            string whereClause, TruncMap[] maps, TruncParam[] extras, string maHang, int stt)
        {
            string head = "Dữ liệu quá dài so với cột trong bảng " + dstTable + ". ";
            try
            {
                // 1) Độ dài cột đích (ký tự). -1 = MAX
                DataTable dst = LoadData(
                    @"SELECT c.name AS COTEN,
                             CASE WHEN c.max_length = -1 THEN 2147483647
                                  WHEN ty.name IN ('nvarchar','nchar') THEN c.max_length / 2
                                  ELSE c.max_length END AS DSTLEN
                      FROM sys.columns c
                      JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                      WHERE c.object_id = OBJECT_ID(@tbl)",
                    new SqlParameter("@tbl", SqlDbType.NVarChar, 300) { Value = "dbo." + dstTable });

                var dstLen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow r in dst.Rows)
                    dstLen[Convert.ToString(r["COTEN"])] = Convert.ToInt32(r["DSTLEN"]);

                // 2) Độ dài lớn nhất của từng cột nguồn
                var parts = new List<string>();
                foreach (TruncMap m in maps)
                    parts.Add("SELECT '" + m.Src + "' AS COTEN, MAX(LEN(CONVERT(nvarchar(max), [" + m.Src + "]))) AS SRCLEN FROM ["
                              + srcTable + "] WHERE " + whereClause);

                DataTable src = LoadData(string.Join(" UNION ALL ", parts.ToArray()),
                    new SqlParameter("@stt", SqlDbType.Int) { Value = stt },
                    new SqlParameter("@maHang", SqlDbType.NVarChar, 100) { Value = maHang ?? "" });

                var errors = new List<string>();
                foreach (TruncMap m in maps)
                {
                    DataRow[] rows = src.Select("COTEN = '" + m.Src + "'");
                    if (rows.Length == 0 || rows[0]["SRCLEN"] == DBNull.Value) continue;
                    int len = Convert.ToInt32(rows[0]["SRCLEN"]);
                    if (m.Cap > 0 && len > m.Cap) len = m.Cap;          // đã bị LEFT() cắt
                    int max;
                    if (!dstLen.TryGetValue(m.Dst, out max))
                    { errors.Add("Cột " + m.Dst + " không tồn tại trong " + dstTable); continue; }
                    if (len > max)
                        errors.Add(dstTable + "." + m.Dst + ": dữ liệu dài " + len + " ký tự > cột đích " + max
                                   + " (nguồn: " + srcTable + "." + m.Src + ")");
                }
                foreach (TruncParam e in extras)
                {
                    int max;
                    if (dstLen.TryGetValue(e.Dst, out max) && e.Len > max)
                        errors.Add(dstTable + "." + e.Dst + ": giá trị " + e.Label + " dài " + e.Len + " > cột đích " + max);
                }

                if (errors.Count > 0)
                    return head + "Cột bị tràn:\r\n- " + string.Join("\r\n- ", errors.ToArray())
                           + "\r\n(Mã hàng " + maHang + ", STT " + stt + ")";

                return head + "Không tìm thấy cột tràn trong các cột đã kiểm tra (MAHANG=" + maHang + ", STT=" + stt
                       + "); có thể là trigger hoặc cột khác. Lỗi gốc: " + original.Message;
            }
            catch (Exception diagEx)
            {
                return original.Message + " [không chẩn đoán được cột: " + diagEx.Message + "]";
            }
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

        #region Bắt buộc pick chờ giao (HTN)
        private sealed class PickNeed
        {
            public string Lot;
            public int SoLuong;
            public int Stt;
        }

        private static DataTable TaoBangLoiPick()
        {
            var t = new DataTable("Table");
            t.Columns.Add("MH", typeof(string));
            t.Columns.Add("LOT", typeof(string));
            t.Columns.Add("SLC", typeof(int));    // cần giao
            t.Columns.Add("SLTK", typeof(int));   // đã pick (đang chờ giao)
            t.Columns.Add("SLT", typeof(int));    // thiếu
            t.Columns.Add("STATUS", typeof(string));
            return t;
        }

        /// <summary>
        /// Với mỗi LOT SP đã trừ STOCKTP: phải có các dòng FVN_HangChoGiao (ChoGiao, GiaoHang) khớp LOT
        /// (LotCodeHelper) với tổng số lượng ĐÚNG BẰNG số lượng cần giao. Đủ -> đóng các dòng đó (DaGiao) +
        /// ghi EXPORT_CONFIRMED_HVN. Không đụng SlotLot. Thiếu/sai -> trả bảng lỗi (cột như bảng lỗi của SP:
        /// MH, LOT, SLC, SLTK, SLT, STATUS) và KHÔNG đóng dòng nào; caller phải rollback.
        /// </summary>
        private DataTable XacNhanTheoChoGiao(DataTable stok, string tmpTable)
        {
            DataTable err = TaoBangLoiPick();
            if (stok == null || stok.Rows.Count == 0) return err;
            if (!Uow.IsInTransaction)
                throw new InvalidOperationException("XacNhanTheoChoGiao phải chạy trong transaction giao hàng hiện tại.");
            if (!stok.Columns.Contains("LOT") || !stok.Columns.Contains("SOLUONG"))
                throw new InvalidOperationException(
                    "Stored procedure trả về dữ liệu xuất kho nhưng thiếu cột LOT/SOLUONG; huỷ giao dịch.");

            // 1) Gom nhu cầu theo LOT (LOT cũ 13 ký tự / mới 20 ký tự coi là cùng khoá).
            var needs = new List<PickNeed>();
            foreach (DataRow row in stok.Rows)
            {
                string lot = LotCodeHelper.TrimTo(row["LOT"]?.ToString(), LotCodeHelper.LEN_HEAD_FIXED);
                int sl;
                try { sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]); }
                catch (Exception ex)
                { throw new InvalidOperationException("Stored procedure trả về SOLUONG không hợp lệ; huỷ giao dịch.", ex); }
                if (string.IsNullOrWhiteSpace(lot) || sl <= 0)
                    throw new InvalidOperationException(
                        $"Stored procedure trả về LOT/SOLUONG không hợp lệ (LOT='{lot}', SOLUONG={sl}); huỷ giao dịch.");

                int stt = 0;
                if (stok.Columns.Contains("STT") && row["STT"] != DBNull.Value)
                    int.TryParse(row["STT"].ToString(), out stt);

                PickNeed n = needs.FirstOrDefault(x => LotCodeHelper.AreLotKeysEquivalent(x.Lot, lot));
                if (n == null) needs.Add(new PickNeed { Lot = lot, SoLuong = sl, Stt = stt });
                else n.SoLuong += sl;
            }

            // 2) Đối chiếu từng LOT với hàng chờ giao đã pick.
            var toClose = new List<DataRow>();
            foreach (PickNeed need in needs)
            {
                string maHang = "";
                if (need.Stt > 0)
                {
                    DataTable mh = LoadData($"SELECT TOP 1 ISNULL(MAHANG,'') AS MAHANG FROM [{tmpTable}] WHERE STT=@stt",
                        new SqlParameter("@stt", SqlDbType.Int) { Value = need.Stt });
                    if (mh.Rows.Count > 0) maHang = Convert.ToString(mh.Rows[0]["MAHANG"])?.Trim() ?? "";
                }

                DataTable rows = LoadData(
                    @"SELECT Id, MaHang, LotGoc, LotThung, SoLuong, SlotIdNguon
                      FROM FVN_HangChoGiao WITH (UPDLOCK, ROWLOCK)
                      WHERE TrangThai = @cg AND LoaiYeuCauGiao = @loai AND "
                    + LotCodeHelper.BuildLotMatchSql("LotGoc", "@lot") + @"
                      ORDER BY Id",
                    new SqlParameter("@cg", SqlDbType.NVarChar, 60) { Value = HangChoGiaoStatus.ChoGiao.ToString() },
                    new SqlParameter("@loai", SqlDbType.NVarChar, 60) { Value = HangChoGiaoLoai.GiaoHang.ToString() },
                    new SqlParameter("@lot", SqlDbType.NVarChar, 200) { Value = need.Lot });

                int tongPick = 0;
                foreach (DataRow r in rows.Rows) tongPick += Convert.ToInt32(r["SoLuong"]);

                if (tongPick < need.SoLuong)
                {
                    DataRow e = err.NewRow();
                    e["MH"] = maHang; e["LOT"] = need.Lot;
                    e["SLC"] = need.SoLuong; e["SLTK"] = tongPick; e["SLT"] = need.SoLuong - tongPick;
                    e["STATUS"] = tongPick == 0
                        ? "LOT chưa được pick vào hàng chờ giao. Hãy pick đúng LOT rồi cập nhật kho lại."
                        : "Pick chưa đủ hàng chờ giao. Hãy pick bổ sung rồi cập nhật kho lại.";
                    err.Rows.Add(e);
                    continue;
                }

                // Lấy nguyên từng dòng chờ giao (không tách dòng) cho tới khi đúng bằng số lượng cần giao.
                int daLay = 0;
                var chon = new List<DataRow>();
                foreach (DataRow r in rows.Rows)
                {
                    if (daLay >= need.SoLuong) break;
                    daLay += Convert.ToInt32(r["SoLuong"]);
                    chon.Add(r);
                }

                if (daLay != need.SoLuong)
                {
                    DataRow e = err.NewRow();
                    e["MH"] = maHang; e["LOT"] = need.Lot;
                    e["SLC"] = need.SoLuong; e["SLTK"] = tongPick; e["SLT"] = 0;
                    e["STATUS"] = "Số lượng pick không khớp số lượng giao (các dòng chờ giao: "
                        + string.Join(" + ", rows.AsEnumerable().Select(x => Convert.ToString(x["SoLuong"])).ToArray())
                        + " = " + tongPick + ", cần giao " + need.SoLuong + "). Hãy huỷ chờ giao/pick lại cho khớp.";
                    err.Rows.Add(e);
                    continue;
                }

                toClose.AddRange(chon);
            }

            if (err.Rows.Count > 0) return err;

            // 3) Đủ và khớp tất cả LOT -> đóng chờ giao + lịch sử. KHÔNG trừ SlotLot (đã trừ lúc pick).
            foreach (DataRow r in toClose)
            {
                int id = Convert.ToInt32(r["Id"]);
                int affected = ExecuteNonQuery(
                    @"UPDATE FVN_HangChoGiao
                      SET TrangThai = @dg, NgayGiao = GETDATE(), NguoiGiao = @ng
                      WHERE Id = @id AND TrangThai = @cg",
                    new SqlParameter("@dg", SqlDbType.NVarChar, 60) { Value = HangChoGiaoStatus.DaGiao.ToString() },
                    new SqlParameter("@ng", SqlDbType.NVarChar, 100) { Value = SYSTEM_PERFORMED_BY },
                    new SqlParameter("@id", SqlDbType.Int) { Value = id },
                    new SqlParameter("@cg", SqlDbType.NVarChar, 60) { Value = HangChoGiaoStatus.ChoGiao.ToString() });
                if (affected != 1)
                    throw new InvalidOperationException(
                        $"Hàng chờ giao Id={id} đã thay đổi trạng thái trong lúc cập nhật kho; huỷ giao dịch.");

                if (r["SlotIdNguon"] != DBNull.Value)
                {
                    _historyRepo.SaveHistory(
                        "EXPORT_CONFIRMED_HVN", Convert.ToString(r["MaHang"]),
                        new LotInfo
                        {
                            LotNo = Convert.ToString(r["LotGoc"]),
                            Quantity = Convert.ToInt32(r["SoLuong"]),
                            TemCode = r["LotThung"] == DBNull.Value ? null : Convert.ToString(r["LotThung"])
                        },
                        fromSlotId: Convert.ToInt32(r["SlotIdNguon"]), toSlotId: null, performedBy: SYSTEM_PERFORMED_BY);
                }
            }

            return err;
        }
        #endregion

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
            DataTable rows = LoadData($"SELECT LOT, SLCONLAI FROM STOCKTP WITH (UPDLOCK, HOLDLOCK) WHERE {matchCondition} AND SLCONLAI > 0 ORDER BY NGAYNHAP ASC, LOT ASC");
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
