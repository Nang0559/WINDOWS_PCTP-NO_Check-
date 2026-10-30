namespace PCTP.Modules.NhapKho
{
    /// <summary>
    /// ĐIỀU KIỆN LỌC DUY NHẤT cho "Phiếu SX chờ nhập" (badge, grid bước 1 và worklist của form quét).
    /// Trước đây 3 câu SQL tự lặp điều kiện nên dễ lệch nhau.
    ///
    /// Một dòng được coi là CHỜ NHẬP khi còn thiếu hàng (SL_DA_NHAP &lt; SL_DA_SAN_XUAT) và:
    ///   - LOT chưa kết thúc (KET_THUC_LOT = 0), HOẶC
    ///   - LOT đã kết thúc nhưng kế hoạch sản xuất đã TĂNG so với lúc kho nhập
    ///     (SL_DA_SAN_XUAT từ MES khác STOCKTP.SLSX đã lưu) → cho phép "Mở lại Lot".
    /// Lot đóng thủ công khi thiếu hàng và SLSX không đổi thì vẫn KHÔNG hiện lại.
    /// SLSX_DA_LUU = 0 (dữ liệu cũ chưa có SLSX) bị loại để không bật lại hàng loạt lot cũ.
    /// </summary>
    internal static class ReceivingWorklistFilter
    {
        public const string WhereClause = @"
WHERE ISNULL(SL_DA_NHAP,0) < ISNULL(SL_DA_SAN_XUAT,0)
  AND (
        ISNULL(KET_THUC_LOT,0) = 0
        OR (ISNULL(SLSX_DA_LUU,0) > 0
            AND ISNULL(SLSX_DA_LUU,0) <> ISNULL(SL_DA_SAN_XUAT,0))
      )";
    }
}
