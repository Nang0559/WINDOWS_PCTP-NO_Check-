namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    /// <summary>
    /// Cổng tồn kho trên MES ledger (MES.FVN_InventoryBalance / FVN_InventoryTransaction).
    /// Không có API "cộng/trừ SLCONLAI": mọi thay đổi tồn phải là một <see cref="StockLedgerEntry"/>
    /// đi qua dbo.usp_FVN_PCTP_PostStockMovement. Implementation tham gia UnitOfWork của caller.
    /// </summary>
    public interface IStockBalanceRepository
    {
        /// <summary>SiteCode đang dùng cho mọi thao tác của repository này.</summary>
        string SiteCode { get; }

        /// <summary>
        /// Tìm MaterialLotId theo (SiteCode, itemCode, lotNo). createIfMissing=true tạo LOT FG mới
        /// (chỉ dùng ở nhập kho / điều chỉnh tăng). Trả null nếu không có và không được tạo.
        /// </summary>
        StockLotKey ResolveLot(string itemCode, string lotNo, bool createIfMissing);

        /// <summary>Tồn AVAILABLE (OnHand - Reserved) của LOT trên toàn site.</summary>
        decimal GetAvailableQuantity(StockLotKey lot);

        /// <summary>Tồn AVAILABLE của LOT tại location MES chính của Slot.</summary>
        decimal GetAvailableQuantity(StockLotKey lot, int slotId);

        /// <summary>Ghi một phát sinh vào ledger, trả InventoryTransactionId.</summary>
        long Post(StockLedgerEntry entry);
    }
}
