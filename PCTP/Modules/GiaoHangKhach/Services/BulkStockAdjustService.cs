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

        /// <summary>
        /// Trừ SlotLot tương ứng lượng đã trừ ở STOCKTP khi giao hàng khách, để giữ
        /// STOCKTP.SLCONLAI = SUM(SlotLot.Quantity, PhieuStatus = 0) theo LOT.
        /// Thứ tự: Slot ảo A0 trước, phần còn thiếu trừ tiếp ở các Slot thật (FIFO).
        ///
        /// Thiếu hàng trong SlotLot:
        /// - manageTransaction = false (cùng UoW với việc trừ STOCKTP): ném lỗi để rollback cả phiếu.
        /// - manageTransaction = true (STOCKTP đã trừ ở transaction khác, vd. SP): trừ phần có,
        ///   ghi lịch sử EXPORT_SHORTFALL để truy vết/đối chiếu, KHÔNG nuốt im lặng.
        /// Trả về true nếu có trừ được ít nhất một phần ở SlotLot.
        /// </summary>
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

                int conCanTru = slXuat;
                bool daTruSlotLot = false;
                string itemChung = null;   // ItemCode đã biết, dùng cho lịch sử thiếu hàng

                // 1) Slot ảo A0
                var lots = _bulkRepo.GetLots(slotId) ?? new List<LotInfo>();
                var candidates = lots
                    .Where(l => l.Quantity > 0)
                    .Where(l => LotCodeHelper.AreLotKeysEquivalent(l.LotNo, lotNo))
                    .ToList();

                if (candidates.Count > 0)
                {
                    string itemA0 = candidates.Select(x => x.ItemCode).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    if (!string.IsNullOrWhiteSpace(itemA0))
                    {
                        int qtyA0 = Math.Min(conCanTru, candidates.Where(x => string.Equals((x.ItemCode ?? "").Trim(), itemA0.Trim(), StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity));
                        itemChung = itemA0;
                        conCanTru -= TruTaiSlot(slotId, lotNo, itemA0, qtyA0);
                        daTruSlotLot = true;
                    }
                }

                // 2) Slot thật chứa LOT (trước đây bỏ qua -> STOCKTP đã trừ nhưng SlotLot còn nguyên)
                if (conCanTru > 0)
                {
                    foreach (var vt in _bulkRepo.FindSlotLotsByLot(lotNo, slotId) ?? new List<BulkSlotLotLocation>())
                    {
                        if (conCanTru <= 0) break;
                        int take = Math.Min(conCanTru, vt.Quantity);
                        if (take <= 0) continue;
                        itemChung = itemChung ?? vt.ItemCode;
                        conCanTru -= TruTaiSlot(vt.SlotId, lotNo, vt.ItemCode, take);
                        daTruSlotLot = true;
                    }
                }

                // 3) Còn thiếu -> báo rõ, không nuốt im lặng
                if (conCanTru > 0)
                {
                    string msg = string.Format(
                        "LOT [{0}] cần trừ {1} nhưng SlotLot chỉ trừ được {2} (thiếu {3}). STOCKTP và SlotLot sẽ lệch nếu STOCKTP đã trừ đủ.",
                        lotNo, slXuat, slXuat - conCanTru, conCanTru);

                    if (!manageTransaction)
                        throw new InvalidOperationException(msg);

                    _historyRepo.SaveHistory(
                        "EXPORT_SHORTFALL",
                        itemChung ?? string.Empty,
                        new LotInfo { LotNo = lotNo, Quantity = conCanTru },
                        slotId,
                        null,
                        "SYSTEM_HVN_CNK");
                    System.Diagnostics.Debug.WriteLine("[BulkStockAdjust] " + msg);
                }

                if (manageTransaction) _uow.Commit();
                return daTruSlotLot;
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

        private int TruTaiSlot(int slotId, string lotNo, string itemCode, int quantity)
        {
            if (quantity <= 0) return 0;

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
                throw new InvalidOperationException("Không thể trừ tồn Slot " + slotId + ": " + (movement.Message ?? "Stock movement thất bại."));

            int daTru = movement.ConsumedLots == null ? quantity : movement.ConsumedLots.Sum(x => x.Quantity);
            _historyRepo.SaveHistory(
                "EXPORT_AUTO_HVN",
                itemCode,
                new LotInfo { ItemCode = itemCode, LotNo = lotNo, Quantity = daTru },
                slotId,
                null,
                "SYSTEM_HVN_CNK");
            return daTru;
        }
    }
}