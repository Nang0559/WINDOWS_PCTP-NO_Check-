using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.KhoVatLy.Application.Services
{
    public sealed class StockService : IStockService
    {
        private readonly ISlotService _slotService;
        private readonly IWarehouseService _warehouseService;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly IChoGiaoRepository _choGiaoRepo;
        private readonly IUnitOfWork _uow;

        public StockService(
            ISlotService slotService,
            IWarehouseService warehouseService,
            IStockHistoryRepository historyRepo,
            IChoGiaoRepository choGiaoRepo,
            IUnitOfWork uow)
        {
            _slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _choGiaoRepo = choGiaoRepo ?? throw new ArgumentNullException(nameof(choGiaoRepo));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        // ============================================================
        // 1. NHẬP KHO
        // ============================================================
        #region Nhập kho

        public List<string> GetAvailableSlotsForImport(string itemCode, int soLuongNhap)
            => _slotService.GetEmptySlots(itemCode, soLuongNhap);

        public InspectionConfig GetInspectionConfig(string itemCode)
            => _warehouseService.GetInspectionConfig(itemCode);

        public QRCodeInfo ParseQr(string qrText) => QRCodeParser.ParseQRCode(qrText);

        #endregion

        // ============================================================
        // 2. XUẤT KHO
        // ============================================================
        #region Xuất kho
        public void GhiNhanChoGiao(IEnumerable<LotInfo> exportedLots, int slotIdNguon, string itemCode, string phieuGiaoId)
        {
            if (exportedLots == null) return;

            foreach (var lot in exportedLots)
            {
                if (lot.Quantity <= 0) continue;

                _choGiaoRepo.InsertChoGiao(
                    slotIdNguon: slotIdNguon,
                    lotThung: string.IsNullOrWhiteSpace(lot.TemCode) ? lot.LotNo : lot.TemCode,
                    lotGoc: lot.LotNo,
                    maHang: itemCode,
                    soLuong: lot.Quantity,
                    phieuGiaoId: phieuGiaoId);
            }
        }
        public LotSplitResult ExportFromSlot(
        int slotId,
        int exportQty,
        string itemCode = null,
        string actionType = "EXPORT")
        {
            var currentLots =
                _slotService.GetLots(slotId);

            var result =
                LotNoHelper.SubtractLots(
                    currentLots,
                    exportQty);
            _uow.Begin();
            try
            {
                // 1. Ghi lại LOT còn tồn
                _slotService.SaveLots(
                slotId,
                result.RemainingLots);

            // 2. Đồng bộ Header Slot từ LOT còn tồn
            _slotService.UpdateSlotHeaderFromLots(
                slotId,
                result.RemainingLots);
                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
            // 3. Ghi lịch sử
            foreach (var exportedLot in result.ExportLots)
            {
                _historyRepo.SaveHistory(
                    actionType,
                    itemCode ?? exportedLot.QRInfo?.ItemCode,
                    exportedLot,
                    fromSlotId: slotId,
                    toSlotId: null,
                    performedBy: null);
            }

            return result;
        }

        public class ExportMoveResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public LotSplitResult Split { get; set; }
        }

        public ExportMoveResult ExportAndMoveRemaining(
    int fromSlotId,
    string toSlotSelectedText,
    int exportQty,
    string itemCode = null,
    string actionType = "EXPORT")
        {
            if (fromSlotId <= 0)
            {
                return new ExportMoveResult
                {
                    Success = false,
                    Message = "Slot nguồn không hợp lệ."
                };
            }

            if (exportQty <= 0)
            {
                return new ExportMoveResult
                {
                    Success = false,
                    Message = "Số lượng xuất phải lớn hơn 0."
                };
            }

            int toSlotId =
                _slotService.GetSlotIdFromString(toSlotSelectedText);

            if (toSlotId <= 0)
            {
                return new ExportMoveResult
                {
                    Success = false,
                    Message = "Không tìm thấy Slot đích."
                };
            }

            if (fromSlotId == toSlotId)
            {
                return new ExportMoveResult
                {
                    Success = false,
                    Message = "Slot nguồn và Slot đích không được trùng nhau."
                };
            }

            int capacity =
                _slotService.GetCapacity(toSlotId);

            var sourceLots =
                _slotService.GetLots(fromSlotId);

            var destLots =
                _slotService.GetLots(toSlotId);

            // ------------------------------------------------------------
            // 1. Tách LOT nguồn
            // ------------------------------------------------------------

            var split =
                LotNoHelper.SubtractLots(
                    sourceLots,
                    exportQty);

            // ------------------------------------------------------------
            // 2. Gộp phần còn lại vào Slot đích
            // ------------------------------------------------------------

            var mergedLots =
                LotNoHelper.MergeLotInfos(
                    destLots,
                    split.RemainingLots);

            int finalQty =
                LotNoHelper.GetTotalQuantity(mergedLots);

            if (capacity > 0 && finalQty > capacity)
            {
                return new ExportMoveResult
                {
                    Success = false,
                    Message =
                        $"Không thể chuyển. Tổng số lượng ({finalQty}) " +
                        $"vượt quá sức chứa ({capacity})."
                };
            }

            // ------------------------------------------------------------
            // 3. Ghi Slot đích + Slot nguồn trong cùng transaction
            // ------------------------------------------------------------

            _uow.Begin();

            try
            {
                // Slot đích:
                // ghi LOT mới
                _slotService.SaveLots(
                    toSlotId,
                    mergedLots);

                // đồng bộ Header từ LOT
                _slotService.UpdateSlotHeaderFromLots(
                    toSlotId,
                    mergedLots);

                // Slot nguồn:
                // ghi LOT còn lại
                _slotService.SaveLots(
                    fromSlotId,
                    split.RemainingLots);

                // đồng bộ Header từ LOT còn lại
                _slotService.UpdateSlotHeaderFromLots(
                    fromSlotId,
                    split.RemainingLots);

                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }

            // ------------------------------------------------------------
            // 4. Ghi lịch sử
            // ------------------------------------------------------------

            foreach (var lot in split.ExportLots)
            {
                _historyRepo.SaveHistory(
                    actionType,
                    itemCode ?? lot.QRInfo?.ItemCode,
                    lot,
                    fromSlotId,
                    null,
                    null);
            }

            foreach (var lot in split.RemainingLots)
            {
                _historyRepo.SaveHistory(
                    "MOVE",
                    itemCode ?? lot.QRInfo?.ItemCode,
                    lot,
                    fromSlotId,
                    toSlotId,
                    null);
            }

            return new ExportMoveResult
            {
                Success = true,
                Split = split
            };
        }
        public void SyncSlotFromSplitResult(Slot slot, LotSplitResult result)
        {
            if (slot == null || result == null) return;

            slot.Lots = result.RemainingLots;
            slot.Quantity = LotNoHelper.GetTotalQuantity(result.RemainingLots);
            slot.IsOccupied = slot.Quantity > 0;

            if (result.RemainingLots.Any())
            {
                slot.ItemCode = result.RemainingLots.First().QRInfo?.ItemCode;
                slot.ImportDate = result.RemainingLots.Max(x => x.QRInfo?.ImportDate);
            }
            else
            {
                slot.ItemCode = null;
                slot.ImportDate = null;
            }
        }

        #endregion

        // ============================================================
        // 3. SLOT CHUNG
        // ============================================================
        #region Slot chung
        public void LockSlotForUpdate(int slotId)
{
    if (slotId <= 0)
        throw new ArgumentException("SlotId không hợp lệ.", nameof(slotId));

    _repository.LockSlotForUpdate(slotId);
}
        public List<LotInfo> GetSlotLots(int slotId) => _slotService.GetLots(slotId);

        public void ClearSlotTemporarily(Slot slot) => _slotService.ClearSlotTemporarily(slot);

        #endregion

        // ============================================================
        // 4. IN TEM / PHIẾU
        // ============================================================
        #region In tem / phiếu

        public PrintLotResult CreatePrintData(List<LotInfo> lots) => LotNoHelper.CreatePrintData(lots);

        public List<PXuatINModel> BuildExportPreview(Slot slot, int exportQty, string productName, string nguoiThucHien = "")
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));

            var lots = _slotService.GetLots(slot.SlotId);
            int tongSoLuong = LotNoHelper.GetTotalQuantity(lots);

            if (exportQty > tongSoLuong)
                throw new InvalidOperationException("Số lượng xuất lớn hơn tồn kho.");

            var split = LotNoHelper.SubtractLots(lots, exportQty);
            var exportPrint = LotNoHelper.CreatePrintData(split.ExportLots);
            var remainPrint = LotNoHelper.CreatePrintData(split.RemainingLots);

            var dataSource = new List<PXuatINModel>
        {
            PrintHelper.CreatePrintModel(
                printData: exportPrint, loaiPhieu: "PHIẾU XUẤT", productName: productName,
                slotNumber: slot.SlotNumber, soLuongXuat: exportPrint.Quantity,
                soLuongTon: remainPrint.Quantity, nguoiThucHien: nguoiThucHien)
        };

            if (remainPrint.Quantity > 0)
            {
                dataSource.Add(PrintHelper.CreatePrintModel(
                    printData: remainPrint, loaiPhieu: "PHIẾU NHẬP LẠI KHO", productName: productName,
                    slotNumber: slot.SlotNumber, soLuongXuat: exportPrint.Quantity,
                    soLuongTon: remainPrint.Quantity, nguoiThucHien: nguoiThucHien));
            }

            return dataSource;
        }

        public string GetProductNameByCode(string itemCode) => _warehouseService.GetProductName(itemCode);

        #endregion

        // ============================================================
        // 5. SLOT ẢO / KHO TẠM
        // ============================================================
        #region Slot ảo

        public string GetOrCreateBulkImportSlotText()
            => _slotService.GetOrCreateVirtualSlotText(
                BulkImportConfig.WarehouseName, BulkImportConfig.RackName, BulkImportConfig.Capacity);

        public string GetOrCreateVirtualSlotText(string warehouseName, string rackName, int capacity = 999999999)
            => _slotService.GetOrCreateVirtualSlotText(warehouseName, rackName, capacity);

        /// <summary>
        /// CHỈ ghi SlotLot + Slot — KHÔNG đụng STOCKTP.
        /// ⚠️ KHÔNG dùng cho luồng "Nhập TP từ sản xuất" (đã có NhapTpReceivingService.NhapTpVaoSlot
        /// lo trọn transaction STOCKTP+SlotLot+Slot). CHỈ dùng khi STOCKTP đã được cập nhật ở MỘT
        /// transaction RIÊNG, TRƯỚC KHI gọi hàm này (ví dụ TraHangService.XacNhanNhanHangKhachTraVeKho).
        /// </summary>
        public ScanResult ImportSlotOnlyAfterStockTpAlreadyUpdated(
        string selectedSlotText,
        string lotNo,
        string itemCode,
        int quantity)
            {
            int slotId =
                _slotService.GetSlotIdFromString(selectedSlotText);

            if (slotId <= 0)
                return ScanResult.Fail("Không tìm thấy Slot.");

            if (string.IsNullOrWhiteSpace(lotNo))
                return ScanResult.Fail("LotNo không được rỗng.");

            if (quantity <= 0)
                return ScanResult.Fail("Số lượng phải lớn hơn 0.");

            var newLot = new LotInfo
            {
                LotNo = lotNo,
                Quantity = quantity,
                TemCode = "",
                QRInfo = new QRCodeInfo
                {
                    ItemCode = itemCode,
                    Quantity = quantity,
                    ImportDate = DateTime.Now
                }
            };

            var existingLots =
                _slotService.GetLots(slotId);

            var mergedLots =
                LotNoHelper.MergeLotInfos(
                    existingLots,
                    new List<LotInfo> { newLot });

            int finalQty =
                LotNoHelper.GetTotalQuantity(mergedLots);

            int capacity =
                _slotService.GetCapacity(slotId);

            if (capacity > 0 && finalQty > capacity)
            {
                return ScanResult.Fail(
                    $"Vượt sức chứa Slot ({finalQty}/{capacity}).");
            }

            _uow.Begin();

            try
            {
                // 1. Lưu LOT
                _slotService.SaveLots(
                    slotId,
                    mergedLots);

                // 2. Header Slot được tính lại từ chính mergedLots
                _slotService.UpdateSlotHeaderFromLots(
                    slotId,
                    mergedLots);

                _uow.Commit();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }

            _historyRepo.SaveHistory(
                "CUSTOMER_RETURN",
                itemCode,
                newLot,
                fromSlotId: null,
                toSlotId: slotId,
                performedBy: null);

            return ScanResult.OK(
                $"Đã nhập LOT {lotNo} (SL: {quantity}) vào Slot {slotId}.");
        }

        #endregion
    }
}
