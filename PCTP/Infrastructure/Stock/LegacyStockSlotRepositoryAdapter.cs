using System;
using System.Collections.Generic;
using System.Linq;
using PCTP.Common;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Shared.Helpers;

namespace PCTP.Infrastructure.Stock
{
    /// <summary>
    /// Transitional adapter from the KhoCore stock-slot contract to the legacy
    /// KhoVatLy ISlotService storage API.
    ///
    /// This adapter intentionally lives outside KhoCore so KhoCore never
    /// depends on KhoVatLy. Business modules consume the KhoCore contract;
    /// the composition/legacy boundary owns this dependency.
    /// </summary>
    public class LegacyStockSlotRepositoryAdapter : IStockSlotRepository
    {
        private readonly ISlotService _legacy;

        public LegacyStockSlotRepositoryAdapter(ISlotService legacy)
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

        public StockSlotTakeResult TakeLot(int slotId, string lotNo, string itemCode, int quantity)
        {
            if (slotId <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotId));
            if (string.IsNullOrWhiteSpace(lotNo))
                throw new ArgumentException("LotNo không được rỗng.", nameof(lotNo));
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("ItemCode không được rỗng.", nameof(itemCode));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            var lots = _legacy.GetLots(slotId) ?? new List<LotInfo>();
            var matched = lots
                .Where(x => x.Quantity > 0)
                .Where(x => LotCodeHelper.AreLotKeysEquivalent(x.LotNo, lotNo))
                .Where(x => string.Equals(x.ItemCode, itemCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.ImportDate ?? DateTime.MaxValue)
                .ToList();

            var available = matched.Sum(x => x.Quantity);
            if (available < quantity)
                throw new InvalidOperationException(
                    string.Format("LOT [{0}] / Item [{1}] trong Slot {2} chỉ còn {3}, không đủ {4}.",
                        lotNo, itemCode, slotId, available, quantity));

            var split = LotNoHelper.SubtractLots(matched, quantity);
            var matchedSet = new HashSet<LotInfo>(matched);
            var others = lots.Where(x => !matchedSet.Contains(x)).ToList();
            var merged = others.Concat(split.RemainingLots).ToList();

            _legacy.SaveLots(slotId, merged);
            _legacy.UpdateSlotHeaderFromLots(slotId, merged);

            return new StockSlotTakeResult
            {
                Quantity = quantity,
                ExportLots = split.ExportLots
                    .Select(x => new StockSlotLot
                    {
                        LotNo = x.LotNo,
                        ItemCode = x.ItemCode,
                        Quantity = x.Quantity,
                        TemCode = x.TemCode,
                        RawQr = x.RawQr,
                        ImportDate = x.ImportDate
                    })
                    .ToList()
            };
        }
    }
}
