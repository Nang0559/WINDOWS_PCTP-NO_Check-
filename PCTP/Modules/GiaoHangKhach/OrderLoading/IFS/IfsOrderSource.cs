using PCTP.Modules.GiaoHangKhach.Mode;
using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.IFS
{
    /// <summary>Wrap IfsOrderLoadStrategy — không viết lại logic, chỉ gom 3 bước thành 1 Load().</summary>
    public class IfsOrderSource : IOrderSource
    {
        private readonly IfsOrderLoadStrategy _strategy;

        public IfsOrderSource(IfsOrderLoadStrategy strategy) => _strategy = strategy;

        public OrderSourceKind SourceKind => OrderSourceKind.IFS;

        public OrderSourceResult Load(OrderLoadContext context)
        {
            var donHang = _strategy.LoadDonHangGoc(context);
            _strategy.MergeLotDaLuu(donHang, context);
            var diff = _strategy.SoSanhVoiIFS(donHang, context);   // rỗng — IFS không so sánh với chính nó

            return new OrderSourceResult
            {
                Orders = donHang,
                Difference = diff,
                SourceKind = SourceKind
            };
        }
    }
}