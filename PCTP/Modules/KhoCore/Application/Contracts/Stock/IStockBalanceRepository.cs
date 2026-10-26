namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Minimal stock-balance port used by the central stock-movement application layer.
    /// Implementations may temporarily delegate to legacy storage during migration.
    /// </summary>
    public interface IStockBalanceRepository
    {
        int GetAvailableQuantity(string lotNo);

        void DecreaseAvailableQuantity(string lotNo, int quantity);

        void AdjustAvailableQuantity(string lotNo, int delta);

        bool TryDecreaseAvailableQuantity(string lotNo, int quantity);
    }
}
