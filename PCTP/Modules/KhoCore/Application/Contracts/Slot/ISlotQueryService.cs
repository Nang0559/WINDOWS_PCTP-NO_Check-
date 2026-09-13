using System.Collections.Generic;

namespace PCTP.Modules.KhoCore.Application.Contracts.Slot
{
    /// <summary>
    /// Read-only slot contract owned by KhoCore.
    /// Implementations may use legacy storage during migration, but callers must not.
    /// </summary>
    public interface ISlotQueryService
    {
        SlotLocation GetById(int slotId);
        SlotLocation GetByText(string slotText);
        int GetQuantity(int slotId);
        int GetCapacity(int slotId);
        IReadOnlyList<SlotLocation> FindContainingLot(string lotNo);
    }
}
