using System;
using System.Linq;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Kho.Models;

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

            var lots = _legacy.GetLots(slotId) ?? new System.Collections.Generic.List<LotInfo>();
            var existing = lots.FirstOrDefault(x =>
                string.Equals(x.LotNo, lot.LotNo, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ItemCode, lot.ItemCode, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Quantity += lot.Quantity;
                if (!existing.ImportDate.HasValue)
                    existing.ImportDate = lot.ImportDate;
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
    }
}
