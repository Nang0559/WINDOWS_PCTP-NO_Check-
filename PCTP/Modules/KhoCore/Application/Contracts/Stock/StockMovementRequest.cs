using System;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Cross-module command for a stock movement.
    /// Business modules provide intent; KhoCore owns stock invariants and persistence.
    /// </summary>
    public sealed class StockMovementRequest
    {
        public static class Types
        {
            public const string Receive = "RECEIVE";
            public const string Reserve = "RESERVE";
            public const string Pick = "PICK";
            public const string Export = "EXPORT";
            public const string Move = "MOVE";
            public const string ReworkExport = "REWORK_EXPORT";
            public const string ReworkOkReceive = "REWORK_OK_RECEIVE";
            public const string ReworkNgReceive = "REWORK_NG_RECEIVE";
            public const string ReworkCancelReturn = "REWORK_CANCEL_RETURN";
            public const string Correct = "CORRECT";
        }

        public string MovementType { get; set; }
        public int? SlotId { get; set; }
        public int? SlotLotId { get; set; }
        public int? TargetSlotId { get; set; }
        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public string PerformedBy { get; set; }
        public DateTime? OccurredAt { get; set; }
        public string Reason { get; set; }

        // Receiving metadata. These fields are used only by the receiving
        // storage adapter; other stock movements can leave them empty.
        public string ItemName { get; set; }
        public string Model { get; set; }
        public string ProductionCase { get; set; }
        public DateTime? ProductionDate { get; set; }
        public int ProductionQuantity { get; set; }
        public int? ReceivingStatus { get; set; }
    }
}
