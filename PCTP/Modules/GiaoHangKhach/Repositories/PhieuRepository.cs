using PCTP.Domain.Entities;
using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using PCTP.VIEWSTOCK.Models;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Repositories
{
    public sealed class PhieuRepository : IPhieuRepository
    {
        private readonly IPhieuValidationRepository _validation;
        private readonly IPhieuTmpRepository _tmp;
        private readonly IPhieuLotRepository _lot;
        private readonly IPhieuKhoRepository _kho;
        private readonly IPhieuLuuTruRepository _luuTru;
        private readonly IPhieuGiaoDBRepository _giaoDB;
        private readonly PhieuSqlExecutor _db;

        public PhieuRepository(
            PhieuSqlExecutor db,
            IUnitOfWork uow,
            CustomerConfig cfg,
            IBulkStockSlotRepository bulkStockSlotRepo,
            IStockHistoryRepository historyRepo,
            IHangChoGiaoRepository hangChoGiaoRepo = null,
            IIFSRepository ifsRepo = null,
            IStockMovementService stockMovement = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _validation = new PhieuValidationRepository(db, uow);
            _tmp = new PhieuTmpRepository(db, uow);
            _lot = new PhieuLotRepository(db, uow);
            _giaoDB = new PhieuGiaoDBRepository(db, uow);
            _luuTru = new PhieuLuuTruRepository(db, uow);
            _kho = new PhieuKhoRepository(db, uow, bulkStockSlotRepo, historyRepo, _validation, cfg, hangChoGiaoRepo, stockMovement);
        }

        public int CountDocQRCode(string docQRTable) => _validation.CountDocQRCode(docQRTable);
        public bool CheckCoMaNG(string tenBan) => _validation.CheckCoMaNG(tenBan);
        public bool KiemTraMaTrongPhieu(string maHang, string tenBan) => _validation.KiemTraMaTrongPhieu(maHang, tenBan);
        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, PhieuTableSet tables) => _validation.GetDanhSachTrungMaSl(maHang, sl, tables);
        public DataTable GetDanhSachTrungMaSl(string maHang, int sl, string tenBan, string docQRTable) => _validation.GetDanhSachTrungMaSl(maHang, sl, tenBan, docQRTable);
        public int CountTrungMaSl(string maHang, int sl, PhieuTableSet tables) => _validation.CountTrungMaSl(maHang, sl, tables);
        public int CountTrungMaSl(string maHang, int sl, string tenBan, string docQRTable) => _validation.CountTrungMaSl(maHang, sl, tenBan, docQRTable);
        public DataTable GetDonHangChuaLot(PhieuTableSet tables) => _validation.GetDonHangChuaLot(tables);
        public DataTable GetDonHangChuaLot(string tenBan, string docQRTable) => _validation.GetDonHangChuaLot(tenBan, docQRTable);
        public List<FifoViolation> CheckFifoViolations(string tenBangTmp) => _validation.CheckFifoViolations(tenBangTmp);

        public List<FifoViolation> EvaluateFifoViolations(string tmpTable, string docQRTable)
        {
            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(docQRTable);

            // CNK FIFO is scoped strictly to delivery rows that still own QR data.
            // This prevents unscanned/unrelated rows from consuming FIFO allocation.
            return EvaluateCurrentQrFifoViolations(tmpTable, docQRTable);
        }

        public void ReleaseFifoViolations(string tmpTable, string docQRTable, IReadOnlyList<FifoViolation> violations)
        {
            _db.ValidateTableName(tmpTable);
            _db.ValidateTableName(docQRTable);
            if (violations == null || violations.Count == 0) return;

            var affected = new HashSet<int>();
            foreach (FifoViolation violation in violations)
            {
                if (violation == null || violation.Stt <= 0) continue;
                if (!affected.Add(violation.Stt)) continue;
                _lot.LayLaiLotNo(violation.Stt, tmpTable, docQRTable);
            }
        }

        public DataTable SoSanhLechIFS(DataTable donHang, DataTable ifsTable) => _validation.SoSanhLechIFS(donHang, ifsTable);

        public DataTable GetDanhSachMaHang() => _giaoDB.GetDanhSachMaHang();
        public DataTable LoadTmpPhieuGiaoDB(string tenBan, DateTime ngayGiao, int addNm) => _giaoDB.LoadTmpPhieuGiaoDB(tenBan, ngayGiao, addNm);
        public DataTable BuildDonHangTuUpload() => _giaoDB.BuildDonHangTuUpload();
        public void LuuGiaoDB(DataTable donHang, string gioFccMoTa, int addNm, string tmpTable, string ifsTable, string nhaMayOverride = "") => _giaoDB.LuuGiaoDB(donHang, gioFccMoTa, addNm, tmpTable, ifsTable, nhaMayOverride);
        public int TaoPhieuVaChiTietGiaoDB(string ten, DateTime ngayLap, int nhaMay, string nhaMayName, string note, DataTable chiTiet) => _giaoDB.TaoPhieuVaChiTietGiaoDB(ten, ngayLap, nhaMay, nhaMayName, note, chiTiet);

        public DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm, PhieuTableSet tables) => _tmp.LoadPhieuDocQR(ngayGiao, nhaMay, gioFcc, addNm, tables);
        public DataTable LoadPhieuDocQR(string ngayGiao, string nhaMay, string gioFcc, int addNm, string tmpTable, string ifsTable, string docQRTable) => _tmp.LoadPhieuDocQR(ngayGiao, nhaMay, gioFcc, addNm, tmpTable, ifsTable, docQRTable);
        public DataTable LuuVaLoad(PhieuTableSet tables, string tenSP, DataTable donHang, string ngayGiao, string nhaMay, string gioFcc, int addNm) => _tmp.LuuVaLoad(tables, tenSP, donHang, ngayGiao, nhaMay, gioFcc, addNm);
        public DataTable LuuVaLoad(string tenSPBang, string tenSP, DataTable donHang, string ngayGiao, string nhaMay, string gioFcc, int addNm, string tenBan, string docQRTable, string ifsView = "") => _tmp.LuuVaLoad(tenSPBang, tenSP, donHang, ngayGiao, nhaMay, gioFcc, addNm, tenBan, docQRTable, ifsView);
        public void PushIfsSnapshot(string ifsTable, DataTable donHang) => _tmp.PushIfsSnapshot(ifsTable, donHang);
        public DataTable LoadTuTmpTable(string tmpTable) => _tmp.LoadTuTmpTable(tmpTable);
        public DataTable GetDonHangHienTai(string tenBan) => _tmp.GetDonHangHienTai(tenBan);
        public void XoaTmpPhieu(string tenBan) => _tmp.XoaTmpPhieu(tenBan);
        public void XoaDocQRCode(string docQRTable) => _tmp.XoaDocQRCode(docQRTable);
        public TrangThaiBan GetTrangThaiDangBan(PhieuTableSet tables) => _tmp.GetTrangThaiDangBan(tables);
        public TrangThaiBan GetTrangThaiDangBan(string tmpTable, string docQRTable) => _tmp.GetTrangThaiDangBan(tmpTable, docQRTable);
        public TrangThaiBan GetTrangThaiDangBanYMVN(PhieuTableSet tables) => _tmp.GetTrangThaiDangBanYMVN(tables);
        public TrangThaiBan GetTrangThaiDangBanYMVN(string tmpTable, string docQRTable) => _tmp.GetTrangThaiDangBanYMVN(tmpTable, docQRTable);
        public void EnsureTablesExist() => _tmp.EnsureTablesExist();
        public void InsertTmpRow(string tmpTable, string stt, string cua, string truyen, string maHang, string tenHang, string lot, string dv, int slXuat, string ngayGiao, string gear, string gioXuat, string poNo = "", string cusPoNo = "") => _tmp.InsertTmpRow(tmpTable, stt, cua, truyen, maHang, tenHang, lot, dv, slXuat, ngayGiao, gear, gioXuat, poNo, cusPoNo);

        public string GetLotNo(string maHang, int stt, int dem, int slGiao, PhieuTableSet tables) => _lot.GetLotNo(maHang, stt, dem, slGiao, tables);
        public string GetLotNo(string maHang, int stt, int dem, int slGiao, string docQRTable = "DOCQRCODE", string tmpTable = "TMPPHIEUGIAOHANG") => _lot.GetLotNo(maHang, stt, dem, slGiao, docQRTable, tmpTable);
        public void CapNhapLotTmpPhieu(int stt, string lot, string tenBan) => _lot.CapNhapLotTmpPhieu(stt, lot, tenBan);
        public void LayLaiLotNo(int stt, PhieuTableSet tables) => _lot.LayLaiLotNo(stt, tables);
        public void LayLaiLotNo(int stt, string tenBan, string docQRTable) => _lot.LayLaiLotNo(stt, tenBan, docQRTable);
        public DataTable LoadGhepLot(string tenBan = "TMPPHIEUGIAOHANG", string ifsTable = "IFSPHIEUGIAOHANG") => _lot.LoadGhepLot(tenBan, ifsTable);
        public DataTable GetDanhSachLotTuKho(string maHang) => _lot.GetDanhSachLotTuKho(maHang);

        public int CapNhapKho(string gioGiaoFcc, string nhaMay, PhieuTableSet tables, out DataTable errors) => _kho.CapNhapKho(gioGiaoFcc, nhaMay, tables, out errors);
        public int CapNhapKho(string gioGiaoFcc, string nhaMay, string tmpTable, string docQRTable, out DataTable errors) => _kho.CapNhapKho(gioGiaoFcc, nhaMay, tmpTable, docQRTable, out errors);
        public int CapNhapKhoHTN(string nhaMay, PhieuTableSet tables, out DataTable errors) => _kho.CapNhapKhoHTN(nhaMay, tables, out errors);
        public int CapNhapKhoHTN(string nhaMay, string tmpTable, string docQRTable, out DataTable errors) => _kho.CapNhapKhoHTN(nhaMay, tmpTable, docQRTable, out errors);
        public int CapNhapKhoSP(string gioGiaoFcc, string nhaMay, out DataTable errors) => _kho.CapNhapKhoSP(gioGiaoFcc, nhaMay, out errors);
        public bool CapNhapKhoYMVN(int stt, string lotSl, string maHang, string ngayGiao, string gioGiao, string nhaMay, out DS_ERR_CNK error) => _kho.CapNhapKhoYMVN(stt, lotSl, maHang, ngayGiao, gioGiao, nhaMay, out error);
        public void DanhDauDaGiao(string poNo, string maHang, string ngayGiao, CustomerConfig cfg) => _kho.DanhDauDaGiao(poNo, maHang, ngayGiao, cfg);

        public DataTable LoadLuuPhieu(string nhaMay, string ngayGiao, string gioGiaoFcc) => _luuTru.LoadLuuPhieu(nhaMay, ngayGiao, gioGiaoFcc);
        public int LuuPhieuSP(string nhaMay, string ngayGiao, string gioGiaoFcc, string loaiPhieu) => _luuTru.LuuPhieuSP(nhaMay, ngayGiao, gioGiaoFcc, loaiPhieu);
        public void CapNhapTTPHIEU(string nhaMay, string ngayGiao, string gioGiaoFcc, int stt, string ghiChu) => _luuTru.CapNhapTTPHIEU(nhaMay, ngayGiao, gioGiaoFcc, stt, ghiChu);
        public DataTable LoadLuuPhieuCaNgay(string nhaMay, string ngayGiao) => _luuTru.LoadLuuPhieuCaNgay(nhaMay, ngayGiao);
        public Dictionary<string, int> LoadTonKhoBatch(List<string> maHangList) => _luuTru.LoadTonKhoBatch(maHangList);
        public Dictionary<string, int> GetQcDongGoiBatch(List<string> maHangList) => _validation.GetQcDongGoiBatch(maHangList);
        public DataTable TakeLotYMVN(string tmpTable, string docQRTable, bool isLoaiSP) => _lot.TakeLotYMVN(tmpTable, docQRTable, isLoaiSP);
        public DataTable TinhHangThieuTuDonHang(DataTable donHang) => _validation.TinhHangThieuTuDonHang(donHang);

        private List<FifoViolation> EvaluateCurrentQrFifoViolations(string tmpTable, string docQRTable)
        {
            const int keyLen = PCTP.Common.LotCodeHelper.LEN_LEGACY_KEY;
            var result = new List<FifoViolation>();

            DataTable selectedRows = _db.LoadData($@"
SELECT
    tmp.STT,
    tmp.MAHANG AS MaHang,
    tmp.LOT AS LotDaChon,
    ISNULL(tmp.SOLUONG, 0) AS SoLuong
FROM [{tmpTable}] tmp
INNER JOIN FVN_ItemFifoConfig cfg
    ON cfg.ItemCode = tmp.MAHANG
   AND ISNULL(cfg.EnforceFifo, 0) = 1
WHERE ISNULL(tmp.STATUS, '') NOT IN ('NG', 'OK')
  AND ISNULL(tmp.LOT, '') <> ''
  AND EXISTS
  (
      SELECT 1
      FROM [{docQRTable}] qr
      WHERE ISNULL(qr.STTBAN, 0) = tmp.STT
  )
ORDER BY tmp.MAHANG, tmp.STT;");

            foreach (var partGroup in selectedRows.AsEnumerable()
                .GroupBy(r => r["MaHang"]?.ToString()?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                string maHang = partGroup.Key;
                if (string.IsNullOrEmpty(maHang)) continue;

                DataTable fifoRows = _db.LoadData($@"
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
                    .Select(r => new FifoStockLine
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
                        Selections = ParseLotSelections(row["LotDaChon"]?.ToString(), SafeInt(row["SoLuong"]))
                    })
                    .Where(x => x.Selections.Count > 0)
                    .OrderBy(x => x.Stt)
                    .ToList();

                int totalSelectedQty = selectionsByRow.Sum(x => x.Selections.Sum(s => s.Quantity));
                var allowedByLot = BuildAllowedAllocation(fifo, totalSelectedQty);

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

        private static Dictionary<string, int> BuildAllowedAllocation(List<FifoStockLine> fifo, int requiredQty)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int remaining = Math.Max(requiredQty, 0);
            foreach (FifoStockLine row in fifo)
            {
                if (remaining <= 0) break;
                int allowed = Math.Min(remaining, row.Stock);
                if (allowed <= 0) continue;
                result[row.Key] = allowed;
                remaining -= allowed;
            }
            return result;
        }

        private static string FindRequiredLot(List<FifoStockLine> fifo, Dictionary<string, int> remainingAllowed)
        {
            foreach (FifoStockLine row in fifo)
            {
                int remaining;
                if (remainingAllowed.TryGetValue(row.Key, out remaining) && remaining > 0)
                    return row.Display;
            }
            return fifo.Count == 0 ? string.Empty : fifo[0].Display;
        }

        private static List<LotSelection> ParseLotSelections(string value, int defaultQuantity)
        {
            var result = new List<LotSelection>();
            if (string.IsNullOrWhiteSpace(value)) return result;

            foreach (string token in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string part = token.Trim();
                if (string.IsNullOrWhiteSpace(part)) continue;

                int separator = part.LastIndexOf('-');
                if (separator <= 0 || separator >= part.Length - 1)
                {
                    if (defaultQuantity <= 0) continue;
                    result.Add(new LotSelection
                    {
                        LotKey = part.Length <= 13 ? part : part.Substring(0, 13),
                        Lot = part,
                        Quantity = defaultQuantity
                    });
                    continue;
                }

                string lotPart = part.Substring(0, separator).Trim();
                string qtyPart = part.Substring(separator + 1).Trim();
                int quantity;
                if (!int.TryParse(qtyPart, out quantity) || quantity <= 0)
                {
                    if (defaultQuantity <= 0) continue;
                    result.Add(new LotSelection
                    {
                        LotKey = part.Length <= 13 ? part : part.Substring(0, 13),
                        Lot = part,
                        Quantity = defaultQuantity
                    });
                    continue;
                }

                result.Add(new LotSelection
                {
                    LotKey = lotPart.Length <= 13 ? lotPart : lotPart.Substring(0, 13),
                    Lot = lotPart,
                    Quantity = quantity
                });
            }
            return result;
        }

        private static int SafeInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            int.TryParse(value.ToString(), out int result);
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

        private sealed class FifoStockLine
        {
            public string Key { get; set; }
            public string Display { get; set; }
            public int Stock { get; set; }
        }
    }
}