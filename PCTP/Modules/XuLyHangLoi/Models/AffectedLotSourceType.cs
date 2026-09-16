namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Nguồn tồn/nguồn hàng được snapshot khi truy vết LOT bị ảnh hưởng.
    /// Đây là nguồn nghiệp vụ, không phải trạng thái xử lý.
    /// </summary>
    public enum AffectedLotSourceType
    {
        Kho = 1,
        SanXuat = 2,
        KhachTra = 3
    }
}
