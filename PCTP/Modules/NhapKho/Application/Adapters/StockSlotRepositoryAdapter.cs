using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Helpers;

namespace PCTP.Modules.NhapKho.Application.Adapters
{
    /// <summary>
    /// Transitional NhapKho adapter for the central LOT-aware stock port.
    /// </summary>
    public sealed class StockSlotRepositoryAdapter : IStockSlotRepository
    {
        private readonly ISlotService _legacy;

        public StockSlotRepositoryAdapter(ISlotService legacy)
        {
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        public int GetLotQuantity(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? 0 : lot.Quantity;
        }

        public string GetLotNo(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? null : lot.LotNo;
        }

        public string GetItemCode(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? null : lot.ItemCode;
        }

        public int? GetSlotId(int slotLotId)
        {
            var lot = _legacy.GetLotsBySlotLotId(slotLotId);
            return lot == null ? (int?)null : lot.SlotVatLyId;
        }

        public void DecreaseLotQuantity(int slotLotId, int quantity)
        {
            _legacy.DecreaseSlotLotQuantity(slotLotId, quantity);
        }

        public void AddQuantity(int slotId, int quantity, string itemCode)
        {
            _legacy.AddQuantity(slotId, quantity, itemCode, DateTime.Now);
        }

        public void AddLot(int slotId, StockSlotLot lot)
        {
            if (lot == null)
                throw new ArgumentNullException(nameof(lot));
            if (string.IsNullOrWhiteSpace(lot.LotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lot));
            if (lot.Quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(lot.Quantity));

            var lots = _legacy.GetLots(slotId) ?? new List<LotInfo>();
            var existing = lots.FirstOrDefault(x =>
                string.Equals(x.LotNo, lot.LotNo, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ItemCode, lot.ItemCode, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Quantity += lot.Quantity;
                if (!existing.ImportDate.HasValue)
                    existing.ImportDate = lot.ImportDate;
                if (string.IsNullOrWhiteSpace(existing.TemCode))
                    existing.TemCode = lot.TemCode;
                if (string.IsNullOrWhiteSpace(existing.RawQr))
                    existing.RawQr = lot.RawQr;
            }
            else
            {
                lots.Add(new LotInfo
                {
                    SlotVatLyId = slotId,
                    ItemCode = lot.ItemCode,
                    LotNo = lot.LotNo,
                    Quantity = lot.Quantity,
                    TemCode = lot.TemCode,
                    RawQr = lot.RawQr,
                    ImportDate = lot.ImportDate ?? DateTime.Now
                });
            }

            _legacy.SaveLots(slotId, lots);
            _legacy.UpdateSlotHeaderFromLots(slotId, lots);
        }

        public void TakeLot(int slotId, string lotNo, int quantity)
        {
            if (slotId <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotId));
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            var lots = _legacy.GetLots(slotId) ?? new List<LotInfo>();
            var matched = lots
                .Where(x => x.Quantity > 0)
                .Where(x => LotCodeHelper.AreLotKeysEquivalent(x.LotNo, lotNo))
                .OrderBy(x => x.ImportDate ?? DateTime.MaxValue)
                .ToList();

            var available = matched.Sum(x => x.Quantity);
            if (available < quantity)
                throw new InvalidOperationException(
                    string.Format("LOT [{0}] trong Slot {1} chỉ còn {2}, không đủ {3}.",
                        lotNo, slotId, available, quantity));

            var remaining = LotNoHelper.SubtractLots(matched, quantity).RemainingLots;
            var matchedIds = new HashSet<LotInfo>(matched);
            var others = lots.Where(x => !matchedIds.Contains(x)).ToList();

            var merged = others.Concat(remaining).ToList();
            _legacy.SaveLots(slotId, merged);
            _legacy.UpdateSlotHeaderFromLots(slotId, merged);
        }
    }
}
