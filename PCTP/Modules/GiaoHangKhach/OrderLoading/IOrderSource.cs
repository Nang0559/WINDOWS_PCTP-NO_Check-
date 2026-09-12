using PCTP.Modules.GiaoHangKhach.Mode;
using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading
{
    /// <summary>
    /// Phase 3 — thay thế dần IOrderLoadStrategy. Source CHỈ load data
    /// (LoadDonHangGoc + MergeLotDaLuu + SoSanhVoiIFS gộp lại), KHÔNG làm
    /// SyncChoDocQR (đó là QR/TMP Working State — Phase 4, tách riêng).
    /// </summary>
    public interface IOrderSource
    {
        OrderSourceKind SourceKind { get; }
        OrderSourceResult Load(OrderLoadContext context);
    }
}