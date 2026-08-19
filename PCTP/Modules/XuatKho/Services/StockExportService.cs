using PCTP.Common;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Models;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuatKho.Services
{
    public sealed class StockExportService : IStockExportService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISlotService _slotService;                 // module Kho
        private readonly IStockExportRepository _stockTpRepo;       // đọc/trừ STOCKTP
        private readonly IStockHistoryRepository _historyRepo;      // dùng chung bảng StockHistory (module Kho)
        private readonly IHangChoGiaoRepository _choGiaoRepo;       // FVN_HangChoGiao
        private readonly IStockExportValidationService _validationService;

        public StockExportService(
            IUnitOfWork uow,
            ISlotService slotService,
            IStockExportRepository stockTpRepo,
            IStockHistoryRepository historyRepo,
            IHangChoGiaoRepository choGiaoRepo,
            IStockExportValidationService validationService)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _stockTpRepo = stockTpRepo ?? throw new ArgumentNullException(nameof(stockTpRepo));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _choGiaoRepo = choGiaoRepo ?? throw new ArgumentNullException(nameof(choGiaoRepo));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        }

        // ════════════════════════════════════════════════════════════════
        // TRƯỜNG HỢP 1 — BƯỚC 1: Xuất khỏi Slot → chờ giao (KHÔNG trừ STOCKTP)
        // ════════════════════════════════════════════════════════════════
        public StockExportResult PickToChoGiao(StockExportRequest request)
        {
            var validation = _validationService.ValidatePickToChoGiao(request);
            if (!validation.IsValid)
                return MapFail(validation);

            _uow.Begin();
            try
            {
                // slotId ở đây LUÔN là Slot.SlotId thật (vị trí vật lý) — request.SlotId
                // do UI/nghiệp vụ chọn, KHÔNG phải SlotLotId.
                int slotId = request.SlotId.Value;

                _slotService.LockSlotForUpdate(slotId);

                var allLots = _slotService.GetLots(slotId);
                var matched = allLots
                    .Where(l => l.Quantity > 0)
                    .Where(l => LotCodeHelper.AreLotKeysEquivalent(l.LotNo, request.LotNo))
                    // ✅ dùng ImportDate cấp 1 của LotInfo — không còn phải đọc qua
                    // QRInfo?.ImportDate (QRInfo có thể null khi Lot lấy từ GetLots).
                    .OrderBy(l => l.ImportDate ?? DateTime.MaxValue)
                    .ToList();

                var others = allLots
                    .Where(l => l.Quantity > 0 && !LotCodeHelper.AreLotKeysEquivalent(l.LotNo, request.LotNo))
                    .ToList();

                int tonThuc = matched.Sum(l => l.Quantity);
                if (tonThuc < request.SoLuong)
                {
                    _uow.Rollback();
                    return StockExportResult.InsufficientStock(
                        $"LOT [{request.LotNo}] trong Slot chỉ còn {tonThuc}, không đủ {request.SoLuong} để pick.");
                }

                var split = LotNoHelper.SubtractLots(matched, request.SoLuong);
                var remaining = others.Concat(split.RemainingLots).ToList();

                // 1) Trừ Slot/SlotLot + cập nhật header
                _slotService.SaveLots(slotId, remaining);
                _slotService.UpdateSlotHeaderFromLots(slotId, remaining);

                var firstExported = split.ExportLots.FirstOrDefault();

                // 2) Tạo dòng chờ giao
                var hangChoGiao = new HangChoGiao
                {
                    LotGoc = request.LotNo,
                    LotThung = firstExported?.TemCode,   // TemCode = tem QR thật, giữ đúng nghĩa gốc
                    MaHang = request.MaHang,
                    SoLuong = request.SoLuong,
                    SlotIdNguon = slotId,
                    LoaiYeuCauGiao = request.Purpose == StockTransactionType.XuatGiaoBuNG
                        ? HangChoGiaoLoai.GiaoBuNG
                        : HangChoGiaoLoai.GiaoHang,
                    TrangThai = HangChoGiaoStatus.ChoGiao,
                    ReferenceType = request.ReferenceType,
                    ReferenceId = request.ReferenceId,
                    NgayXuatKho = DateTime.Now,
                    NguoiXuatKho = request.NguoiThucHien
                };
                int choGiaoId = _choGiaoRepo.Insert(hangChoGiao);

                // 3) History = CHO_GIAO — chưa đụng STOCKTP, chỉ ghi nhận đã rời Slot.
                // ItemCode giờ set trực tiếp trên LotInfo (không cần lồng qua QRInfo).
                // Reference (để truy vết/chống trùng) được nhét vào QRInfo.MaPhieu —
                // ĐÚNG cột StockHistory.MaPhieu mà IStockHistoryRepository map từ
                // QRInfo?.MaPhieu (kế thừa hành vi cũ của SlotHelper.SaveHistory).
                // KHÔNG dùng TemCode cho việc này nữa vì TemCode nay là tem QR thật.
                _historyRepo.SaveHistory(
                    StockHistoryActionType.ChoGiao,
                    request.MaHang,
                    new LotInfo
                    {
                        ItemCode = request.MaHang,
                        LotNo = request.LotNo,
                        Quantity = request.SoLuong,
                        QRInfo = new QRCodeInfo
                        {
                            MaPhieu = StockExportReferenceFormatter.Format(
                                request.ReferenceType, request.ReferenceId)
                        }
                    },
                    fromSlotId: slotId,
                    toSlotId: null,
                    performedBy: request.NguoiThucHien);

                _uow.Commit();

                var items = split.ExportLots
                    .Select(l => new StockExportItem { LotNo = l.LotNo, SoLuong = l.Quantity, SlotId = slotId })
                    .ToList();

                return StockExportResult.Ok(items,
                    message: $"Đã pick {request.SoLuong} SP LOT [{request.LotNo}] vào chờ giao (Id={choGiaoId}).");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return StockExportResult.Fail("Lỗi pick hàng chờ giao: " + ex.Message);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // TRƯỜNG HỢP 1 — BƯỚC 2: Giao thật → trừ STOCKTP
        // ════════════════════════════════════════════════════════════════
        public StockExportResult ConfirmGiaoHangTuChoGiao(int hangChoGiaoId, string nguoiGiao)
        {
            _uow.Begin();
            try
            {
                var item = _choGiaoRepo.GetForUpdate(hangChoGiaoId); // khoá dòng — chặn confirm trùng
                if (item == null)
                {
                    _uow.Rollback();
                    return StockExportResult.Fail($"Không tìm thấy HangChoGiao Id={hangChoGiaoId}.");
                }

                if (item.TrangThai != HangChoGiaoStatus.ChoGiao)
                {
                    _uow.Rollback();
                    return StockExportResult.Duplicate(
                        $"HangChoGiao Id={hangChoGiaoId} đã ở trạng thái {item.TrangThai}, không thể xác nhận lại.");
                }

                int slConLai = _stockTpRepo.GetSlConLai(item.LotGoc);
                if (slConLai < item.SoLuong)
                {
                    _uow.Rollback();
                    return StockExportResult.InsufficientStock(
                        $"STOCKTP LOT [{item.LotGoc}] chỉ còn {slConLai}, không đủ {item.SoLuong} để xác nhận giao.");
                }

                // 1) Trừ STOCKTP — KHÔNG đụng Slot (đã trừ ở bước 1)
                _stockTpRepo.DecreaseStockTp(item.LotGoc, item.SoLuong);

                // 2) Cập nhật trạng thái staging
                _choGiaoRepo.UpdateStatus(hangChoGiaoId, HangChoGiaoStatus.DaGiao, nguoiGiao);

                // 3) History = EXPORT — MaPhieu mã hoá reference để truy vết
                string actionType = item.LoaiYeuCauGiao == HangChoGiaoLoai.GiaoBuNG
                    ? StockHistoryActionType.ChoGiao
                    : StockHistoryActionType.Export;

                _historyRepo.SaveHistory(
                    actionType,
                    item.MaHang,
                    new LotInfo
                    {
                        ItemCode = item.MaHang,
                        LotNo = item.LotGoc,
                        Quantity = item.SoLuong,
                        // fromSlotId dùng SlotIdNguon (vị trí vật lý gốc) — SlotVatLyId
                        // của HangChoGiao không có ở đây vì hàng đã rời khỏi Slot rồi,
                        // SlotIdNguon lưu lại chính là Slot.SlotId thật lúc pick.
                        QRInfo = new QRCodeInfo
                        {
                            MaPhieu = StockExportReferenceFormatter.Format(
                                item.ReferenceType, item.ReferenceId)
                        }
                    },
                    fromSlotId: item.SlotIdNguon,
                    toSlotId: null,
                    performedBy: nguoiGiao);

                _uow.Commit();

                return StockExportResult.Ok(
                    new List<StockExportItem> { new StockExportItem { LotNo = item.LotGoc, SoLuong = item.SoLuong } },
                    message: $"Đã xác nhận giao {item.SoLuong} SP LOT [{item.LotGoc}].");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return StockExportResult.Fail("Lỗi xác nhận giao hàng: " + ex.Message);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // TRƯỜNG HỢP 2 (A0 giao thẳng) + TRƯỜNG HỢP 3 (NG → rework)
        // ════════════════════════════════════════════════════════════════
        public StockExportResult XuatTrucTiep(StockExportRequest request)
        {
            var validation = _validationService.ValidateXuatTrucTiep(request);
            if (!validation.IsValid)
                return MapFail(validation);

            _uow.Begin();
            try
            {
                int slotId = request.Source == StockExportSource.Slot
                    ? request.SlotId.Value
                    : _slotService.GetSlotIdFromString(
                        _slotService.GetOrCreateVirtualSlotText(
                            BulkImportConfig.WarehouseName,
                            BulkImportConfig.RackName,
                            BulkImportConfig.Capacity));

                if (slotId <= 0)
                {
                    _uow.Rollback();
                    return StockExportResult.Fail("Không xác định được Slot nguồn để xuất.");
                }

                _slotService.LockSlotForUpdate(slotId);

                var allLots = _slotService.GetLots(slotId);
                var matched = allLots
                    .Where(l => l.Quantity > 0)
                    .Where(l => LotCodeHelper.AreLotKeysEquivalent(l.LotNo, request.LotNo))
                    .OrderBy(l => l.ImportDate ?? DateTime.MaxValue)
                    .ToList();

                var others = allLots
                    .Where(l => l.Quantity > 0 && !LotCodeHelper.AreLotKeysEquivalent(l.LotNo, request.LotNo))
                    .ToList();

                int tonThuc = matched.Sum(l => l.Quantity);
                if (tonThuc < request.SoLuong)
                {
                    _uow.Rollback();
                    return StockExportResult.InsufficientStock(
                        $"LOT [{request.LotNo}] trong Slot chỉ còn {tonThuc}, không đủ {request.SoLuong}.");
                }

                var split = LotNoHelper.SubtractLots(matched, request.SoLuong);
                var remaining = others.Concat(split.RemainingLots).ToList();

                // 1) Trừ Slot/SlotLot
                _slotService.SaveLots(slotId, remaining);
                _slotService.UpdateSlotHeaderFromLots(slotId, remaining);

                // 2) Trừ STOCKTP — cả A0-giao-thẳng lẫn Rework đều là xuất thật khỏi tồn kho
                _stockTpRepo.DecreaseStockTp(request.LotNo, request.SoLuong);

                // 3) History — Rework dùng ActionType riêng để tách báo cáo
                string actionType = request.Purpose == StockTransactionType.XuatRework
                    ? StockHistoryActionType.Rework
                    : StockHistoryActionType.Export;

                _historyRepo.SaveHistory(
                    actionType,
                    request.MaHang,
                    new LotInfo
                    {
                        ItemCode = request.MaHang,
                        LotNo = request.LotNo,
                        Quantity = request.SoLuong,
                        QRInfo = new QRCodeInfo
                        {
                            MaPhieu = StockExportReferenceFormatter.Format(
                                request.ReferenceType, request.ReferenceId)
                        }
                    },
                    fromSlotId: slotId,
                    toSlotId: null,
                    performedBy: request.NguoiThucHien);

                _uow.Commit();

                var items = split.ExportLots
                    .Select(l => new StockExportItem { LotNo = l.LotNo, SoLuong = l.Quantity, SlotId = slotId })
                    .ToList();

                return StockExportResult.Ok(items,
                    message: $"Đã xuất trực tiếp {request.SoLuong} SP LOT [{request.LotNo}].");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return StockExportResult.Fail("Lỗi xuất kho trực tiếp: " + ex.Message);
            }
        }

        private void SafeRollback()
        {
            try { _uow.Rollback(); } catch { /* không che mất exception gốc */ }
        }

        private static StockExportResult MapFail(StockExportValidationResult v)
        {
            switch (v.FailureStatus)
            {
                case StockExportStatus.Duplicate: return StockExportResult.Duplicate(v.Message);
                case StockExportStatus.InsufficientStock: return StockExportResult.InsufficientStock(v.Message);
                default: return StockExportResult.Fail(v.Message);
            }
        }
    }
}
