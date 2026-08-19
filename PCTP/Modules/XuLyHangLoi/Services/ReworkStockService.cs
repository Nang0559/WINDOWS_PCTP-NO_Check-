using PCTP.Common;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public sealed class ReworkStockService : IReworkStockService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISlotService _slotService;
        private readonly IStockExportRepository _stockTpRepo;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly ITraHangQTChungRepository _qtChungRepo;
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepo;

        public ReworkStockService(
            IUnitOfWork uow, ISlotService slotService, IStockExportRepository stockTpRepo,
            IStockHistoryRepository historyRepo, ITraHangQTChungRepository qtChungRepo,
            IPhieuXuLyBatThuongRepository phieuXuLyRepo)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _stockTpRepo = stockTpRepo ?? throw new ArgumentNullException(nameof(stockTpRepo));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _qtChungRepo = qtChungRepo ?? throw new ArgumentNullException(nameof(qtChungRepo));
            _phieuXuLyRepo = phieuXuLyRepo ?? throw new ArgumentNullException(nameof(phieuXuLyRepo));
        }

        public List<LotInfo> GetLotsCanRework(string maHang, string lotNo)
        {
            string lotChuan = string.IsNullOrWhiteSpace(lotNo) ? null : LotNoHelper.GetStockTpKey(lotNo);
            return _stockTpRepo.FindLotsWithStock(maHang, lotChuan)
                .Select(r => LotInfo.Create(r.LotNo, r.SlConLai, r.ItemCode))
                .ToList();
        }

        public List<LotInfo> GetLotsCanReworkByPhieuXuLy(int phieuXuLyId)
        {
            var phieu = _phieuXuLyRepo.GetById(phieuXuLyId)
                ?? throw new InvalidOperationException($"Không tìm thấy phiếu xử lý bất thường Id={phieuXuLyId}.");
            return GetLotsCanRework(phieu.MaSanPham, phieu.SoLoLoi);
        }

        public ScanResult XuatKhoRework(int phieuXuLyId, int slotLotId, string lotNo, int soLuong, string nguoiXuat)
        {
            if (soLuong <= 0) return ScanResult.Fail("Số lượng xuất phải lớn hơn 0.");
            string lotChuan = LotNoHelper.GetStockTpKey(lotNo);

            _uow.Begin();
            try
            {
                var slotLot = _slotService.GetLotsBySlotLotId(slotLotId);
                if (slotLot == null)
                {
                    _uow.Rollback();
                    return ScanResult.Fail($"Không tìm thấy SlotLot Id={slotLotId}.");
                }
                if (!LotCodeHelper.AreLotKeysEquivalent(slotLot.LotNo, lotChuan))
                {
                    _uow.Rollback();
                    return ScanResult.Fail($"SlotLot {slotLotId} chứa LOT [{slotLot.LotNo}], không khớp [{lotChuan}].");
                }
                if (slotLot.Quantity < soLuong)
                {
                    _uow.Rollback();
                    return ScanResult.Fail($"SlotLot {slotLotId} chỉ còn {slotLot.Quantity}, không đủ {soLuong}.");
                }

                int tonTruocStockTp = _stockTpRepo.GetSlConLai(lotChuan);

                // ── Atomic check-and-decrement, thay cho lock-rồi-update tách rời ──
                if (!_stockTpRepo.TryDecreaseSlConLai(lotChuan, soLuong))
                {
                    _uow.Rollback();
                    return ScanResult.Fail($"STOCKTP LOT [{lotChuan}] không đủ tồn để xuất {soLuong} (hiện có: {tonTruocStockTp}).");
                }

                _slotService.DecreaseSlotLotQuantity(slotLotId, soLuong);

                int xuatId = _qtChungRepo.InsertXuat(new TraHangQTChungXuat
                {
                    PhieuXuLyId = phieuXuLyId,
                    SlotId = slotLot.SlotVatLyId,
                    LotNo = lotChuan,
                    MaHang = slotLot.ItemCode,
                    SoLuong = soLuong,
                    TonTruoc = tonTruocStockTp,
                    TonSau = tonTruocStockTp - soLuong,
                    NguoiXuat = nguoiXuat,
                    LyDo = "Xuất kho đi rework"
                });

                _historyRepo.SaveHistory(
                     actionType: "REWORK_EXPORT",
                     itemCode: slotLot.ItemCode,
                     lot: new LotInfo
                     {
                         LotNo = lotChuan,
                         Quantity = soLuong,
                         TemCode = StockExportReferenceFormatter.Format(StockExportReferenceType.PhieuXuLyBatThuong, phieuXuLyId)
                     },
                     fromSlotId: slotLot.SlotVatLyId,
                     toSlotId: null,
                     performedBy: nguoiXuat);

                _uow.Commit();
                return ScanResult.OK( $"Đã xuất {soLuong} LOT [{lotChuan}] đi rework (XuatId={xuatId})." );
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi xuất kho rework: " + ex.Message);
            }
        }

        public ScanResult NhapLaiHangNG(int phieuXuLyId, string lotNo, int soLuong, int? slotIdDich, string nguoiNhap)
        {
            if (soLuong <= 0) return ScanResult.Fail("Số lượng nhập phải lớn hơn 0.");
            if (!slotIdDich.HasValue) return ScanResult.Fail("Chưa chọn Slot đích để nhập hàng NG.");

            string lotChuan = LotNoHelper.GetStockTpKey(lotNo);

            _uow.Begin();
            try
            {
                var maHang = _phieuXuLyRepo.GetById(phieuXuLyId)?.MaSanPham;

                // ⚠ Giữ nguyên giả định: KHÔNG gọi AdjustSlConLai ở đây — hàng NG
                // chưa QC xác nhận OK, chỉ ghi nhận vị trí vật lý.
                _slotService.AddQuantity(slotIdDich.Value, soLuong, maHang, DateTime.Now);

                int nhapId = _qtChungRepo.InsertNhapNG(new TraHangQTChungNhapNG
                {
                    PhieuXuLyId = phieuXuLyId,
                    LotNo = lotChuan,
                    MaHang = maHang,
                    SoLuongNG = soLuong,
                    SlotIdNhap = slotIdDich,
                    NguoiNhap = nguoiNhap,
                    LyDo = "Nhập lại hàng NG sau rework"
                });

                _historyRepo.SaveHistory(
                    actionType: "REWORK_EXPORT",
                    itemCode: maHang,
                    lot: new LotInfo
                    {
                        LotNo = lotChuan,
                        Quantity = soLuong,
                        TemCode = StockExportReferenceFormatter.Format(StockExportReferenceType.PhieuXuLyBatThuong, phieuXuLyId)
                    },
                    fromSlotId: slotIdDich,
                    toSlotId: null,
                    performedBy: nguoiNhap);

                _uow.Commit();
                return ScanResult.OK($"Đã nhập lại {soLuong} LOT [{lotChuan}] hàng NG vào Slot {slotIdDich} (NhapId={nhapId}).");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi nhập lại hàng NG: " + ex.Message);
            }
        }

        public ScanResult HoanTraKhoKhiHuy(int phieuXuLyId, string nguoiThucHien)
        {
            _uow.Begin();
            try
            {
                var tongXuatTheoLot = _qtChungRepo.GetXuat(phieuXuLyId)
                    .GroupBy(x => x.LotNo)
                    .Select(g => new { LotNo = g.Key, SlotId = g.First().SlotId, MaHang = g.First().MaHang, TongXuat = g.Sum(x => x.SoLuong) })
                    .ToList();

                if (tongXuatTheoLot.Count == 0)
                {
                    _uow.Commit();
                    return  ScanResult.OK("Không có gì để hoàn trả — phiếu chưa từng xuất kho.");
                }

                var tongNhapNGTheoLot = _qtChungRepo.GetNhapNG(phieuXuLyId)
                    .GroupBy(x => x.LotNo)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SoLuongNG));

                var ketQua = new List<string>();
                foreach (var nhom in tongXuatTheoLot)
                {
                    int daNhapNG = tongNhapNGTheoLot.TryGetValue(nhom.LotNo, out int v) ? v : 0;
                    int conTreo = nhom.TongXuat - daNhapNG;
                    if (conTreo <= 0) continue;

                    string lotChuan = LotNoHelper.GetStockTpKey(nhom.LotNo);

                    _stockTpRepo.AdjustSlConLai(lotChuan, +conTreo);
                    _slotService.AddQuantity(nhom.SlotId, conTreo, nhom.MaHang, DateTime.Now);

                    _historyRepo.SaveHistory(
                    actionType: "REWORK_EXPORT",
                    itemCode: nhom.MaHang,
                    lot: new LotInfo
                    {
                        LotNo = lotChuan,
                        Quantity = nhom.TongXuat,
                        TemCode = StockExportReferenceFormatter.Format(StockExportReferenceType.PhieuXuLyBatThuong, phieuXuLyId)
                    },
                    fromSlotId: nhom.SlotId,
                    toSlotId: null,
                    performedBy: nguoiThucHien); ;

                    ketQua.Add($"LOT [{lotChuan}]: hoàn trả {conTreo} về Slot {nhom.SlotId}");
                }

                _uow.Commit();
                return ketQua.Count == 0
                    ? ScanResult.OK("Toàn bộ hàng xuất đã được nhập lại — không còn gì để hoàn trả.")
                    : ScanResult.OK($"Đã hoàn trả kho do huỷ QT chung:\n" + string.Join("\n", ketQua));
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi hoàn trả kho khi huỷ: " + ex.Message);
            }
        }

        private void SafeRollback() { try { _uow.Rollback(); } catch { } }
    }
}
