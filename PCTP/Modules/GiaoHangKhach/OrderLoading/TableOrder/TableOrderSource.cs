
using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading
{
    /// <summary>Wrap OrderTableLoadStrategy — bắt buộc có Difference (roadmap mục 2.2).</summary>
    public class TableOrderSource : IOrderSource
    {
        private readonly OrderTableLoadStrategy _strategy;

        public TableOrderSource(OrderTableLoadStrategy strategy) => _strategy = strategy;

        public OrderSourceKind SourceKind => OrderSourceKind.TableOrder;

        public OrderSourceResult Load(OrderLoadContext context)
        {
            var donHang = _strategy.LoadDonHangGoc(context);
            _strategy.MergeLotDaLuu(donHang, context);   // no-op — đã merge nội bộ trong LoadDonHangGoc
            var diff = _strategy.SoSanhVoiIFS(donHang, context);   // BẮT BUỘC có ý nghĩa ở nguồn này

            return new OrderSourceResult
            {
                Orders = donHang,
                Difference = diff,
                SourceKind = SourceKind,
                Warning = context.IfsLoadError
            };
        }
    }
}