using PCTP.Infrastructure.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;

namespace PCTP.Modules.XuLyHangLoi.Application.Adapters
{
    /// <summary>
    /// Backward-compatible type kept for existing composition code.
    /// The implementation is centralized in Infrastructure so XuLyHangLoi
    /// does not own a second copy of the legacy Slot adapter.
    /// </summary>
    public sealed class ReworkStockSlotAdapter : LegacyStockSlotRepositoryAdapter
    {
        public ReworkStockSlotAdapter(ISlotService legacy)
            : base(legacy)
        {
        }
    }
}
