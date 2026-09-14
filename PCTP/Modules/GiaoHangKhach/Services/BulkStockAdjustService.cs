using PCTP.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.VIEWSTOCK.Services
{
    /// <summary>
    /// Điều chỉnh kho ảo A0 khi hàng đã nhập vào A0 sau đó được xuất đi qua
    /// luồng CNK thông thường.
    ///
    /// Physical stock mutation MUST go through IStockMovementService.
    /// IBulkStockSlotRepository is retained only for virtual-slot resolution,
    /// locking and read/query responsibilities during the migration.
    /// </summary>
    public sealed class BulkStockAdjustService: IBulkStockAdjustService
    {
        private readonly IBulkStockSlotRepository _bulkRepo;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly IUnitOfWork _uow;
        private readonly IStockMovementService _stockMovement;

        public BulkStockAdjustService(
            IBulkStockSlotRepository bulkRepo,
            IStockHistoryRepository historyRepo,
            IUnitOfWork uow,
            IStockMovementService stockMovement = null)
        {
            _bulkRepo = bulkRepo
                ?? throw new ArgumentNullException(nameof(bulkRepo));

            _historyRepo = historyRepo
                ?? throw new ArgumentNullException(nameof(historyRepo));

            _uow = uow
                ?? throw new ArgumentNullException(nameof(uow));

            _stockMovement = stockMovement;
        }

        /// <summary>
        /// Tự động trừ số lượng xuất khỏi Slot ảo A0 theo LOT.
        ///
        /// - Resolve + lock Slot A0 locally.
        /// - Read candidate LOTs only for validation/item resolution.
        /// - Mutate Slot/SlotLot through central IStockMovementService.Pick().
        /// - Record StockHistory in the same transaction.
        /// </summary>
        public bool TruKhoAoTheoLot(string lotNo, int slXuat)
        {
            if (slXuat <= 0 || string.IsNullOrWhiteSpace(lotNo))
                return false;

            if (_stockMovement == null)
                throw new InvalidOperationException(
                    "Chưa cấu hình IStockMovementService cho BulkStockAdjustService.");

            int slotId;
            List<LotInfo> candidates;

            _uow.Begin();
            try
            {
                slotId = _bulkRepo.GetOrCreateVirtualSlotId(
                    BulkImportConfig.WarehouseName,
                    BulkImportConfig.RackName,
                    BulkImportConfig.Capacity);

                _bulkRepo.LockSlotForUpdate(slotId);

                var lots = _bulkRepo.GetLots(slotId) ?? new List<LotInfo>();
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

                var itemCode = candidates
                    .Select(x => x.ItemCode)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (string.IsNullOrWhiteSpace(itemCode))
                {
                    _uow.Rollback();
                    return false;
                }

                int available = candidates.Sum(x => x.Quantity);
                int quantity = Math.Min(slXuat, available);

                if (quantity <= 0)
                {
                    _uow.Rollback();
                    return false;
                }

                var movement = _stockMovement.Pick(new StockMovementRequest
                {
                    MovementType = StockMovementRequest.Types.Pick,
                    SlotId = slotId,
                    LotNo = lotNo,
                    ItemCode = itemCode,
                    Quantity = quantity,
                    OccurredAt = DateTime.Now,
                    PerformedBy = "SYSTEM_HVN_CNK",
                    Reason = "EXPORT_AUTO_HVN"
                });

                if (!movement.Success)
                {
                    _uow.Rollback();
                    return false;
                }

                int slThucTeDaTru = movement.ConsumedLots == null
                    ? quantity
                    : movement.ConsumedLots.Sum(x => x.Quantity);

                _historyRepo.SaveHistory(
                    "EXPORT_AUTO_HVN",
                    itemCode,
                    new LotInfo
                    {
                        ItemCode = itemCode,
                        LotNo = lotNo,
                        Quantity = slThucTeDaTru
                    },
                    slotId,
                    null,
                    "SYSTEM_HVN_CNK");

                _uow.Commit();

                if (quantity < slXuat)
                {
                    System.Diagnostics.Debug.WriteLine(
                        string.Format(
                            "[BulkStockAdjust] A0 thiếu {0} cho LOT {1}.",
                            slXuat - quantity,
                            lotNo));
                }

                return true;
            }
            catch
            {
                try { _uow.Rollback(); } catch { }
                throw;
            }
        }
    }
}
