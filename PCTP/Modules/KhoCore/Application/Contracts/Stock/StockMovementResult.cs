namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    public sealed class StockMovementResult
    {
        public bool Success { get; private set; }
        public bool Duplicate { get; private set; }
        public string Message { get; private set; }

        private StockMovementResult(bool success, bool duplicate, string message)
        {
            Success = success;
            Duplicate = duplicate;
            Message = message;
        }

        public static StockMovementResult Ok(string message = null)
        {
            return new StockMovementResult(true, false, message);
        }

        public static StockMovementResult Fail(string message)
        {
            return new StockMovementResult(false, false, message);
        }

        public static StockMovementResult AlreadyProcessed(string message)
        {
            return new StockMovementResult(false, true, message);
        }
    }
}
