using System;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;

namespace PCTP.Modules.XuLyHangLoi.Application.Adapters
{
    /// <summary>
    /// Transitional adapter from the legacy SlotService to the KhoCore stock port.
    /// </summary>
    public sealed class ReworkStockSlotAdapter : IStockSlotRepository
    {
        private readonly ISlotService _legacy;

        public ReworkStockSlotAdapter(ISlotService legacy)
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
    }
}
