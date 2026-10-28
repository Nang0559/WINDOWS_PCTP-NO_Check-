using PCTP.Modules.GiaoHangKhach.Contracts.Delivery;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Durable BusinessKey / QR ledger for the delivery stock command (P7, P8).
    /// All members must run inside the transaction the handler already opened.
    /// </summary>
    public interface IDeliveryCommandLedger
    {
        /// <summary>
        /// Returns the committed quantity of a prior command with this BusinessKey,
        /// or null when none. Must take a key-range lock so two sessions serialize.
        /// </summary>
        int? FindCommittedQuantity(string businessKey);

        /// <summary>Inserts the ledger header; returns its id. Unique BusinessKey is the last guard.</summary>
        int Insert(DeliveryStockCommand command);

        /// <summary>Records a delivered QR; returns false when the QR was already delivered (P8).</summary>
        bool TryClaimQr(int ledgerId, string partCode, string qrKey);
    }

    /// <summary>
    /// Closes the TMPPHIEUGIAOHANG rows (pending delivery) for this command. Implementation needs
    /// the DBA-confirmed TMP schema, so it is intentionally a separate seam (not invented here).
    /// </summary>
    public interface IDeliveryPendingGate
    {
        /// <summary>Returns the number of pending rows actually closed.</summary>
        int ClosePending(DeliveryStockCommand command);
    }

    /// <summary>
    /// Work that the legacy path did right after the stock deduction (close HangChoGiao rows and write
    /// EXPORT_CONFIRMED_HVN history). Runs inside the handler's transaction; any exception rolls back.
    /// </summary>
    public interface IDeliveryPostStockStep
    {
        void Run(DeliveryStockCommand command);
    }
}
