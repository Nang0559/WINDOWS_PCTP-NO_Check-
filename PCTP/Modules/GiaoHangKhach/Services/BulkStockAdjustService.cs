using PCTP.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public sealed class BulkStockAdjustService : IBulkStockAdjustService
    {
        private readonly IBulkStockSlotRepository _bulkRepo;
        private readonly IStockHistoryRepository _historyRepo;
        private readonly IUnitOfWork _uow;
        private readonly IStockMovementService _stockMovement;

        public BulkStockAdjustService(IBulkStockSlotRepository bulkRepo, IStockHistoryRepository historyRepo, IUnitOfWork uow, IStockMovementService stockMovement = null)
        {
            _bulkRepo = bulkRepo ?? throw new ArgumentNullException(nameof(bulkRepo));
            _historyRepo = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _stockMovement = stockMovement;
        }

        public bool TruKhoAoTheoLot(string lotNo, int slXuat, bool manageTransaction = true)
        {
            if (slXuat <= 0 || string.IsNullOrWhiteSpace(lotNo)) return false;
            if (_stockMovement == null)
                throw new InvalidOperationException("Chưa cấu hình IStockMovementService cho BulkStockAdjustService.");

            try
            {
                if (manageTransaction) _uow.Begin();

                int slotId = _bulkRepo.GetOrCreateVirtualSlotId(BulkImportConfig.WarehouseName, BulkImportConfig.RackName, BulkImportConfig.Capacity);
                _bulkRepo.LockSlotForUpdate(slotId);

                var lots = _bulkRepo.GetLots(slotId) ?? new List<LotInfo>();
                var candidates = lots
                    .Where(l => l.Quantity > 0)
                    .Where(l => LotCodeHelper.AreLotKeysEquivalent(l.LotNo, lotNo))
                    .OrderBy(l => l.QRInfo?.ImportDate ?? DateTime.MaxValue)
                    .ToList();

                if (candidates.Count == 0)
                {
                    if (manageTransaction) _uow.Rollback();
                    return false;
                }

                string itemCode = candidates.Select(x => x.ItemCode).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                if (string.IsNullOrWhiteSpace(itemCode))
                {
                    if (manageTransaction) _uow.Rollback();
                    return false;
                }

                int quantity = Math.Min(slXuat, candidates.Sum(x => x.Quantity));
                if (quantity <= 0)
                {
                    if (manageTransaction) _uow.Rollback();
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
                    throw new InvalidOperationException("Không thể trừ tồn A0: " + (movement.Message ?? "Stock movement thất bại."));

                int slThucTeDaTru = movement.ConsumedLots == null ? quantity : movement.ConsumedLots.Sum(x => x.Quantity);
                _historyRepo.SaveHistory(
                    "EXPORT_AUTO_HVN",
                    itemCode,
                    new LotInfo { ItemCode = itemCode, LotNo = lotNo, Quantity = slThucTeDaTru },
                    slotId,
                    null,
                    "SYSTEM_HVN_CNK");

                if (manageTransaction) _uow.Commit();

                if (quantity < slXuat)
                    System.Diagnostics.Debug.WriteLine(string.Format("[BulkStockAdjust] A0 thiếu {0} cho LOT {1}.", slXuat - quantity, lotNo));

                return true;
            }
            catch
            {
                if (manageTransaction)
                {
                    try { _uow.Rollback(); } catch { }
                }
                throw;
            }
        }
    }
}