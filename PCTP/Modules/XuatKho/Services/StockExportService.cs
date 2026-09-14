using PCTP.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
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

namespace PCTP.Modules.XuatKho.Services
{
    public sealed class StockExportService : IStockExportService
    {
        private readonly IUnitOfWork _uow;
        private readonly ISlotService _slotService;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly IHangChoGiaoRepository _choGiaoRepo;
        private readonly IStockExportValidationService _validationService;
        private readonly IStockMovementService _stockMovement;

        public StockExportService(
            IUnitOfWork uow,
            ISlotService slotService,
            IStockHistoryRepository historyRepo,
            IHangChoGiaoRepository choGiaoRepo,
            IStockExportValidationService validationService,
            IStockMovementService stockMovement = null)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _choGiaoRepo = choGiaoRepo ?? throw new ArgumentNullException(nameof(choGiaoRepo));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _stockMovement = stockMovement;
        }

        public StockExportResult PickToChoGiao(StockExportRequest request)
        {
            var validation = _validationService.ValidatePickToChoGiao(request);
            if (!validation.IsValid)
                return MapFail(validation);

            if (_stockMovement == null)
                return StockExportResult.Fail("Chưa cấu hình IStockMovementService cho XuatKho.");

            _uow.Begin();
            try
            {
                int slotId = request.SlotId.Value;
                _slotService.LockSlotForUpdate(slotId);

                var movement = _stockMovement.Pick(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Pick,
                    SlotId = slotId,
                    LotNo = request.LotNo,
                    ItemCode = request.ItemCode,
                    Quantity = request.Quantity,
                    ReferenceType = request.ReferenceType,
                    ReferenceId = request.ReferenceId,
                    PerformedBy = request.NguoiThucHien,
                    OccurredAt = DateTime.Now,
                    Reason = "PICK_CHO_GIAO"
                });

                if (!movement.Success)
                {
                    _uow.Rollback();
                    return StockExportResult.InsufficientStock(movement.Message);
                }

                var firstExported = movement.ConsumedLots.FirstOrDefault();

                var hangChoGiao = new HangChoGiao
                {
                    LotGoc = request.LotNo,
                    LotThung = firstExported == null ? null : firstExported.TemCode,
                    MaHang = request.ItemCode,
                    SoLuong = request.Quantity,
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

                _historyRepo.SaveHistory(
                    StockHistoryActionType.ChoGiao,
                    request.ItemCode,
                    new LotInfo
                    {
                        ItemCode = request.ItemCode,
                        LotNo = request.LotNo,
                        Quantity = request.Quantity,
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

                var items = movement.ConsumedLots
                    .Select(l => new StockExportItem
                    {
                        LotNo = l.LotNo,
                        SoLuong = l.Quantity,
                        SlotId = slotId
                    })
                    .ToList();

                return StockExportResult.Ok(items,
                    message: $"Đã pick {request.Quantity} SP LOT [{request.LotNo}] vào chờ giao (Id={choGiaoId}).");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return StockExportResult.Fail("Lỗi pick hàng chờ giao: " + ex.Message);
            }
        }

        public StockExportResult ConfirmGiaoHangTuChoGiao(int hangChoGiaoId, string nguoiGiao)
        {
            if (_stockMovement == null)
                return StockExportResult.Fail("Chưa cấu hình IStockMovementService cho XuatKho.");

            _uow.Begin();
            try
            {
                var item = _choGiaoRepo.GetForUpdate(hangChoGiaoId);
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

                var movement = _stockMovement.Export(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Export,
                    LotNo = item.LotGoc,
                    ItemCode = item.MaHang,
                    Quantity = item.SoLuong,
                    ReferenceType = item.ReferenceType,
                    ReferenceId = item.ReferenceId,
                    PerformedBy = nguoiGiao,
                    OccurredAt = DateTime.Now,
                    Reason = "CONFIRM_GIAO_HANG_CHO_GIAO"
                });

                if (!movement.Success)
                {
                    _uow.Rollback();
                    return StockExportResult.InsufficientStock(movement.Message);
                }

                _choGiaoRepo.UpdateStatus(hangChoGiaoId, HangChoGiaoStatus.DaGiao, nguoiGiao);

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
                    new List<StockExportItem>
                    {
                        new StockExportItem { LotNo = item.LotGoc, SoLuong = item.SoLuong }
                    },
                    message: $"Đã xác nhận giao {item.SoLuong} SP LOT [{item.LotGoc}].");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return StockExportResult.Fail("Lỗi xác nhận giao hàng: " + ex.Message);
            }
        }

        public StockExportResult XuatTrucTiep(StockExportRequest request)
        {
            var validation = _validationService.ValidateXuatTrucTiep(request);
            if (!validation.IsValid)
                return MapFail(validation);

            if (_stockMovement == null)
                return StockExportResult.Fail("Chưa cấu hình IStockMovementService cho XuatKho.");

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

                var pick = _stockMovement.Pick(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Pick,
                    SlotId = slotId,
                    LotNo = request.LotNo,
                    ItemCode = request.ItemCode,
                    Quantity = request.Quantity,
                    ReferenceType = request.ReferenceType,
                    ReferenceId = request.ReferenceId,
                    PerformedBy = request.NguoiThucHien,
                    OccurredAt = DateTime.Now,
                    Reason = request.Purpose == StockTransactionType.XuatRework
                        ? "PICK_REWORK"
                        : "PICK_XUAT_TRUC_TIEP"
                });

                if (!pick.Success)
                {
                    _uow.Rollback();
                    return StockExportResult.InsufficientStock(pick.Message);
                }

                var movement = _stockMovement.Export(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Export,
                    LotNo = request.LotNo,
                    ItemCode = request.ItemCode,
                    Quantity = request.Quantity,
                    ReferenceType = request.ReferenceType,
                    ReferenceId = request.ReferenceId,
                    PerformedBy = request.NguoiThucHien,
                    OccurredAt = DateTime.Now,
                    Reason = request.Purpose == StockTransactionType.XuatRework
                        ? "XUAT_REWORK"
                        : "XUAT_TRUC_TIEP"
                });

                if (!movement.Success)
                {
                    _uow.Rollback();
                    return StockExportResult.InsufficientStock(movement.Message);
                }

                string actionType = request.Purpose == StockTransactionType.XuatRework
                    ? StockHistoryActionType.Rework
                    : StockHistoryActionType.Export;

                _historyRepo.SaveHistory(
                    actionType,
                    request.ItemCode,
                    new LotInfo
                    {
                        ItemCode = request.ItemCode,
                        LotNo = request.LotNo,
                        Quantity = request.Quantity,
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

                var items = pick.ConsumedLots
                    .Select(l => new StockExportItem
                    {
                        LotNo = l.LotNo,
                        SoLuong = l.Quantity,
                        SlotId = slotId
                    })
                    .ToList();

                return StockExportResult.Ok(items,
                    message: $"Đã xuất trực tiếp {request.Quantity} SP LOT [{request.LotNo}].");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return StockExportResult.Fail("Lỗi xuất kho trực tiếp: " + ex.Message);
            }
        }

        private void SafeRollback()
        {
            try { _uow.Rollback(); } catch { }
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

        public LotSplitResult ExportFromSlot(
            int slotId,
            int exportQty,
            string itemCode = null,
            string actionType = "EXPORT")
        {
            if (_stockMovement == null)
                throw new InvalidOperationException("Chưa cấu hình IStockMovementService cho XuatKho.");
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("itemCode là bắt buộc khi ExportFromSlot chuyển sang central stock movement.", nameof(itemCode));

            _uow.Begin();
            try
            {
                // Lock BEFORE reading/splitting LOTs. Otherwise another transaction can
                // change the source slot after GetLots(), making the calculated split stale.
                _slotService.LockSlotForUpdate(slotId);
                var currentLots = _slotService.GetLots(slotId);
                var result = LotNoHelper.SubtractLots(currentLots, exportQty);

                foreach (var exported in result.ExportLots)
                {
                    var movement = _stockMovement.Pick(new StockMovementRequest
                    {
                        MovementType = StockMovementRequest.Types.Pick,
                        SlotId = slotId,
                        LotNo = exported.LotNo,
                        ItemCode = itemCode,
                        Quantity = exported.Quantity,
                        OccurredAt = DateTime.Now,
                        Reason = actionType
                    });

                    if (!movement.Success)
                        throw new InvalidOperationException(
                            "Không thể pick LOT qua central stock movement: " + movement.Message);
                }

                _uow.Commit();
                return result;
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }
    }
}