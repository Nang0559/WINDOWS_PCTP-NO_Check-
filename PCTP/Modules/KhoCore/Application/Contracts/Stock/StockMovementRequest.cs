using System;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Cross-module command for a stock movement.
    /// Business modules provide intent; KhoCore owns stock invariants and persistence.
    /// </summary>
    public sealed class StockMovementRequest
    {
        public string MovementType { get; set; }

        /// <summary>Physical source SlotId when the movement has a slot source.</summary>
        public int? SlotId { get; set; }

        /// <summary>Exact SlotLotId when the movement targets one physical lot row.</summary>
        public int? SlotLotId { get; set; }

        /// <summary>Physical destination SlotId for Move/receiving flows.</summary>
        public int? TargetSlotId { get; set; }

        public string LotNo { get; set; }
        public string ItemCode { get; set; }
        public int Quantity { get; set; }
        public string ReferenceType { get; set; }
        public string ReferenceId { get; set; }
        public string PerformedBy { get; set; }
        public DateTime? OccurredAt { get; set; }
        public string Reason { get; set; }
    }
}
