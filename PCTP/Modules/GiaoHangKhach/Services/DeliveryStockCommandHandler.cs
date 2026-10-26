using System;
using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Shared.Common;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// B0-01 delivery command handler - FIRST SLICE (P1, P2, P3, P5, P6, P7, P8, P9, P10).
    /// Every PART/LOT/STT of a command commits or rolls back as one unit (F-Y4); STOCKTP and
    /// SlotLot are mutated only through IStockMovementService.Export. It never calls the legacy
    /// Usp_Qrcode_Update_Stock* procedures (migration constraint 2).
    ///
    /// NOT WIRED into production composition (migration constraint 3). Out of scope here and
    /// failing closed: allocations without SlotLotId (A0/virtual stock), YMVN Gear/Model (P11),
    /// MP/SP table variants (P12) and DG scan-row archiving (P13).
    /// </summary>
    public sealed class DeliveryStockCommandHandler : IDeliveryStockCommandHandler
    {
        public const string ReferenceType = "GIAOHANG_KHACH";

        private readonly IUnitOfWork _uow;
        private readonly IStockMovementService _stock;
        private readonly IDeliveryCommandLedger _ledger;
        private readonly IDeliveryPendingGate _pending;

        public DeliveryStockCommandHandler(
            IUnitOfWork uow,
            IStockMovementService stock,
            IDeliveryCommandLedger ledger,
            IDeliveryPendingGate pending)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _pending = pending ?? throw new ArgumentNullException(nameof(pending));
        }

        public DeliveryStockCommandResult Execute(DeliveryStockCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            // Fail closed before opening a transaction: no physical SlotLot -> not in this slice.
            foreach (var part in command.Parts)
                foreach (var lot in part.Lots)
                    if (!lot.SlotLotId.HasValue)
                        return DeliveryStockCommandResult.Fail(
                            "PART " + part.PartCode + " LOT " + lot.LotKey +
                            ": thiếu SlotLotId; đường A0/ảo chưa thuộc handler này.");

            _uow.Begin();
            try
            {
                int? prior = _ledger.FindCommittedQuantity(command.BusinessKey);
                if (prior.HasValue)
                {
                    _uow.Rollback();
                    return DeliveryStockCommandResult.AlreadyProcessed(
                        "BusinessKey đã được xử lý trước đó.", prior.Value);
                }

                int ledgerId = _ledger.Insert(command);

                foreach (var part in command.Parts)
                {
                    foreach (var lot in part.Lots) // already ordered by FifoSequence
                    {
                        var result = _stock.Export(new StockMovementRequest
                        {
                            MovementType = StockMovementRequest.Types.Export,
                            SlotLotId = lot.SlotLotId,
                            LotNo = lot.StockTpLotKey,
                            ItemCode = part.PartCode,
                            Quantity = lot.Quantity,
                            ReferenceType = ReferenceType,
                            // Stable per business key: a retry can never mint a new reference.
                            ReferenceId = command.BusinessKey + "|" + part.PartCode + "|" + lot.FifoSequence,
                            PerformedBy = command.PerformedBy,
                            OccurredAt = command.OccurredAt
                        });

                        if (result == null || !result.Success)
                            return Abort(result == null ? "Stock movement trả về null." : result.Message);
                        if (result.Duplicate)
                            return Abort("Tham chiếu xuất kho đã tồn tại ngoài ledger; huỷ để tránh lệch tồn.");
                    }

                    foreach (var qr in part.QrKeys)
                        if (!_ledger.TryClaimQr(ledgerId, part.PartCode, qr))
                            return Abort("QR " + qr + " đã được giao trước đó.");
                }

                int closed = _pending.ClosePending(command);
                if (closed != command.PendingStt.Count)
                    return Abort("Đóng chờ giao không đủ: " + closed + "/" + command.PendingStt.Count +
                                 " STT; huỷ toàn bộ command (F-Y4).");

                _uow.Commit();
                return DeliveryStockCommandResult.Ok(command.TotalQuantity);
            }
            catch (Exception ex)
            {
                TryRollback();
                return DeliveryStockCommandResult.Fail("Lỗi giao hàng, đã rollback: " + ex.Message);
            }
        }

        private DeliveryStockCommandResult Abort(string message)
        {
            _uow.Rollback();
            return DeliveryStockCommandResult.Fail(message);
        }

        private void TryRollback()
        {
            try { _uow.Rollback(); } catch { /* original failure is reported */ }
        }
    }
}
