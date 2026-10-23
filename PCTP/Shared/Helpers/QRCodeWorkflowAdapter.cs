using System;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;

namespace PCTP.Shared.Helpers
{
    /// <summary>
    /// Bridges the production QR parser to the central stock-movement command.
    /// UI handlers should not duplicate QR field parsing or invent their own
    /// LOT/quantity mapping.
    /// </summary>
    public static class QRCodeWorkflowAdapter
    {
        public static StockMovementRequest BuildReceivingRequest(string rawQr, int targetSlotId, string performedBy, string referenceId = null)
        {
            var qr = QRCodeParser.ParseQRCode(rawQr);
            if (targetSlotId <= 0) throw new ArgumentOutOfRangeException(nameof(targetSlotId));
            if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy is required.", nameof(performedBy));

            return new StockMovementRequest
            {
                MovementType = StockMovementRequest.Types.Receive,
                TargetSlotId = targetSlotId,
                LotNo = qr.LotNo,
                ItemCode = qr.ItemCode,
                Quantity = qr.Quantity,
                ReferenceType = "QR_RECEIVE",
                ReferenceId = string.IsNullOrWhiteSpace(referenceId) ? qr.MaPhieu : referenceId.Trim(),
                PerformedBy = performedBy.Trim(),
                OccurredAt = qr.ImportDate,
                ProductionDate = qr.ImportDate,
                ProductionQuantity = qr.Quantity,
                QrData = qr.RawQr,
                TemCode = qr.MaPhieu
            };
        }

        public static StockMovementRequest BuildCustomerDeliveryRequest(string rawQr, int slotLotId, string performedBy, string deliveryReference)
        {
            var qr = QRCodeParser.ParseQRCode(rawQr);
            if (slotLotId <= 0) throw new ArgumentOutOfRangeException(nameof(slotLotId));
            if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy is required.", nameof(performedBy));
            if (string.IsNullOrWhiteSpace(deliveryReference)) throw new ArgumentException("Delivery reference is required.", nameof(deliveryReference));

            return new StockMovementRequest
            {
                MovementType = StockMovementRequest.Types.Export,
                SlotLotId = slotLotId,
                LotNo = qr.LotNo,
                ItemCode = qr.ItemCode,
                Quantity = qr.Quantity,
                ReferenceType = "QR_DELIVERY",
                ReferenceId = deliveryReference.Trim(),
                PerformedBy = performedBy.Trim(),
                OccurredAt = qr.ImportDate,
                QrData = qr.RawQr,
                TemCode = qr.MaPhieu
            };
        }
    }
}
