using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Models;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoCore.Application.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;

namespace PCTP.VIEWSTOCK.Repository
{
    /// <summary>
    /// Nhập thành phẩm vào Slot.
    /// NhapKho sở hữu phiếu nhập / case / production tracking;
    /// mọi mutation STOCKTP + Slot/SlotLot đi qua IStockMovementService.
    /// </summary>
    public sealed class NhapTpReceivingService : INhapTpReceivingService
    {
        private readonly IUnitOfWork _uow;
        private readonly IStockTpRepository _stockTpRepo;
        private readonly IPhieuTrackingRepository _phieuRepo;
        private readonly IStockTpCaseRepository _caseRepo;
        private readonly IStockTpProductionRepository _productionRepo;
        private readonly ISlotService _slotService;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly IStockTpStatusRepository _stockTpStatus;
        private readonly IStockMovementService _stockMovement;

        public NhapTpReceivingService(
            IUnitOfWork uow,
            IStockTpRepository stockTpRepo,
            IPhieuTrackingRepository phieuRepo,
            IStockTpCaseRepository caseRepo,
            IStockTpProductionRepository productionRepo,
            ISlotService slotService,
            IStockHistoryRepository historyRepo,
            IStockTpStatusRepository stockTpStatus,
            IStockMovementService stockMovement = null)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _stockTpRepo = stockTpRepo ?? throw new ArgumentNullException(nameof(stockTpRepo));
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _caseRepo = caseRepo ?? throw new ArgumentNullException(nameof(caseRepo));
            _productionRepo = productionRepo ?? throw new ArgumentNullException(nameof(productionRepo));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _stockTpStatus = stockTpStatus ?? throw new ArgumentNullException(nameof(stockTpStatus));
            _stockMovement = stockMovement;
        }

        public ScanResult KiemTraTruocKhiNhap(QRCodeInfo qr)
        {
            if (qr == null)
                return ScanResult.Fail("Không đọc được dữ liệu QR.");
            if (!qr.IsTongPhieu)
                return ScanResult.Fail("Vui lòng bắn tem TỔNG để nhập kho (không nhận tem thùng).");
            if (qr.Quantity <= 0)
                return ScanResult.Fail("Số lượng trên tem không hợp lệ.");
            if (_phieuRepo.ExistsQrData(qr.RawQr))
                return ScanResult.Trung("Tem này đã được nhập kho trước đó!");
            return ScanResult.OK();
        }

        public ScanResult NhapTpVaoSlot(QRCodeInfo qr, int slotId, PhieuNhapInfo matchedPhieu = null)
        {
            DateTime ngayNhapThucTe = DateTime.Now;

            ScanResult check = KiemTraTruocKhiNhap(qr);
            if (!check.IsOK)
                return check;
            if (_stockMovement == null)
                return ScanResult.Fail("Chưa cấu hình IStockMovementService cho NhapKho.");
            if (slotId <= 0)
                return ScanResult.Fail("Slot đích không hợp lệ.");

            int capacity = _slotService.GetCapacity(slotId);
            if (capacity <= 0)
                return ScanResult.Fail("Slot đích chưa cấu hình sức chứa.");

            PhieuNhapInfo phieuLive = matchedPhieu;
            if (matchedPhieu != null && !string.IsNullOrWhiteSpace(matchedPhieu.Find))
            {
                phieuLive = _productionRepo.GetPhieuByFind(matchedPhieu.Find);
                if (phieuLive == null)
                    return ScanResult.Fail("Không còn tìm thấy phiếu sản xuất [" + matchedPhieu.Find + "]. Vui lòng tải lại danh sách và quét lại tem.");
                if (!string.Equals(phieuLive.LotNo, matchedPhieu.LotNo, StringComparison.OrdinalIgnoreCase))
                    return ScanResult.Fail("LOT của phiếu đã thay đổi (" + matchedPhieu.LotNo + " → " + phieuLive.LotNo + "). Dữ liệu trên màn hình đã cũ, vui lòng tải lại danh sách.");
                if (!string.Equals(phieuLive.MaSP, qr.ItemCode, StringComparison.OrdinalIgnoreCase))
                    return ScanResult.Fail("Mã hàng của phiếu không khớp với tem quét (Phiếu: " + phieuLive.MaSP + " / Tem: " + qr.ItemCode + ").");

                bool vuaMoLai = _stockTpStatus.DongBoSLSXVaMoLaiNeuThayDoi(
                    phieuLive.LotNo, phieuLive.Find, phieuLive.SlSanXuat);
                if (vuaMoLai)
                    phieuLive.KetThucLot = false;
            }

            string lotNo = phieuLive != null
                ? phieuLive.LotNo
                : LotCodeHelper.StripCounterAndQty(qr.RawLotNo ?? qr.LotNo);
            if (string.IsNullOrWhiteSpace(lotNo))
                return ScanResult.Fail("Không xác định được LOT.");

            string caseNo = !string.IsNullOrWhiteSpace(qr.SoPhieuTong)
                ? qr.RawLotNo + qr.SoPhieuTong
                : qr.RawLotNo + "4";

            NhapKhoItem nhapItem = new NhapKhoItem
            {
                Lot = lotNo,
                Part = qr.ItemCode,
                Name = phieuLive != null ? phieuLive.TenSP : qr.ItemCode,
                NgaySX = phieuLive != null ? phieuLive.NgaySX : qr.ImportDate,
                SlSanXuat = phieuLive != null ? phieuLive.SlSanXuat : qr.Quantity,
                SlNhap = qr.Quantity
            };

            try
            {
                _uow.Begin();

                if (_caseRepo.ExistsCaseHistory(caseNo))
                {
                    _uow.Rollback();
                    return ScanResult.Trung("Case [" + caseNo + "] đã được nhập kho trước đó!");
                }

                int qtyHienTai = _slotService.GetQuantityWithLock(slotId);
                int qtySauNhap = qtyHienTai + qr.Quantity;
                if (qtySauNhap > capacity)
                {
                    _uow.Rollback();
                    return ScanResult.Fail("Vượt sức chứa Slot (" + qtySauNhap + "/" + capacity + "). Chọn Slot khác.");
                }

                // Chỉ đọc STOCKTP để xác định status nghiệp vụ; mutation do KhoCore thực hiện.
                int slDaNhapTruoc = _stockTpRepo.ExistsStockTp(lotNo)
                    ? _stockTpRepo.GetSlDaNhap(lotNo)
                    : 0;
                int tongSlSauKhiNhap = slDaNhapTruoc + qr.Quantity;
                int slSanXuatThuc = phieuLive != null ? phieuLive.SlSanXuat : nhapItem.SlSanXuat;
                int status = slSanXuatThuc > 0 && tongSlSauKhiNhap >= slSanXuatThuc ? 1 : 0;

                var movement = _stockMovement.Receive(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Receive,
                    TargetSlotId = slotId,
                    LotNo = lotNo,
                    ItemCode = qr.ItemCode,
                    ItemName = nhapItem.Name,
                    ProductionCase = caseNo,
                    ProductionDate = nhapItem.NgaySX,
                    ProductionQuantity = nhapItem.SlSanXuat,
                    Quantity = qr.Quantity,
                    ReceivingStatus = status,
                    ReferenceType = "NHAP_TP",
                    ReferenceId = qr.MaPhieu,
                    OccurredAt = ngayNhapThucTe,
                    Reason = "NHAP_TP_VAO_SLOT"
                });

                if (!movement.Success)
                {
                    _uow.Rollback();
                    return ScanResult.Fail(movement.Message);
                }

                string maPhieuMoi = PhieuNoHelper.NewMaPhieuNhap(lotNo);
                _phieuRepo.InsertPhieuMoi(
                    slotId, qr.ItemCode, lotNo, qr.Quantity, qr.MaPhieu, qr.RawQr,
                    ngayNhapThucTe, qr.NgaySX, qr.SoPhieuTong, maPhieuMoi,
                    null, PhieuStatus.Active);

                _caseRepo.InsertCaseHistory(caseNo);
                _uow.Commit();
            }
            catch (Exception ex)
            {
                try { _uow.Rollback(); } catch { }
                return ScanResult.Fail("Lỗi nhập kho: " + ex.Message);
            }

            try
            {
                _historyRepo.SaveHistory(
                    "IMPORT", qr.ItemCode,
                    new LotInfo
                    {
                        LotNo = lotNo,
                        Quantity = qr.Quantity,
                        TemCode = qr.MaPhieu,
                        RawQr = qr.RawQr,
                        QRInfo = qr
                    },
                    fromSlotId: null,
                    toSlotId: slotId,
                    performedBy: null);
            }
            catch (Exception exHist)
            {
                System.Diagnostics.Debug.WriteLine("[NhapTpReceivingService] Nhập kho thành công nhưng ghi StockHistory lỗi: " + exHist.Message);
            }

            return ScanResult.OKNhapKho(
                qr,
                nhapItem,
                "Đã nhập LOT " + lotNo + " (SL: " + qr.Quantity + ") vào Slot " + slotId + ".");
        }

        public void MoLaiLot(string lot, string find = null)
        {
            _stockTpStatus.MoLaiLot(lot, find);
        }

        public bool KiemTraKhopTonKho(string lotNo, out int slActive, out int slConLaiStockTp)
        {
            slActive = _phieuRepo.GetTongSlActiveTheoLot(lotNo);
            slConLaiStockTp = _stockTpRepo.GetSlConLai(lotNo);
            return slActive == slConLaiStockTp;
        }

        public List<PhieuNhapInfo> GetPhieuDangSanXuat(int soNgayGanDay = 30)
        {
            return _productionRepo.GetPhieuDangSanXuat(soNgayGanDay);
        }

        public PhieuNhapInfo GetPhieuByFind(string find)
        {
            if (string.IsNullOrWhiteSpace(find)) return null;
            return _productionRepo.GetPhieuByFind(find);
        }

        public PhieuNhapInfo TimPhieuTheoLotQR(string rawLotNoSL, string maHang)
        {
            return _productionRepo.TimPhieuTheoLotQR(rawLotNoSL, maHang);
        }
    }
}
