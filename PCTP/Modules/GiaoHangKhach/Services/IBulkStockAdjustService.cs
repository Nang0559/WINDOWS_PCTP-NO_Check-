namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Điều chỉnh kho ảo A0 khi hàng đã nhập vào A0 sau đó được xuất đi qua
    /// luồng CNK thông thường. Physical stock mutation phải đi qua
    /// IStockMovementService — interface này chỉ expose thao tác nghiệp vụ
    /// mức cao cho các repository/service khác gọi vào.
    /// Implementation: <see cref="BulkStockAdjustService"/>.
    /// </summary>
    public interface IBulkStockAdjustService
    {
        /// <summary>
        /// Tự động trừ số lượng xuất khỏi Slot ảo A0 theo LOT.
        /// Trả về false nếu không tìm được Lot khớp hoặc số lượng &lt;= 0.
        /// </summary>
        bool TruKhoAoTheoLot(string lotNo, int slXuat);
    }
}