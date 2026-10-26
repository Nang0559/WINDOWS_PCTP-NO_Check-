using PCTP.Modules.KhoCore.Application.Contracts.Slot;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using System;

namespace PCTP.Modules.KhoVatLy.Application.Adapters
{
    /// <summary>
    /// Transitional adapter from the legacy KhoVatLy slot service to the KhoCore query contract.
    /// This is intentionally kept on the legacy side so KhoCore does not depend on KhoVatLy.
    /// </summary>
    public sealed class KhoCoreSlotQueryAdapter : ISlotQueryService
    {
        private readonly ISlotService _legacy;

        public KhoCoreSlotQueryAdapter(ISlotService legacy)
        {
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        public SlotLocation GetByText(string slotText)
        {
            var slot = _legacy.GetSlotInfoFromString(slotText);
            if (slot == null)
                return null;

            return new SlotLocation
            {
                SlotId = slot.SlotId,
                WarehouseName = slot.WarehouseName,
                RackName = slot.RackName,
                SlotNumber = slot.SlotNumber,
                Capacity = slot.Capacity
            };
        }

        public int GetQuantity(int slotId)
        {
            return _legacy.GetQuantity(slotId);
        }

        public int GetCapacity(int slotId)
        {
            return _legacy.GetCapacity(slotId);
        }
    }
}
