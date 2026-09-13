namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Single application boundary for stock mutations.
    /// Implementations must execute the complete movement atomically and must not expose UI/DataTable types.
    /// </summary>
    public interface IStockMovementService
    {
        StockMovementResult Receive(StockMovementRequest request);
        StockMovementResult Pick(StockMovementRequest request);
        StockMovementResult Export(StockMovementRequest request);
        StockMovementResult Move(StockMovementRequest request);
        StockMovementResult ReturnFromRework(StockMovementRequest request);
    }
}
