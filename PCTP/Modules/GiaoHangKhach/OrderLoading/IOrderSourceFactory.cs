using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading
{
    public interface IOrderSourceFactory
    {
        IOrderSource GetSource(OrderLoadContext ctx);
    }
}