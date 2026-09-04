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
           
            _stockExportService =
                stockExportService
                ?? throw new ArgumentNullException(nameof(stockExportService));

            _choGiaoRepo =
                choGiaoRepo
                ?? throw new ArgumentNullException(nameof(choGiaoRepo));

            _slotService =
                slotService
                ?? throw new ArgumentNullException(nameof(slotService));
        }


        // ============================================================
        // 1. DANH SÁCH HÀNG ĐÃ PICK - CHỜ GIAO BÙ
        // ============================================================

        public List<HangChoGiao> GetHangSanSangGiaoBu(
            int phieuKhachTraId)
        {
            if (phieuKhachTraId <= 0)
                return new List<HangChoGiao>();

            return _choGiaoRepo.GetByReference(
                StockExportReferenceType.PhieuKhachTra,
                phieuKhachTraId,
                HangChoGiaoStatus.ChoGiao);
        }


        // ============================================================
        // 2. QUÉT QR HÀNG THAY THẾ
        //
        // Slot
        //   ↓
        // HangChoGiao
        //
        // CHƯA trừ STOCKTP.
        // Việc trừ Slot + tạo HangChoGiao do StockExportService xử lý.
        // ============================================================

        public ScanResult GiaoBuTheoQR(
            int phieuKhachTraId,
            string rawQr,
            string nguoiGiao)
        {
            if (phieuKhachTraId <= 0)
            {
                return ScanResult.Fail(
                    "Thiếu thông tin phiếu khách trả.");
            }

            if (string.IsNullOrWhiteSpace(rawQr))
            {
                return ScanResult.Fail(
                    "Không có dữ liệu QR.");
            }

            if (string.IsNullOrWhiteSpace(nguoiGiao))
            {
                return ScanResult.Fail(
                    "Chưa xác định người giao.");
            }


            // ========================================================
            // 1. PARSE QR
            // ========================================================

            QRCodeInfo qr;

            try
            {
                qr = QRCodeParser.ParseQRCode(
                    rawQr.Trim().ToUpper());
            }
            catch (FormatException ex)
            {
                return ScanResult.Fail(
                    $"QR không đúng định dạng: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ScanResult.Fail(
                    $"Lỗi đọc QR: {ex.Message}");
            }


            // ========================================================
            // 2. KHÔNG CHO TEM TỔNG
            // ========================================================

            if (qr == null)
            {
                return ScanResult.Fail(
                    "Không đọc được thông tin QR.");
            }

            if (qr.IsTongPhieu)
            {
                return ScanResult.Fail(
                    "Vui lòng quét tem THÙNG hàng thay thế, " +
                    "không phải tem tổng.");
            }

            if (string.IsNullOrWhiteSpace(qr.LotNo))
            {
                return ScanResult.Fail(
                    "QR không có LOT.");
            }

            if (string.IsNullOrWhiteSpace(qr.ItemCode))
            {
                return ScanResult.Fail(
                    "QR không có mã hàng.");
            }

            if (qr.Quantity <= 0)
            {
                return ScanResult.Fail(
                    "Số lượng trên tem không hợp lệ.");
            }


            // ========================================================
            // 3. TÌM SLOT ĐANG CHỨA LOT
            // ========================================================

            var slotsChuaLot =
                _slotService.FindSlotsContainingLot(
                    qr.LotNo);

            if (slotsChuaLot == null ||
                slotsChuaLot.Count == 0)
            {
                return ScanResult.Fail(
                    $"Không tìm thấy Slot chứa LOT [{qr.LotNo}].");
            }


            // ========================================================
            // 4. CHỈ LẤY SLOT ĐỦ SỐ LƯỢNG
            // ========================================================

            var candidates =
                slotsChuaLot
                    .Where(x => x.Quantity >= qr.Quantity)
                    .OrderBy(x => x.ImportDate)
                    .ToList();

            if (candidates.Count == 0)
            {
                return ScanResult.Fail(
                    $"Không tìm thấy Slot nào còn đủ " +
                    $"{qr.Quantity} SP của LOT [{qr.LotNo}] " +
                    "để pick hàng giao bù.");
            }


            // ========================================================
            // 5. CHỌN SLOT
            //
            // FIFO theo ImportDate.
            // Không tự cộng/trừ tồn tại đây.
            // ========================================================

            int slotId =
                candidates.First().SlotId;


            // ========================================================
            // 6. TẠO REQUEST
            //
            // Khớp hoàn toàn StockExportRequest hiện tại:
            //
            // Quantity
            // SlotId
            // ReferenceType
            // ReferenceId
            // LyDo
            // NguoiThucHien
            // ========================================================

            var request =
                new StockExportRequest
                {
                    LotNo = qr.LotNo,

                    ItemCode = qr.ItemCode,

                    Quantity = qr.Quantity,

                    Source = StockExportSource.Slot,

                    Purpose = StockTransactionType.XuatGiaoBuNG,

                    SlotId = slotId,

                    ReferenceType =
                        StockExportReferenceType.PhieuKhachTra,

                    ReferenceId =
                        phieuKhachTraId,

                    LyDo =
                        "Pick hàng thay thế để giao bù NG",

                    NguoiThucHien =
                        nguoiGiao
                };


            // ========================================================
            // 7. PICK VÀO HÀNG CHỜ GIAO
            //
            // StockExportService chịu trách nhiệm:
            //
            // Slot
            //   ↓
            // HangChoGiao
            //
            // Không trừ STOCKTP ở bước Pick.
            // ========================================================

            var result =
                _stockExportService.PickToChoGiao(
                    request);

            if (!result.IsOK)
            {
                return ScanResult.Fail(
                    result.Message);
            }


            // ========================================================
            // 8. TRẢ PAYLOAD QR
            // ========================================================

            return ScanResult.OKNhapKho(
                qr,
                nhapItem: null,
                message: result.Message);
        }


        // ============================================================
        // 3. XÁC NHẬN HOÀN TẤT GIAO BÙ
        //
        // HangChoGiao
        //     ↓
        // ConfirmGiaoHangTuChoGiao()
        //     ↓
        // Trừ STOCKTP
        // ============================================================

        public ScanResult XacNhanHoanTatGiaoBu(
            int phieuKhachTraId,
            string nguoiGiao)
        {
            if (phieuKhachTraId <= 0)
            {
                return ScanResult.Fail(
                    "Thiếu thông tin phiếu khách trả.");
            }

            if (string.IsNullOrWhiteSpace(nguoiGiao))
            {
                return ScanResult.Fail(
                    "Chưa xác định người giao.");
            }


            // ========================================================
            // 1. LẤY HÀNG ĐANG CHỜ GIAO
            // ========================================================

            var danhSachChoGiao =
                GetHangSanSangGiaoBu(
                    phieuKhachTraId);

            if (danhSachChoGiao == null ||
                danhSachChoGiao.Count == 0)
            {
                return ScanResult.Fail(
                    "Chưa có hàng nào được pick để giao bù " +
                    "cho phiếu này.");
            }


            // ========================================================
            // 2. XÁC NHẬN TỪNG DÒNG
            //
            // Mỗi dòng có transaction riêng bên
            // StockExportService.
            // ========================================================

            int soLuongThanhCong = 0;

            var loi =
                new List<string>();

            foreach (var item in danhSachChoGiao)
            {
                var result =
                    _stockExportService.ConfirmGiaoHangTuChoGiao(
                        item.Id,
                        nguoiGiao);

                if (result.IsOK)
                {
                    soLuongThanhCong++;
                }
                else
                {
                    loi.Add(
                        $"LOT [{item.LotGoc}]: {result.Message}");
                }
            }


            // ========================================================
            // 3. TẤT CẢ THÀNH CÔNG
            // ========================================================

            if (loi.Count == 0)
            {
                return ScanResult.OK(
                    $"Đã xác nhận giao bù xong " +
                    $"{soLuongThanhCong}/{danhSachChoGiao.Count} " +
                    $"dòng cho phiếu {phieuKhachTraId}.");
            }


            // ========================================================
            // 4. KHÔNG CÓ DÒNG NÀO THÀNH CÔNG
            // ========================================================

            if (soLuongThanhCong == 0)
            {
                return ScanResult.Fail(
                    $"Không xác nhận được dòng nào " +
                    $"({loi.Count} lỗi):\n" +
                    string.Join("\n", loi));
            }


            // ========================================================
            // 5. THÀNH CÔNG MỘT PHẦN
            // ========================================================

            return ScanResult.Fail(
                $"Đã xác nhận " +
                $"{soLuongThanhCong}/{danhSachChoGiao.Count} dòng. " +
                $"Còn {loi.Count} dòng lỗi:\n" +
                string.Join("\n", loi));
        }
    }
}
