using PCTP.Infrastructure.Stock;
using PCTP.Modules.KhoVatLy.Application.Interfaces;

namespace PCTP.Modules.NhapKho.Application.Adapters
{
    /// <summary>
    /// Backward-compatible type kept for existing composition code.
    /// The implementation is centralized in Infrastructure so NhapKho does
    /// not own a second copy of the legacy Slot adapter.
    /// </summary>
    public sealed class StockSlotRepositoryAdapter : LegacyStockSlotRepositoryAdapter
    {
        public StockSlotRepositoryAdapter(ISlotService legacy)
            : base(legacy)
        {
        }
    }
}
