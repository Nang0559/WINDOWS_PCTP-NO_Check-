using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Contracts.Delivery
{
    /// <summary>
    /// Immutable, already-validated delivery allocation. This is a command contract only:
    /// it does not execute stock mutations and does not replace the legacy stored procedures.
    /// </summary>
    public sealed class DeliveryStockCommand
    {
        public string BusinessKey { get; private set; }
        public string CustomerCode { get; private set; }
        public string DeliveryReference { get; private set; }
        public string PerformedBy { get; private set; }
        public DateTime OccurredAt { get; private set; }
        public IList<DeliveryPartAllocation> Parts { get; private set; }

        public DeliveryStockCommand(
            string businessKey,
            string customerCode,
            string deliveryReference,
            string performedBy,
            DateTime occurredAt,
            IEnumerable<DeliveryPartAllocation> parts)
        {
            BusinessKey = Required(businessKey, nameof(businessKey));
            CustomerCode = Required(customerCode, nameof(customerCode));
            DeliveryReference = Required(deliveryReference, nameof(deliveryReference));
            PerformedBy = Required(performedBy, nameof(performedBy));
            OccurredAt = occurredAt;
            var copy = (parts ?? Enumerable.Empty<DeliveryPartAllocation>()).ToList();
            if (copy.Count == 0 || copy.Any(x => x == null))
                throw new ArgumentException("Delivery command requires at least one valid PART allocation.", nameof(parts));
            if (copy.Select(x => x.PartCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Count)
                throw new ArgumentException("Each PART must occur once; combine its LOT allocations under one PART.", nameof(parts));
            Parts = copy.AsReadOnly();
        }

        public int TotalQuantity => Parts.Sum(x => x.Quantity);

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value is required.", name);
            return value.Trim();
        }
    }

    /// <summary>One PART line; LOT allocations are kept nested under their PART (F-Y1=part).</summary>
    public sealed class DeliveryPartAllocation
    {
        public string PartCode { get; private set; }
        public string Model { get; private set; }
        public string Gear { get; private set; }
        public IList<DeliveryLotAllocation> Lots { get; private set; }
        public IList<string> QrKeys { get; private set; }
        public int Quantity { get; private set; }

        public DeliveryPartAllocation(
            string partCode,
            string model,
            string gear,
            IEnumerable<DeliveryLotAllocation> lots,
            IEnumerable<string> qrKeys)
        {
            if (string.IsNullOrWhiteSpace(partCode))
                throw new ArgumentException("PART code is required.", nameof(partCode));
            PartCode = partCode.Trim();
            Model = model == null ? string.Empty : model.Trim();
            Gear = gear == null ? string.Empty : gear.Trim();
            var lotCopy = (lots ?? Enumerable.Empty<DeliveryLotAllocation>()).ToList();
            if (lotCopy.Count == 0 || lotCopy.Any(x => x == null))
                throw new ArgumentException("PART requires at least one LOT allocation.", nameof(lots));
            if (lotCopy.GroupBy(x => x.LotKey, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new ArgumentException("Duplicate LOT allocation under one PART must be consolidated.", nameof(lots));
            Lots = lotCopy.AsReadOnly();
            var qrCopy = (qrKeys ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            QrKeys = qrCopy.AsReadOnly();
            Quantity = lotCopy.Sum(x => x.Quantity);
        }
    }

    /// <summary>One FIFO allocation slice; SlotLotId is required when the allocation consumes a physical SlotLot.</summary>
    public sealed class DeliveryLotAllocation
    {
        public string LotKey { get; private set; }
        public string StockTpLotKey { get; private set; }
        public int? SlotLotId { get; private set; }
        public int Quantity { get; private set; }
        public int FifoSequence { get; private set; }

        public DeliveryLotAllocation(string lotKey, string stockTpLotKey, int? slotLotId, int quantity, int fifoSequence)
        {
            if (string.IsNullOrWhiteSpace(lotKey))
                throw new ArgumentException("LOT key is required.", nameof(lotKey));
            if (string.IsNullOrWhiteSpace(stockTpLotKey))
                throw new ArgumentException("STOCKTP LOT key is required.", nameof(stockTpLotKey));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Allocation quantity must be positive.");
            if (fifoSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(fifoSequence));
            if (slotLotId.HasValue && slotLotId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotLotId));
            LotKey = lotKey.Trim();
            StockTpLotKey = stockTpLotKey.Trim();
            SlotLotId = slotLotId;
            Quantity = quantity;
            FifoSequence = fifoSequence;
        }
    }
}
