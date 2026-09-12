using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Applications.Services
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
