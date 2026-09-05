using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.FuctionMain;
using PCTP.Infrastructure.Repositories;
using PCTP.Models;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Services;          // BulkStockAdjustService (nếu khác namespace, chỉnh lại)
using PCTP.Modules.KhoCore.Repositories;             // IBulkStockSlotRepository, IStockHistoryRepository
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;               // IHangChoGiaoRepository
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;                            // SqlRepositoryBase, IUnitOfWork
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
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public class PhieuRepository : SqlRepositoryBase, IPhieuRepository
    {
        private readonly CustomerConfig _cfg;
        private readonly IHangChoGiaoRepository _hangChoGiaoRepo;
        private readonly IIFSRepository _ifsRepo;
        private readonly IBulkStockSlotRepository _bulkStockSlotRepo;
        private readonly IStockHistoryRepository _historyRepo;

        public PhieuRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            CustomerConfig cfg,
            IBulkStockSlotRepository bulkStockSlotRepo,
            IStockHistoryRepository historyRepo,
            IHangChoGiaoRepository hangChoGiaoRepo = null,
            IIFSRepository ifsRepo = null)
            : base(db, uow)
        {
            _cfg = cfg;
            _bulkStockSlotRepo = bulkStockSlotRepo ?? throw new ArgumentNullException(nameof(bulkStockSlotRepo));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _hangChoGiaoRepo = hangChoGiaoRepo;   // giữ nullable như hành vi cũ (có null-check khi dùng)
            _ifsRepo = ifsRepo ?? IFSRepository.Create();
        }

        private BulkStockAdjustService CreateBulkService()
            => new BulkStockAdjustService(_bulkStockSlotRepo, _historyRepo, Uow);

        #region ══ IPhieuValidationRepository ══════════════════════════════════

        public int CountDocQRCode(string docQRTable)
        {
            ValidateTenBan(docQRTable);
            object kq = ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]");
            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public bool CheckCoMaNG(string tenBan)
        {
            string kq = ExecuteScalar("SELECT dbo.ufn_QRcode_ADD_CMD_MANG()")?.ToString() ?? "0";
            return int.TryParse(kq, out int v) && (v == 1 || v == 2);
        }

        public bool KiemTraMaTrongPhieu(string maHang, string tenBan)
        {
            ValidateTenBan(tenBan);
            object kq = ExecuteScalar(
                $"SELECT COUNT(*) FROM [{tenBan}] WHERE MAHANG = @ma",
                new SqlParameter("@ma", maHang));
            return int.TryParse(kq?.ToString(), out int v) && v > 0;
        }

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, PhieuTableSet tables)
            => GetDanhSachTrungMaSl(maHang, sl, tables.TmpTable, tables.DocQRTable);

        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);

            string sql =
                $"SELECT STT, MAHANG, TENHANG, GIOGIAO, SOLUONG, " +
                "CASE " +
                "  WHEN STATUS IS NULL OR STATUS = '' THEN N'Chưa Bắn QRCODE' " +
                "  WHEN STATUS = '0' THEN N'Đang Bắn QRCODE' " +
                "  WHEN STATUS = '1' THEN N'Đã Bắn QRCODE' " +
                "  ELSE STATUS " +
                $"END AS STATUS FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma AND SOLUONG = @sl " +
                $"AND (LOT = '' OR LOT IS NULL) " +
                $"AND MAHANG IN (" +
                $"  SELECT MAHANGFCC FROM [{docQRTable}] " +
                $"  WHERE ISNULL(KETQUA,'') <> 'DG' GROUP BY MAHANGFCC)";

            return LoadData(sql, new SqlParameter("@ma", maHang), new SqlParameter("@sl", sl));
        }

        public int CountTrungMaSl(string maHang, int sl, PhieuTableSet tables)
            => CountTrungMaSl(maHang, sl, tables.TmpTable, tables.DocQRTable);

        public int CountTrungMaSl(string maHang, int sl, string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);

            object kq = ExecuteScalar(
                $"SELECT COUNT(*) FROM [{tenBan}] " +
                $"WHERE MAHANG = @ma AND SOLUONG = @sl " +
                $"AND (LOT = '' OR LOT IS NULL) " +
                $"AND MAHANG IN (" +
                $"  SELECT MAHANGFCC FROM [{docQRTable}] " +
                $"  WHERE KETQUA <> 'DG' GROUP BY MAHANGFCC)",
                new SqlParameter("@ma", maHang), new SqlParameter("@sl", sl));

            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public DataTable GetDonHangChuaLot(PhieuTableSet tables)
            => GetDonHangChuaLot(tables.TmpTable, tables.DocQRTable);

        public DataTable GetDonHangChuaLot(string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);

            return LoadData(
                $"SELECT STT, MAHANG, LOT, SOLUONG FROM [{tenBan}] " +
                $"WHERE (LOT = '' OR LOT IS NULL) " +
                $"AND MAHANG IN ( " +
                $"  SELECT MAHANGFCC FROM [{docQRTable}] " +
                $"  WHERE ISNULL(KETQUA,'') <> 'DG' " +
                $"  GROUP BY MAHANGFCC" +
                $") ORDER BY STT");
        }

        #endregion

        #region ══ IPhieuTmpRepository ══════════════════════════════════════════

        public DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm,
            PhieuTableSet tables)
        {
            ValidateTenBan(tables.TmpTable);
            ValidateTenBan(tables.SourceTable);
            ValidateTenBan(tables.DocQRTable);

            DataTable tt = LoadData(
                $"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAOFCC, NHAMAY FROM [{tables.SourceTable}]");

            if (tt.Rows.Count > 0)
            {
                ngayGiao = tt.Rows[0]["NGAYGIAO"].ToString();
                nhaMay = tt.Rows[0]["NHAMAY"].ToString();
                gioFcc = tt.Rows[0]["GIOGIAOFCC"].ToString();
                addNm = SafeInt(tt.Rows[0]["ADDNM"]);
            }

            return CallSP("Usp_Qrcode_LOAD_PHIEU_DOCQR2405", ngayGiao, nhaMay, gioFcc, addNm, tables);
        }

        public DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tmpTable, string ifsTable, string docQRTable)
            => LoadPhieuDocQR(ngayGiao, nhaMay, gioFcc, addNm,
                new PhieuTableSet(tmpTable, ifsTable, docQRTable));

        public DataTable LuuVaLoad(PhieuTableSet tables, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm)
        {
            ValidateTenBan(tables.TmpTable);
            ValidateTenBan(tables.SourceTable);
            ValidateTenBan(tables.DocQRTable);

            SWLog.Measure("4a. ConvertDateTimeColumns", () =>
            {
                var dateTimeCols = donHang.Columns.Cast<DataColumn>()
                    .Where(c => c.DataType == typeof(DateTime))
                    .Select(c => c.ColumnName).ToList();

                foreach (string colName in dateTimeCols)
                {
                    string tempName = colName + "_STR";
                    donHang.Columns.Add(tempName, typeof(string));
                    foreach (DataRow row in donHang.Rows)
                        row[tempName] = row[colName] == DBNull.Value
                            ? "" : ((DateTime)row[colName]).ToString("yyyy-MM-dd");
                    donHang.Columns.Remove(colName);
                    donHang.Columns[tempName].ColumnName = colName;
                }
            });

            SWLog.Measure($"4b. DropCreate [{tables.SourceTable}]",
                () => DropCreate(tables.SourceTable, donHang));

            // ⚠️ BulkInsertDataTable cần connection string thô — lấy qua Db.Sql (SQLPROVIDER
            // gốc bên trong PhieuSqlExecutor), KHÔNG tự giữ field SQLPROVIDER riêng nữa.
            SWLog.Measure($"4c. BulkInsert {donHang.Rows.Count} rows → [{tables.SourceTable}]",
                () => SqlTableCreator.BulkInsertDataTable(Db.Sql.B7R2_FCCdb, tables.SourceTable, donHang));

            // ── Guard: chỉ xoá TMP khi không đang bắn dở ──────────────────
            SWLog.Measure($"4d. Guard DELETE [{tables.TmpTable}]", () =>
            {
                object tmpExistsRaw = ExecuteScalar(
                    "SELECT COUNT(*) FROM sys.objects " +
                    $"WHERE object_id = OBJECT_ID(N'[dbo].[{tables.TmpTable}]') AND type = 'U'");
                int tmpExists = tmpExistsRaw == null || tmpExistsRaw == DBNull.Value ? 0 : Convert.ToInt32(tmpExistsRaw);
                if (tmpExists != 1) return;

                object docQRExistsRaw = ExecuteScalar(
                    "SELECT COUNT(*) FROM sys.objects " +
                    $"WHERE object_id = OBJECT_ID(N'[dbo].[{tables.DocQRTable}]') AND type = 'U'");
                int docQRExists = docQRExistsRaw == null || docQRExistsRaw == DBNull.Value ? 0 : Convert.ToInt32(docQRExistsRaw);

                if (docQRExists == 1)
                {
                    object demDocQRRaw = ExecuteScalar($"SELECT COUNT(*) FROM [{tables.DocQRTable}]");
                    int demDocQR = demDocQRRaw == null || demDocQRRaw == DBNull.Value ? 0 : Convert.ToInt32(demDocQRRaw);
                    if (demDocQR > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LuuVaLoad] SKIP DELETE [{tables.TmpTable}] — đang có {demDocQR} dòng trong [{tables.DocQRTable}]");
                        return;
                    }
                }

                ExecuteNonQuery($"DELETE FROM [{tables.TmpTable}]");
                System.Diagnostics.Debug.WriteLine($"[LuuVaLoad] Đã DELETE [{tables.TmpTable}]");
            });

            return SWLog.Measure($"4e. CallSP [{tenSP}]",
                () => CallSP(tenSP, ngayGiao, nhaMay, gioFcc, addNm, tables));
        }

        public DataTable LuuVaLoad(string tenSPBang, string tenSP, DataTable donHang,
            string ngayGiao, string nhaMay, string gioFcc, int addNm,
            string tenBan, string docQRTable, string ifsView = "")
        {
            var tables = new PhieuTableSet(tenBan, tenSPBang, docQRTable, tenBan, ifsView);
            return LuuVaLoad(tables, tenSP, donHang, ngayGiao, nhaMay, gioFcc, addNm);
        }

        public DataTable LoadTuTmpTable(string tmpTable)
        {
            ValidateTenBan(tmpTable);
            string sql = $@"
                SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV,
                       SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU,
                       NHAMAY, ADDNM, HOP, STATUSDOC, Note,
                       ISNULL(PO_NO,'') AS PO_NO,
                       ISNULL(PO_ITEM,'') AS PO_ITEM
                FROM [{tmpTable}]
                ORDER BY TRY_CAST(STT AS INT), STT";
            return LoadData(sql);
        }

        public DataTable GetDonHangHienTai(string tenBan)
        {
            ValidateTenBan(tenBan);
            return LoadData(
                "SELECT STT, MAHANG, LOT, STATUS, STATUSDOC " +
                $"FROM [{tenBan}] ORDER BY STT");
        }

        public void XoaTmpPhieu(string tenBan)
        {
            ValidateTenBan(tenBan);
            ExecuteNonQuery($"DELETE FROM [{tenBan}]");
        }

        public void XoaDocQRCode(string docQRTable)
        {
            ValidateTenBan(docQRTable);
            ExecuteNonQuery($"DELETE FROM [{docQRTable}]");
        }

        public TrangThaiBan GetTrangThaiDangBan(PhieuTableSet tables)
            => GetTrangThaiDangBan(tables.TmpTable, tables.DocQRTable);

        public TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable)
        {
            ValidateTenBan(tmpTable);
            ValidateTenBan(docQRTable);

            var result = new TrangThaiBan();

            object demQRRaw = ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]");
            if (!int.TryParse(demQRRaw?.ToString(), out int demQR) || demQR == 0)
            {
                result.DangBan = false;
                return result;
            }

            object demPhieuRaw = ExecuteScalar($"SELECT COUNT(*) FROM [{tmpTable}]");
            if (!int.TryParse(demPhieuRaw?.ToString(), out int demPhieu) || demPhieu == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataTable dt = LoadData($"SELECT TOP 1 ADDNM, NGAYGIAO, GIOGIAO, NHAMAY FROM [{tmpTable}]");
            if (dt.Rows.Count == 0)
            {
                result.DangBan = true;
                result.DataKhongKhop = true;
                return result;
            }

            DataRow r = dt.Rows[0];
            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = r["ADDNM"] == DBNull.Value ? 1 : Convert.ToInt32(r["ADDNM"]);
            result.NhaMay = r["NHAMAY"] == DBNull.Value ? "" : r["NHAMAY"].ToString().Trim();
            result.NgayGiao = r["NGAYGIAO"] == DBNull.Value ? "" : Convert.ToDateTime(r["NGAYGIAO"]).ToString("yyyy-MM-dd");

            string gioDon = r["GIOGIAO"] == DBNull.Value ? "" : r["GIOGIAO"].ToString().Trim();
            if (gioDon.Length == 1) gioDon = "0" + gioDon;
            result.GioGiaoFCC = gioDon;

            return result;
        }

        public TrangThaiBan GetTrangThaiDangBanYMVN(PhieuTableSet tables)
            => GetTrangThaiDangBanYMVN(tables.TmpTable, tables.DocQRTable);

        public TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable)
        {
            ValidateTenBan(tmpTable);
            ValidateTenBan(docQRTable);

            var result = new TrangThaiBan();

            string demTmpRaw = Convert.ToString(
                ExecuteScalar($"SELECT COUNT(*) FROM [{tmpTable}] WHERE addnm = 0"));
            if (!int.TryParse(demTmpRaw, out int demTmp) || demTmp == 0)
            {
                result.DangBan = false;
                return result;
            }

            int demQR = Convert.ToInt32(ExecuteScalar($"SELECT COUNT(*) FROM [{docQRTable}]") ?? 0);
            if (demQR == 0)
            {
                result.DangBan = false;
                return result;
            }

            DataTable dt = LoadData($"SELECT TOP 1 NGAYGIAO, GIOGIAO FROM [{tmpTable}]");
            if (dt.Rows.Count == 0) { result.DangBan = false; return result; }

            result.DangBan = true;
            result.DataKhongKhop = false;
            result.AddNM = 1;
            result.NhaMay = "YAMAHA - VIET NAM";

            string ngayRaw = dt.Rows[0]["NGAYGIAO"].ToString();
            result.NgayGiao = ngayRaw.Length >= 10 ? ngayRaw.Substring(0, 10) : ngayRaw;
            result.GioGiaoFCC = dt.Rows[0]["GIOGIAO"].ToString().Trim();

            return result;
        }

        public void EnsureTablesExist()
        {
            string[] tables = { "IFSPHIEUGIAOHANG", "IFSPHIEUGIAOHANGView" };
            string createSql =
                "IF NOT EXISTS (" +
                "  SELECT * FROM sys.objects " +
                "  WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type = 'U'" +
                ") CREATE TABLE [{0}] (" +
                "  STT INT, MAHANG NVARCHAR(50), TENHANG NVARCHAR(100), SOLUONG INT, " +
                "  NGAYGIAO SMALLDATETIME, GIOGIAO NVARCHAR(50), GIOGIAOFCC NVARCHAR(200), " +
                "  NHAMAY NVARCHAR(100), ADDNM INT, LOT NVARCHAR(500), " +
                "  STATUS NVARCHAR(50), STATUSDOC NVARCHAR(50))";

            foreach (var table in tables)
                ExecuteNonQuery(string.Format(createSql, table));
        }

        #endregion

        #region ══ IPhieuLotRepository ══════════════════════════════════════════

        public string GetLotNo(string maHang, int stt, int dem, int slGiao, PhieuTableSet tables)
            => GetLotNo(maHang, stt, dem, slGiao, tables.DocQRTable, tables.TmpTable);

        public string GetLotNo(string maHang, int stt, int dem, int slGiao,
            string docQRTable = "DOCQRCODE", string tmpTable = "TMPPHIEUGIAOHANG")
        {
            DataTable dt = ExecuteStoredProcedure("Usp_Qrcode_Take_Lot2405",
                new SqlParameter("@_MaFCC", maHang),
                new SqlParameter("@_STTP", stt),
                new SqlParameter("@_DeM", dem),
                new SqlParameter("@_SLGIAO", slGiao),
                new SqlParameter("@DOCQRTABLE", docQRTable),
                new SqlParameter("@TMPTABLE", tmpTable));

            var parts = new List<string>();
            foreach (DataRow row in dt.Rows)
                parts.Add($"{row["LOTFCC"].ToString().Trim()}-{row["FCC"].ToString().Trim()}");

            return string.Join(",", parts);
        }

        public void CapNhapLotTmpPhieu(int stt, string lot, string tenBan)
        {
            if (stt <= 0 || string.IsNullOrWhiteSpace(lot)) return;
            ValidateTenBan(tenBan);

            ExecuteNonQuery(
                $"UPDATE [{tenBan}] SET LOT = @lot WHERE STT = @stt",
                new SqlParameter("@lot", lot), new SqlParameter("@stt", stt));
        }

        public void LayLaiLotNo(int stt, PhieuTableSet tables)
            => LayLaiLotNo(stt, tables.TmpTable, tables.DocQRTable);

        public void LayLaiLotNo(int stt, string tenBan, string docQRTable)
        {
            ValidateTenBan(tenBan);
            ValidateTenBan(docQRTable);

            ExecuteNonQuery(
                $"UPDATE [{tenBan}] SET LOT = '', STATUSDOC = 'NG', TTPHIEU = NULL " +
                "WHERE STT = @stt AND ISNULL(STATUS,'') <> 'OK'",
                new SqlParameter("@stt", stt));

            ExecuteNonQuery(
                $"UPDATE [{docQRTable}] SET GIO = NULL, KETQUA = 'OK', STTBAN = NULL " +
                "WHERE ISNULL(STTBAN, 0) = @stt AND KETQUA = 'DG'",
                new SqlParameter("@stt", stt));
        }

        public DataTable LoadGhepLot()
            => ExecuteStoredProcedure("Usp_Qrcode_gheplot");

        public DataTable GetDanhSachLotTuKho(string maHang)
        {
            string sql = @"
                SELECT LOT, SLCONLAI, SLXUAT, PART, NAME
                FROM STOCKTP
                WHERE PART = @ma AND SLCONLAI > 0
                ORDER BY LOT";
            return LoadData(sql, new SqlParameter("@ma", maHang));
        }

        #endregion

        #region ══ IPhieuKhoRepository ══════════════════════════════════════════

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, PhieuTableSet tables, out DataTable errors)
            => CapNhapKho(gioGiaoFcc, nhaMay, tables.TmpTable, tables.DocQRTable, out errors);

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
        {
            // ⚠️ SP này cần DataSet ĐA BẢNG (stok + errors) — gọi thẳng Db (không qua Uow
            // transaction, giữ đúng hành vi gốc: SP tự quản lý transaction bên trong).
            var ds = Db.ExecuteStoredProcedureDataSet(
                "Usp_Qrcode_Update_Stock2405",
                new SqlParameter("@GIOGIAOFCC", gioGiaoFcc ?? ""),
                new SqlParameter("@NHAMAY", nhaMay ?? ""),
                new SqlParameter("@TMPTABLE", tmpTable ?? "TMPPHIEUGIAOHANG"),
                new SqlParameter("@DOCQRTABLE", docQRTable ?? "DOCQRCODE"),
                new SqlParameter("@LOT_KEY_LEN", PCTP.Common.LotCodeHelper.LEN_HEAD_FIXED));

            errors = ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
            DataTable stok = ds.Tables[0];

            // ── (1) Trừ SlotLot của kho ảo A0 theo từng LOT vừa xuất OK ────
            bool coAnhHuongA0 = false;
            var lotsDaXuatThanhCong = new List<string>();

            if (stok.Rows.Count > 0)
            {
                var bulkService = CreateBulkService();
                foreach (DataRow row in stok.Rows)
                {
                    string lotRaw = row["LOT"]?.ToString();
                    string lot = LotCodeHelper.TrimTo(lotRaw, LotCodeHelper.LEN_HEAD_FIXED);
                    int sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                    if (string.IsNullOrWhiteSpace(lot) || sl <= 0) continue;

                    bool anhHuong = bulkService.TruKhoAoTheoLot(lot, sl);
                    if (anhHuong) coAnhHuongA0 = true;
                    lotsDaXuatThanhCong.Add(lot);
                }
            }

            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();

            // ── (2) Đóng TMPCHOGIAO tương ứng — best-effort ────────────────
            if (lotsDaXuatThanhCong.Count > 0 && _hangChoGiaoRepo != null)
            {
                try
                {
                    List<HangChoGiao> closedItems;

                    Uow.Begin();
                    try
                    {
                        closedItems = _hangChoGiaoRepo.CloseChoGiaoTheoLotAndReturn(
                            Connection, Transaction, lotsDaXuatThanhCong, "SYSTEM_HVN_CNK");
                        Uow.Commit();
                    }
                    catch
                    {
                        Uow.Rollback();
                        throw;
                    }

                    foreach (var it in closedItems.Where(x => x.SlotIdNguon.HasValue))
                        _historyRepo.SaveHistory(
                            "EXPORT_CONFIRMED_HVN", it.MaHang,
                            new LotInfo { LotNo = it.LotGoc, Quantity = it.SoLuong, TemCode = it.LotThung },
                            fromSlotId: it.SlotIdNguon,
                            toSlotId: null,
                            performedBy: "SYSTEM_HVN_CNK");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CapNhapKho] Không đóng được TMPCHOGIAO: {ex.Message}");
                }
            }

            // ── (3) Đánh dấu IsDelivered cho YMVN/HTN (có OrderTable) ──────
            if (_cfg != null && _cfg.LoadTuBangRieng && !string.IsNullOrEmpty(_cfg.OrderTable) && stok.Rows.Count > 0)
            {
                foreach (DataRow row in stok.Rows)
                {
                    string maHang = row["MH"]?.ToString() ?? "";
                    int stt = row.Table.Columns.Contains("STT") ? Convert.ToInt32(row["STT"]) : 0;
                    if (string.IsNullOrEmpty(maHang)) continue;

                    string whereClause = stt > 0 ? $"STT={stt}" : $"MAHANG='{SqlHelper.Esc(maHang)}' AND STATUS='OK'";

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

        public int CapNhapKhoHTN(string nhaMay, PhieuTableSet tables, out DataTable errors)
            => CapNhapKho("", nhaMay, tables.TmpTable, tables.DocQRTable, out errors);

        public int CapNhapKhoHTN(string nhaMay, string tmpTable, string docQRTable, out DataTable errors)
            => CapNhapKho("", nhaMay, tmpTable, docQRTable, out errors);

        public int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors)
        {
            var ds = Db.ExecuteStoredProcedureDataSet("Usp_Qrcode_Update_Stock_SP",
                new SqlParameter("@GIOGIAOFCC", gioGiaoFcc),
                new SqlParameter("@NHAMAY", nhaMay));

            errors = ds.Tables.Count > 1 ? ds.Tables[1] : new DataTable();
            DataTable stok = ds.Tables[0];

            if (stok.Rows.Count > 0 && stok.Columns.Contains("LOT") && stok.Columns.Contains("SOLUONG"))
            {
                var bulkService = CreateBulkService();
                bool coAnhHuongA0 = false;

                foreach (DataRow row in stok.Rows)
                {
                    string lotRaw = row["LOT"]?.ToString();
                    string lot = LotCodeHelper.TrimTo(lotRaw, LotCodeHelper.LEN_HEAD_FIXED);
                    int sl = row["SOLUONG"] == DBNull.Value ? 0 : Convert.ToInt32(row["SOLUONG"]);
                    if (string.IsNullOrWhiteSpace(lot) || sl <= 0) continue;

                    if (bulkService.TruKhoAoTheoLot(lot, sl))
                        coAnhHuongA0 = true;
                }

                if (coAnhHuongA0)
                    StockChangedNotifier.RaiseStockChanged();
            }

            return stok.Rows.Count;
        }

        public bool CapNhapKhoYMVN(int stt, string lotSl, string maHang, string ngayGiao,
            string gioGiao, string nhaMay, out DS_ERR_CNK error)
        {
            error = null;
            var bulkService = CreateBulkService();
            bool coAnhHuongA0 = false;

            string tmpTable = _cfg.TmpTable;
            string docQRTable = _cfg.DocQRTable;

            string[] lotParts = lotSl.Split(',');
            foreach (string part in lotParts)
            {
                string[] tach = part.Trim().Split('-');
                if (tach.Length < 2) continue;

                string lot = LotCodeHelper.TrimTo(tach[0], LotCodeHelper.LEN_HEAD_FIXED);
                string matchCondition = LotCodeHelper.BuildLotMatchSql("LOT", $"'{SqlHelper.Esc(lot)}'");

                if (!int.TryParse(tach[1], out int sl)) continue;

                object slConlaiRaw = ExecuteScalar($"SELECT ISNULL(slconlai,0) FROM STOCKTP WHERE {matchCondition}");
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

                string gg = ngayGiao + " " + gioGiao + ":00";
                ExecuteNonQuery(
                    $"UPDATE t SET t.ngayxuat = '{gg}', t.slxuat = slxuat + {sl}, t.slconlai = slconlai - {sl} " +
                    $"FROM (SELECT TOP 1 * FROM STOCKTP WHERE {matchCondition}) t");

                if (bulkService.TruKhoAoTheoLot(lot, sl))
                    coAnhHuongA0 = true;
            }

            ExecuteNonQuery($@"
                INSERT INTO LUUDOCQRCODE
                (LOTFCC, MAHANGFCC, SLTEMFCC, LOTHVN, MAHANGHVN, SLTEMHVN,
                 STATUS, MAFCC, STT, KETQUA, NGAYXUAT, GIOXUAT, NHAMAY)
                SELECT
                    LEFT(LOTFCC, 500), LEFT(MAHANGFCC, 60), SLTEMFCC,
                    LEFT(LOTHVN, 500), LEFT(MAHANGHVN, 60), SLTEMHVN,
                    STATUS, LEFT(MAFCC, 50), STT, KETQUA,
                    '{ngayGiao}', '{SqlHelper.Esc(gioGiao)}', '{SqlHelper.Esc(nhaMay)}'
                FROM [{docQRTable}]
                WHERE MAHANGFCC = '{SqlHelper.Esc(maHang)}' AND KETQUA = 'DG'");

            ExecuteNonQuery($@"
                INSERT INTO LUUPHIEUGIAOHANG
                (STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                 NGAYGIAO, GIOGIAO, STATUS, GearYMVN, NHAMAY, GIOGIAOFCC, PO_NO, TTPHIEU)
                SELECT
                    STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG,
                    NGAYGIAO, GIOGIAO, 'OK', ISNULL(GEAR,''),
                    '{SqlHelper.Esc(nhaMay)}', CONVERT(VARCHAR(8), GETDATE(), 108),
                    ISNULL(PO_NO,''), ISNULL(TTPHIEU,'')
                FROM [{tmpTable}]
                WHERE STT = {stt} AND STATUS = 'NG'");

            ExecuteNonQuery($"UPDATE [{tmpTable}] SET STATUS = 'OK' WHERE STT = {stt}");

            ExecuteNonQuery(
                $"DELETE FROM [{docQRTable}] WHERE MAHANGFCC = '{SqlHelper.Esc(maHang)}' AND KETQUA = 'DG'");

            if (coAnhHuongA0)
                StockChangedNotifier.RaiseStockChanged();

            string poNo = Convert.ToString(ExecuteScalar(
                $"SELECT ISNULL(PO_NO,'') FROM [{tmpTable}] WHERE STT={stt}"))?.Trim() ?? "";

            if (!string.IsNullOrEmpty(poNo) && !string.IsNullOrEmpty(_cfg.OrderTable))
                DanhDauDaGiao(poNo, maHang, ngayGiao, _cfg);

            return true;
        }

        public void DanhDauDaGiao(string poNo, string maHang, string ngayGiao, CustomerConfig cfg)
        {
            if (string.IsNullOrEmpty(cfg.OrderTable)) return;

            ExecuteNonQuery(
                $"UPDATE [{cfg.OrderTable}] SET IsDelivered = 1, DeliveredDate = GETDATE() " +
                "WHERE Oder_no = @po AND Part_no = @pno AND CAST(NgayGiao AS DATE) = @ng AND IsDelivered = 0",
                new SqlParameter("@po", poNo), new SqlParameter("@pno", maHang), new SqlParameter("@ng", ngayGiao));
        }

        #endregion

        #region ══ IPhieuLuuTruRepository ═══════════════════════════════════════

        public DataTable LoadLuuPhieu(string nhaMay, string ngayGiao, string gioGiaoFcc)
        {
            const string query = @"
                SELECT STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, SOLUONG, NGAYGIAO, GIOGIAO,
                       STATUS, TTPHIEU, NHAMAY, HOP, STATUSDOC, Note,
                       ISNULL(PO_NO, '') AS PO_NO, ISNULL(PO_ITEM, '') AS PO_ITEM
                FROM LUUPHIEUGIAOHANG
                WHERE NHAMAY = @nm AND NGAYGIAO = @ng AND GIOGIAOFCC = @gg";

            return LoadData(query,
                new SqlParameter("@nm", SqlDbType.NVarChar, 200) { Value = (object)nhaMay?.Trim() ?? DBNull.Value },
                new SqlParameter("@ng", SqlDbType.NVarChar, 50) { Value = (object)ngayGiao?.Trim() ?? DBNull.Value },
                new SqlParameter("@gg", SqlDbType.NVarChar, 50) { Value = (object)gioGiaoFcc?.Trim() ?? DBNull.Value });
        }

        public int LuuPhieuSP(string nhaMay, string ngayGiao, string gioGiaoFcc, string loaiPhieu)
        {
            ExecuteNonQuery(
                "UPDATE LUUPHIEUGIAOHANG SET LOT = ISNULL(PO_NO,'') + '-' + ISNULL(PO_ITEM,'') " +
                "WHERE NHAMAY=@nm AND NGAYGIAO=@ng AND GIOGIAOFCC=@gg " +
                "AND (LOT IS NULL OR LOT='') AND ISNULL(PO_NO,'')<>''",
                new SqlParameter("@nm", nhaMay), new SqlParameter("@ng", ngayGiao), new SqlParameter("@gg", gioGiaoFcc));

            object kq = ExecuteScalar(
                "SELECT COUNT(*) FROM LUUPHIEUGIAOHANG " +
                "WHERE NHAMAY=@nm AND NGAYGIAO=@ng AND GIOGIAOFCC=@gg AND LOT IS NOT NULL AND LOT<>''",
                new SqlParameter("@nm", nhaMay), new SqlParameter("@ng", ngayGiao), new SqlParameter("@gg", gioGiaoFcc));

            return int.TryParse(kq?.ToString(), out int v) ? v : 0;
        }

        public void CapNhapTTPHIEU(string nhaMay, string ngayGiao, string gioGiaoFcc, int stt, string ghiChu)
        {
            string safe = ghiChu?.Trim() == "STOP" ? "STOP" : "";

            ExecuteNonQuery(
                "UPDATE LUUPHIEUGIAOHANG SET TTPHIEU=@gc " +
                "WHERE NHAMAY=@nm AND NGAYGIAO=@ng AND GIOGIAOFCC=@gg AND STT=@stt",
                new SqlParameter("@gc", safe), new SqlParameter("@nm", nhaMay),
                new SqlParameter("@ng", ngayGiao), new SqlParameter("@gg", gioGiaoFcc),
                new SqlParameter("@stt", stt));
        }

        #endregion

        #region ══ IPhieuGiaoDBRepository ═══════════════════════════════════════

        public DataTable GetDanhSachMaHang() =>
            LoadData("SELECT ID, Code, Name FROM B20Item WHERE LEN(Code) > 10 GROUP BY ID, Code, Name ORDER BY ID");

        public DataTable LoadTmpPhieuGiaoDB(string tenBan)
        {
            ValidateTenBan(tenBan);
            return LoadData(
                $"SELECT '' AS IDP, STT, CUA, TRUYEN, MAHANG, TENHANG, LOT, DV, " +
                $"SOLUONG, NGAYGIAO, GIOGIAO, STATUS, TTPHIEU, NHAMAY, ADDNM, " +
                $"HOP, STATUSDOC, Note, ISNULL(PO_NO,'') AS PO_NO, ISNULL(PO_ITEM,'') AS PO_ITEM " +
                $"FROM [{tenBan}]");
        }

        public void LuuGiaoDB(DataTable donHang, string gioFccMoTa, int addNm,
            string tmpTable, string ifsTable, string nhaMayOverride = "")
        {
            ValidateTenBan(tmpTable);
            ValidateTenBan(ifsTable);

            DropCreate(ifsTable, donHang);
            ExecuteNonQuery($"DELETE FROM [{ifsTable}]");
            SqlTableCreator.BulkInsertDataTable(Db.Sql.B7R2_FCCdb, ifsTable, donHang);

            string nhaMay = !string.IsNullOrEmpty(nhaMayOverride)
                ? nhaMayOverride
                : (addNm == 1 ? "HON DA - VIET NAM(NHA MAY VP)" : "HON DA - VIET NAM(NHA MAY HA NAM)");

            CallSP("Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                DateTime.Now.ToString("yyyy-MM-dd"), nhaMay, gioFccMoTa, addNm,
                new PhieuTableSet(tmpTable, ifsTable, "DOCQRCODE"));

            ExecuteNonQuery(
                $"UPDATE D SET D.GGFCC=T.GIOGIAO, D.LOT=T.LOT, D.NGAYGIAO=T.NGAYGIAO, D.STATUS='OK' " +
                $"FROM [{tmpTable}] T " +
                $"INNER JOIN TMPPHIEUGIAOHANGDBCT D " +
                $"  ON D.MAHANG=T.MAHANG " +
                $"  AND D.IDP=SUBSTRING(T.TTPHIEU,CHARINDEX('-',T.TTPHIEU)+1,LEN(T.TTPHIEU)) " +
                $"  AND D.STATUS='NG' AND T.LOT<>''");
        }

        #endregion

        #region ══ Facade-level helpers (IPhieuRepository) ══════════════════════

        public DataTable LoadHangThieu(bool isMayBanQR, string tenBan)
        {
            if (isMayBanQR)
                return ExecuteStoredProcedure("Usp_Qrcode_LOAD_HANGTHIEU");

            return ExecuteStoredProcedure("Usp_Qrcode_LOAD_HANGTHIEUView",
                new SqlParameter("@TENBAN", tenBan));
        }

        public Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (maHangList == null || maHangList.Count == 0) return result;

            string inClause = string.Join(",", maHangList.Select(m => $"'{m.Replace("'", "''")}'"));
            string sql = $"SELECT Code, ISNULL(CAST(MinCloseQty AS INT), 0) AS QC FROM B20Item WHERE Code IN ({inClause})";

            DataTable dt = LoadData(sql);
            foreach (DataRow row in dt.Rows)
                result[row["Code"].ToString().Trim()] = Convert.ToInt32(row["QC"]);

            return result;
        }

        public void ExecNonQuery(string spName) => ExecuteStoredProcedureNonQuery(spName);
        public void ExecSP(string spName, params SqlParameter[] parms) => ExecuteStoredProcedureNonQuery(spName, parms);
        public DataTable ExecSPWithResult(string spName, params SqlParameter[] parms) => ExecuteStoredProcedure(spName, parms);

        // ⚠️ SqlRepositoryBase (bản hiện tại) chưa có wrapper ExecuteStoredProcedureNonQuery
        // theo transaction — dùng thẳng Db (không transaction), khớp hành vi gốc của
        // ExecNonQuery/ExecSP (vốn cũng không nằm trong transaction nào cả).
        private int ExecuteStoredProcedureNonQuery(string spName, params SqlParameter[] parms)
            => Db.ExecuteStoredProcedureNonQuery(spName, parms);

        #endregion

        #region ══ Private helpers ═══════════════════════════════════════════════

        private DataTable CallSP(string tenSP, string ngayGiao, string nhaMay, string gioFcc,
            int addNm, PhieuTableSet tables)
        {
            object ngayParam = DateTime.TryParse(ngayGiao, out DateTime dt) ? (object)dt : DBNull.Value;

            var paramList = new List<SqlParameter>
            {
                new SqlParameter("@NGAYGIAO", SqlDbType.SmallDateTime) { Value = ngayParam },
                new SqlParameter("@NHAMAY", nhaMay),
                new SqlParameter("@GIOFCC", gioFcc),
                new SqlParameter("@ADDNM", addNm)
            };

            if (!string.IsNullOrEmpty(tables.TmpTable))
                paramList.Add(new SqlParameter("@TMPTABLE", tables.TmpTable));
            if (!string.IsNullOrEmpty(tables.SourceTable))
                paramList.Add(new SqlParameter("@IFSTABLE", tables.SourceTable));
            if (!string.IsNullOrEmpty(tables.DocQRTable))
                paramList.Add(new SqlParameter("@DOCQRTABLE", tables.DocQRTable));
            if (!string.IsNullOrEmpty(tables.TenBan))
                paramList.Add(new SqlParameter("@TENBAN", tables.TenBan));
            if (!string.IsNullOrEmpty(tables.IfsView))
                paramList.Add(new SqlParameter("@IFSVIEW", tables.IfsView));

            return ExecuteStoredProcedure(tenSP, paramList.ToArray());
        }

        private void DropCreate(string tenBang, DataTable schema)
        {
            int checkExist = Convert.ToInt32(ExecuteScalar(
                "SELECT COUNT(*) FROM sys.objects " +
                $"WHERE object_id = OBJECT_ID(N'[dbo].[{tenBang}]') AND type = 'U'") ?? 0);

            if (checkExist == 0)
                ExecuteNonQuery(SqlTableCreator.GetCreateFromDataTableSQL(tenBang, schema));
            else
                ExecuteNonQuery($"TRUNCATE TABLE [{tenBang}]");
        }

        private void MergeLotTuBangRieng(DataTable dt, string ngayGiao, string tenNhaMay)
        {
            if (dt.Rows.Count == 0) return;
            if (string.IsNullOrEmpty(tenNhaMay)) return;

            string sql =
                "SELECT MAHANG, GIOGIAO, LOT, STATUS, STATUSDOC, " +
                "  ISNULL(PO_NO,'') AS PO_NO, ISNULL(SOLUONG, 0) AS SOLUONG, " +
                "  ISNULL(GearYMVN,'') AS GEAR " +
                "FROM LUUPHIEUGIAOHANG " +
                $"WHERE CAST(NGAYGIAO AS DATE) = '{ngayGiao}' AND NHAMAY = '{tenNhaMay.Replace("'", "''")}'";

            DataTable luuDt = LoadData(sql);
            if (luuDt.Rows.Count == 0) return;

            var lookup = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in luuDt.Rows)
            {
                string gioChuan = NormalizeGio(row["GIOGIAO"].ToString().Trim());
                string keyFull = row["MAHANG"].ToString().Trim() + "|" + gioChuan + "|" + row["PO_NO"].ToString().Trim();
                string keyShort = row["MAHANG"].ToString().Trim() + "|" + gioChuan;

                if (!lookup.ContainsKey(keyFull)) lookup[keyFull] = row;
                if (!lookup.ContainsKey(keyShort)) lookup[keyShort] = row;
            }

            if (!dt.Columns.Contains("GearSuDung"))
                dt.Columns.Add("GearSuDung", typeof(string));

            foreach (DataRow row in dt.Rows)
            {
                string gioChuan = NormalizeGio(row["GIO"].ToString().Trim());
                string maHang = row["MAHANG"].ToString().Trim();
                string poNo = dt.Columns.Contains("PO_NO") ? row["PO_NO"]?.ToString().Trim() ?? "" : "";
                string keyFull = maHang + "|" + gioChuan + "|" + poNo;
                string keyShort = maHang + "|" + gioChuan;

                if (!lookup.TryGetValue(keyFull, out DataRow luuRow))
                    lookup.TryGetValue(keyShort, out luuRow);
                if (luuRow == null) continue;

                string lot = luuRow["LOT"]?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(lot)) continue;

                row["LOT"] = lot;
                row["STATUS"] = luuRow["STATUS"]?.ToString() ?? "NG";
                row["STATUSDOC"] = luuRow["STATUSDOC"]?.ToString() ?? "NG";

                if (dt.Columns.Contains("GEAR"))
                {
                    string gearHienTai = row["GEAR"]?.ToString().Trim() ?? "";
                    string gearLuu = luuRow["GEAR"]?.ToString().Trim() ?? "";
                    if (string.IsNullOrEmpty(gearHienTai) && !string.IsNullOrEmpty(gearLuu))
                        row["GEAR"] = gearLuu;
                }
            }
        }

        private static string NormalizeGio(string gio)
        {
            if (string.IsNullOrWhiteSpace(gio)) return "00";
            gio = gio.Replace("H", "").Trim();
            int colonIdx = gio.IndexOf(':');
            if (colonIdx >= 0) gio = gio.Substring(0, colonIdx).Trim();
            return int.TryParse(gio, out int gioInt) ? gioInt.ToString("00") : "00";
        }

        private static void ValidateTenBan(string tenBan)
        {
            if (string.IsNullOrWhiteSpace(tenBan) ||
                System.Text.RegularExpressions.Regex.IsMatch(tenBan, @"[^A-Za-z0-9_]"))
                throw new ArgumentException($"Tên bảng không hợp lệ: '{tenBan}'");
        }

        private static int SafeInt(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            try { return Convert.ToInt32(val); } catch { return 0; }
        }

        #endregion
    }
}