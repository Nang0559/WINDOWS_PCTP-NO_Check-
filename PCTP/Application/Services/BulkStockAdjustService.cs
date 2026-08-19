using DevExpress.XtraReports.Design;
using PCTP.Common;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Services
{
    /// <summary>
    /// Điều chỉnh kho ảo A0 (BulkImportConfig) khi hàng đã nhập vào A0 sau đó được
    /// Cập Nhập Kho (CNK) xuất đi qua luồng HVN/YMVN/HTN thông thường — A0 phải tự
    /// trừ theo đúng LOT + số lượng đã xuất, KHÔNG chờ người dùng thao tác thủ công.
    /// </summary>
    public sealed class BulkStockAdjustService
    {
        private readonly IBulkStockSlotRepository _bulkRepo;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly IUnitOfWork _uow;
        public BulkStockAdjustService(
        IBulkStockSlotRepository bulkRepo,
       IStockHistoryRepository historyRepo,
        IUnitOfWork uow)
        {
            _bulkRepo = bulkRepo
                ?? throw new ArgumentNullException(nameof(bulkRepo));

            _historyRepo = historyRepo
                ?? throw new ArgumentNullException(nameof(historyRepo));

            _uow = uow
                ?? throw new ArgumentNullException(nameof(uow));
        }

        /// <summary>
        /// Tự động trừ số lượng xuất khỏi Slot ảo A0 theo LOT.
        ///
        /// - Đọc LOT trực tiếp từ SlotService.
        /// - So khớp LOT bằng LotCodeHelper.AreLotKeysEquivalent().
        /// - Trừ theo FIFO dựa trên ImportDate.
        /// - Lưu lại SlotLot.
        /// - Đồng bộ Header Slot từ danh sách LOT còn lại.
        /// - Ghi StockHistory.
        ///
        /// Không phụ thuộc StockService.
        /// </summary>
        public bool TruKhoAoTheoLot(string lotNo, int slXuat)
        {
            if (slXuat <= 0 || string.IsNullOrWhiteSpace(lotNo)) return false;

            int slotId;
            List<LotInfo> candidates;
            List<LotInfo> remaining;
            int conLai = slXuat;

            _uow.Begin();
            try
            {
                slotId = _bulkRepo.GetOrCreateVirtualSlotId(
                    BulkImportConfig.WarehouseName,
                    BulkImportConfig.RackName,
                    BulkImportConfig.Capacity);

                _bulkRepo.LockSlotForUpdate(slotId);

                var lots = _bulkRepo.GetLots(slotId);
                candidates = lots
                    .Where(l => l.Quantity > 0)
                    .Where(l => LotCodeHelper.AreLotKeysEquivalent(l.LotNo, lotNo))
                    .OrderBy(l => l.QRInfo?.ImportDate ?? DateTime.MaxValue)
                    .ToList();

                if (candidates.Count == 0)
                {
                    _uow.Rollback();
                    return false;
                }

                foreach (var lot in candidates)
                {
                    if (conLai <= 0) break;
                    int tru = Math.Min(conLai, lot.Quantity);
                    lot.Quantity -= tru;
                    conLai -= tru;
                    if (lot.QRInfo != null) lot.QRInfo.Quantity = lot.Quantity;
                }

                remaining = lots.Where(l => l.Quantity > 0).ToList();
                _bulkRepo.SaveLots(slotId, remaining);
                _bulkRepo.UpdateSlotHeaderFromLots(slotId, remaining);

                _uow.Commit(); // ← transaction kết thúc TẠI ĐÂY
            }
            catch
            {
                _uow.Rollback();
                throw;
            }

            // ── Side-effect KHÔNG thuộc transaction chính — lỗi ở đây không được
            // phép làm caller nghĩ là thao tác trừ kho thất bại ──────────────────
            int slThucTeDaTru = slXuat - Math.Max(conLai, 0);
            try
            {
                _historyRepo.SaveHistory("EXPORT_AUTO_HVN", candidates[0].QRInfo?.ItemCode,
                    new LotInfo { LotNo = lotNo, Quantity = slThucTeDaTru },
                    slotId, null, "SYSTEM_HVN_CNK");
            }
            catch (Exception ex)
            {
                // Không throw — trừ kho đã commit thành công, chỉ log ghi sử thất bại
                System.Diagnostics.Debug.WriteLine(
                    $"[BulkStockAdjust] Trừ kho OK nhưng ghi StockHistory lỗi cho LOT {lotNo}: {ex.Message}");
            }

            if (conLai > 0)
                System.Diagnostics.Debug.WriteLine(
                    $"[BulkStockAdjust] CẢNH BÁO: A0 thiếu {conLai} cho LOT {lotNo}.");

            return true;
        }
    }
}
