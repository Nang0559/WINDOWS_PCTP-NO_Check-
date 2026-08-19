using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Enums;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public sealed class GiaoBuNGService : IGiaoBuNGService
    {
        private readonly IStockExportService _stockExportService;
        private readonly IHangChoGiaoRepository _choGiaoRepo;
        private readonly ISlotService _slotService;

        public GiaoBuNGService(
            IStockExportService stockExportService,
            IHangChoGiaoRepository choGiaoRepo,
            ISlotService slotService)
        {
            _stockExportService = stockExportService ?? throw new ArgumentNullException(nameof(stockExportService));
            _choGiaoRepo = choGiaoRepo ?? throw new ArgumentNullException(nameof(choGiaoRepo));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
        }

        // ════════════════════════════════════════════════════════════════
        // Danh sách hàng đã pick (chờ giao bù) cho 1 phiếu khách trả
        // ════════════════════════════════════════════════════════════════
        public List<HangChoGiao> GetHangSanSangGiaoBu(int phieuKhachTraId)
        {
            if (phieuKhachTraId <= 0)
                return new List<HangChoGiao>();

            return _choGiaoRepo.GetByReference(
                StockExportReferenceType.PhieuKhachTra,
                phieuKhachTraId,
                HangChoGiaoStatus.ChoGiao);
        }

        // ════════════════════════════════════════════════════════════════
        // Quét QR tem thùng hàng thay thế → pick khỏi Slot vào danh sách chờ giao bù
        // (KHÔNG trừ STOCKTP ở bước này — đúng luồng Trường hợp 1, Bước 1)
        // ════════════════════════════════════════════════════════════════
        public ScanResult GiaoBuTheoQR(int phieuKhachTraId, string rawQr, string nguoiGiao)
        {
            if (phieuKhachTraId <= 0)
                return ScanResult.Fail("Thiếu thông tin phiếu khách trả.");

            if (string.IsNullOrWhiteSpace(rawQr))
                return ScanResult.Fail("Không có dữ liệu QR.");

            // ── 1. Parse QR ────────────────────────────────────────────────
            QRCodeInfo qr;
            try
            {
                qr = QRCodeParser.ParseQRCode(rawQr.Trim().ToUpper());
            }
            catch (FormatException fex)
            {
                return ScanResult.Fail($"QR không đúng định dạng: {fex.Message}");
            }

            if (qr.IsTongPhieu)
                return ScanResult.Fail("Vui lòng quét tem THÙNG hàng thay thế, không phải tem tổng.");

            if (qr.Quantity <= 0)
                return ScanResult.Fail("Số lượng trên tem không hợp lệ.");

            // ── 2. Xác định Slot đang chứa Lot này ───────────────────────────
            // (Người quét không biết slot, phải tự tra — StockExportRequest bắt buộc SlotId
            // khi Source = Slot)
            var slotsChuaLot = _slotService.FindSlotsContainingLot(qr.LotNo);
            var candidates = slotsChuaLot.Where(s => s.Quantity >= qr.Quantity).ToList();

            if (candidates.Count == 0)
            {
                return ScanResult.Fail(
                    $"Không tìm thấy Slot nào còn đủ {qr.Quantity} SP của LOT [{qr.LotNo}] " +
                    "để pick hàng giao bù.");
            }

            if (candidates.Count > 1)
            {
                // Không tự đoán khi có nhiều Slot đủ điều kiện — tránh pick nhầm.
                // FIFO: chọn slot nhập trước nhất, nhưng chỉ log cảnh báo, vẫn tiếp tục.
                candidates = candidates.OrderBy(s => s.ImportDate).ToList();
            }

            int slotId = candidates.First().SlotId;

            // ── 3. Gọi StockExportService — nơi DUY NHẤT xử lý trừ Slot + ghi ChoGiao ──
            var request = new StockExportRequest
            {
                LotNo = qr.LotNo,
                MaHang = qr.ItemCode,
                SoLuong = qr.Quantity,
                Source = StockExportSource.Slot,
                SlotId = slotId,
                Purpose = StockTransactionType.XuatGiaoBuNG,
                ReferenceType = StockExportReferenceType.PhieuKhachTra,
                ReferenceId = phieuKhachTraId,
                NguoiThucHien = nguoiGiao
            };

            var result = _stockExportService.PickToChoGiao(request);

            return result.IsOK
                ? ScanResult.OKNhapKho(qr, nhapItem: null, message: result.Message)
                : ScanResult.Fail(result.Message);
        }

        // ════════════════════════════════════════════════════════════════
        // Xác nhận toàn bộ hàng đã pick cho phiếu này đã giao bù xong
        // (Trường hợp 1, Bước 2 — trừ STOCKTP cho từng dòng)
        // ════════════════════════════════════════════════════════════════
        public ScanResult XacNhanHoanTatGiaoBu(int phieuKhachTraId, string nguoiGiao)
        {
            if (phieuKhachTraId <= 0)
                return ScanResult.Fail("Thiếu thông tin phiếu khách trả.");

            var danhSachChoGiao = GetHangSanSangGiaoBu(phieuKhachTraId);
            if (danhSachChoGiao.Count == 0)
                return ScanResult.Fail("Chưa có hàng nào được pick để giao bù cho phiếu này.");

            int soLuongThanhCong = 0;
            var loi = new List<string>();

            // Mỗi dòng ConfirmGiaoHangTuChoGiao tự có transaction riêng (đã đúng thiết kế
            // trong StockExportService) — không gộp chung 1 transaction lớn ở đây, vì 1 dòng
            // lỗi (ví dụ thiếu tồn STOCKTP tạm thời) không nên làm rollback các dòng đã
            // xác nhận thành công trước đó.
            foreach (var item in danhSachChoGiao)
            {
                var result = _stockExportService.ConfirmGiaoHangTuChoGiao(item.Id, nguoiGiao);
                if (result.IsOK)
                    soLuongThanhCong++;
                else
                    loi.Add($"LOT [{item.LotGoc}]: {result.Message}");
            }

            if (loi.Count == 0)
            {
                return ScanResult.OK($"Đã xác nhận giao bù xong {soLuongThanhCong}/{danhSachChoGiao.Count} dòng cho phiếu {phieuKhachTraId}.");
                
            }

            if (soLuongThanhCong == 0)
            {
                return ScanResult.Fail(
                    $"Không xác nhận được dòng nào ({loi.Count} lỗi):\n" + string.Join("\n", loi));
            }

            // Thành công một phần — vẫn coi là lỗi để UI bắt user xử lý tiếp các dòng còn lại,
            // nhưng giữ nguyên message liệt kê rõ dòng nào đã xong/dòng nào lỗi.
            return ScanResult.Fail(
                $"Đã xác nhận {soLuongThanhCong}/{danhSachChoGiao.Count} dòng. " +
                $"Còn {loi.Count} dòng lỗi:\n" + string.Join("\n", loi));
        }
    }
}
