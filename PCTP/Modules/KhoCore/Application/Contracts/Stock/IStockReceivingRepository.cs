using System;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Minimal persistence port for normal finished-goods receiving.
    /// The implementation may remain a legacy adapter during migration.
    /// </summary>
    public interface IStockReceivingRepository
    {
        bool Exists(string lotNo);
        int GetReceivedQuantity(string lotNo);
        void Insert(StockReceivingRecord record);
        void Update(string lotNo, int receivedQuantityDelta, int status);
    }

    public sealed class StockReceivingRecord
    {
        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public int ReceivedQuantity { get; set; }
        public int Status { get; set; }
        public string Model { get; set; }
        public string ProductionCase { get; set; }
        public DateTime? ProductionDate { get; set; }
        public int ProductionQuantity { get; set; }
    }
}
