using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Models;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Repository
{
    /// <summary>
    /// Nhập hàng TP vào Slot — thay thế hoàn toàn luồng NHAP_TP cũ.
    /// Mỗi lần nhập = 1 transaction gồm:
    ///   1) Ghi/Cộng dồn STOCKTP (nguồn sự thật cho tổng tồn kho)
    ///   2) Tạo 1 "phiếu" mới (SlotLot, PhieuStatus=Active) tại Slot đã chọn
    ///   3) Cập nhật tổng hợp Slot (Quantity/ItemCode/ImportDate/IsOccupied)
    ///   4) Ghi StockHistory (ActionType = IMPORT hoặc BULK_IMPORT tuỳ Slot đích)
    /// Không bao giờ được làm rời từng bước — nếu 1 bước lỗi, toàn bộ rollback.
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

            public NhapTpReceivingService(
                IUnitOfWork uow,
                IStockTpRepository stockTpRepo,
                IPhieuTrackingRepository phieuRepo,
                IStockTpCaseRepository caseRepo,
                IStockTpProductionRepository productionRepo,
                ISlotService slotService,
                IStockHistoryRepository historyRepo,
                IStockTpStatusRepository stockTpStatus)
            {
                _uow = uow
                    ?? throw new ArgumentNullException(nameof(uow));

                _stockTpRepo = stockTpRepo
                    ?? throw new ArgumentNullException(nameof(stockTpRepo));

                _phieuRepo = phieuRepo
                    ?? throw new ArgumentNullException(nameof(phieuRepo));

                _caseRepo = caseRepo
                    ?? throw new ArgumentNullException(nameof(caseRepo));

                _productionRepo = productionRepo
                    ?? throw new ArgumentNullException(nameof(productionRepo));

                _slotService = slotService
                    ?? throw new ArgumentNullException(nameof(slotService));

                _historyRepo = historyRepo
                    ?? throw new ArgumentNullException(nameof(historyRepo));

                _stockTpStatus = stockTpStatus
                    ?? throw new ArgumentNullException(nameof(stockTpStatus));
            }


            // ============================================================
            // 1. KIỂM TRA TRƯỚC KHI NHẬP
            // ============================================================

            public ScanResult KiemTraTruocKhiNhap(QRCodeInfo qr)
            {
                if (qr == null)
                    return ScanResult.Fail("Không đọc được dữ liệu QR.");

                if (!qr.IsTongPhieu)
                    return ScanResult.Fail(
                        "Vui lòng bắn tem TỔNG để nhập kho (không nhận tem thùng).");

                if (qr.Quantity <= 0)
                    return ScanResult.Fail("Số lượng trên tem không hợp lệ.");

                if (_phieuRepo.ExistsQrData(qr.RawQr))
                    return ScanResult.Trung("Tem này đã được nhập kho trước đó!");

                return ScanResult.OK();
            }


            // ============================================================
            // 2. NHẬP TP VÀO SLOT
            // ============================================================

            public ScanResult NhapTpVaoSlot(
                QRCodeInfo qr,
                int slotId,
                PhieuNhapInfo matchedPhieu = null)
            {
                DateTime ngayNhapThucTe = DateTime.Now;

                // 2.1 Kiểm tra QR
                ScanResult check = KiemTraTruocKhiNhap(qr);
                if (!check.IsOK)
                    return check;

                // 2.2 Kiểm tra Slot
                if (slotId <= 0)
                    return ScanResult.Fail("Slot đích không hợp lệ.");

                int capacity = _slotService.GetCapacity(slotId);
                if (capacity <= 0)
                    return ScanResult.Fail("Slot đích chưa cấu hình sức chứa.");

                // 2.3 Lấy phiếu sản xuất live
                PhieuNhapInfo phieuLive = matchedPhieu;

                if (matchedPhieu != null && !string.IsNullOrWhiteSpace(matchedPhieu.Find))
                {
                    phieuLive = _productionRepo.GetPhieuByFind(matchedPhieu.Find);

                    if (phieuLive == null)
                        return ScanResult.Fail(
                            "Không còn tìm thấy phiếu sản xuất [" + matchedPhieu.Find + "]. " +
                            "Vui lòng tải lại danh sách và quét lại tem.");

                    if (!string.Equals(phieuLive.LotNo, matchedPhieu.LotNo, StringComparison.OrdinalIgnoreCase))
                        return ScanResult.Fail(
                            "LOT của phiếu đã thay đổi (" + matchedPhieu.LotNo +
                            " → " + phieuLive.LotNo + "). Dữ liệu trên màn hình đã cũ, vui lòng tải lại danh sách.");

                    if (!string.Equals(phieuLive.MaSP, qr.ItemCode, StringComparison.OrdinalIgnoreCase))
                        return ScanResult.Fail(
                            "Mã hàng của phiếu không khớp với tem quét (Phiếu: " +
                            phieuLive.MaSP + " / Tem: " + qr.ItemCode + ").");

                    bool vuaMoLai = _stockTpStatus.DongBoSLSXVaMoLaiNeuThayDoi(
                        phieuLive.LotNo, phieuLive.Find, phieuLive.SlSanXuat);

                    if (vuaMoLai)
                        phieuLive.KetThucLot = false;
                }

                // 2.4 Xác định LOT
                string lotNo = phieuLive != null
                    ? phieuLive.LotNo
                    : LotCodeHelper.StripCounterAndQty(qr.RawLotNo ?? qr.LotNo);

                if (string.IsNullOrWhiteSpace(lotNo))
                    return ScanResult.Fail("Không xác định được LOT.");

                // 2.5 Xác định Case
                string caseNo = !string.IsNullOrWhiteSpace(qr.SoPhieuTong)
                    ? qr.RawLotNo + qr.SoPhieuTong
                    : qr.RawLotNo + "4";

                // 2.6 Build item
                NhapKhoItem nhapItem = new NhapKhoItem
                {
                    Lot = lotNo,
                    Part = qr.ItemCode,
                    Name = phieuLive != null ? phieuLive.TenSP : qr.ItemCode,
                    NgaySX = phieuLive != null ? phieuLive.NgaySX : qr.ImportDate,
                    SlSanXuat = phieuLive != null ? phieuLive.SlSanXuat : qr.Quantity,
                    SlNhap = qr.Quantity
                };

                // ========================================================
                // 3. TRANSACTION
                // ========================================================
                try
                {
                    _uow.Begin();

                    // 3.1 Case dedup
                    if (_caseRepo.ExistsCaseHistory(caseNo))
                    {
                        _uow.Rollback();
                        return ScanResult.Trung("Case [" + caseNo + "] đã được nhập kho trước đó!");
                    }

                    // 3.2 Lock + kiểm tra sức chứa
                    int qtyHienTai = _slotService.GetQuantityWithLock(slotId);
                    int qtySauNhap = qtyHienTai + qr.Quantity;

                    if (qtySauNhap > capacity)
                    {
                        _uow.Rollback();
                        return ScanResult.Fail(
                            "Vượt sức chứa Slot (" + qtySauNhap + "/" + capacity + "). Chọn Slot khác.");
                    }

                    // 3.3 STOCKTP
                    bool daTonTai = _stockTpRepo.ExistsStockTp(lotNo);
                    int slDaNhapTruoc = daTonTai ? _stockTpRepo.GetSlDaNhap(lotNo) : 0;
                    int tongSlSauKhiNhap = slDaNhapTruoc + qr.Quantity;
                    int slSanXuatThuc = phieuLive != null ? phieuLive.SlSanXuat : nhapItem.SlSanXuat;
                    int status = slSanXuatThuc > 0 && tongSlSauKhiNhap >= slSanXuatThuc ? 1 : 0;

                    if (daTonTai)
                        _stockTpRepo.UpdateStockTp(lotNo, qr.Quantity, status);
                    else
                        _stockTpRepo.InsertStockTp(nhapItem, status);

                    // 3.4 Phiếu tracking
                    string maPhieuMoi = PhieuNoHelper.NewMaPhieuNhap(lotNo);

                    _phieuRepo.InsertPhieuMoi(
                        slotId, qr.ItemCode, lotNo, qr.Quantity, qr.MaPhieu, qr.RawQr,
                        ngayNhapThucTe, qr.NgaySX, qr.SoPhieuTong, maPhieuMoi,
                        null, PhieuStatus.Active);

                    // 3.5 SlotLot + Slot header
                    List<LotInfo> existingLots = _slotService.GetLots(slotId);

                    LotInfo newLot = new LotInfo
                    {
                        LotNo = lotNo,
                        Quantity = qr.Quantity,
                        TemCode = qr.MaPhieu,
                        RawQr = qr.RawQr,
                        QRInfo = qr
                    };

                    List<LotInfo> mergedLots = LotNoHelper.MergeLotInfos(
                        existingLots, new List<LotInfo> { newLot });

                    _slotService.SaveLots(slotId, mergedLots);
                    _slotService.UpdateSlotHeaderFromLots(slotId, mergedLots);

                    // 3.6 Case history
                    _caseRepo.InsertCaseHistory(caseNo);

                    // 3.7 Commit
                    _uow.Commit();
                }
                catch (Exception ex)
                {
                    try { _uow.Rollback(); }
                    catch { /* không che exception gốc */ }

                    return ScanResult.Fail("Lỗi nhập kho: " + ex.Message);
                }

                // ========================================================
                // 4. AUDIT HISTORY — best-effort, không rollback nghiệp vụ chính
                // ========================================================
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
                    System.Diagnostics.Debug.WriteLine(
                        "[NhapTpReceivingService] Nhập kho thành công nhưng ghi StockHistory lỗi: " +
                        exHist.Message);
                }

                // ========================================================
                // 5. SUCCESS
                // ========================================================
                return ScanResult.OKNhapKho(
                    qr, nhapItem,
                    "Đã nhập LOT " + lotNo + " (SL: " + qr.Quantity + ") vào Slot " + slotId + ".");
            }


            // ============================================================
            // 6. MỞ LẠI LOT
            // ============================================================

            public void MoLaiLot(string lot, string find = null)
            {
                _stockTpStatus.MoLaiLot(lot, find);
            }


            // ============================================================
            // 7. ĐỐI CHIẾU TỒN KHO
            // ============================================================

            public bool KiemTraKhopTonKho(
                string lotNo, out int slActive, out int slConLaiStockTp)
            {
                slActive = _phieuRepo.GetTongSlActiveTheoLot(lotNo);
                slConLaiStockTp = _stockTpRepo.GetSlConLai(lotNo);
                return slActive == slConLaiStockTp;
            }


            // ============================================================
            // 8. TRA CỨU PHIẾU SẢN XUẤT — che IStockTpProductionRepository khỏi Form
            // ============================================================

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
