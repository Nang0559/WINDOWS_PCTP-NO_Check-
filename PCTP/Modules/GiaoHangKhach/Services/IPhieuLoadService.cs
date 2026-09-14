using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Orchestrates the delivery-order loading pipeline.
    /// Không publish EventBus và không xử lý UI.
    /// </summary>
    public interface IPhieuLoadService
    {
        OrderLoadResult Load(OrderLoadContext context);
    }
}
