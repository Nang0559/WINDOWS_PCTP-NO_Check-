using PCTP.Modules.GiaoHangKhach.Mode;
using PCTP.Modules.GiaoHangKhach.Models;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.GiaoDB
{
    /// <summary>Wrap GiaoDbOrderLoadStrategy — Difference luôn rỗng (roadmap mục 2.4: "không cần IFS").</summary>
    public class GiaoDbOrderSource : IOrderSource
    {
        private readonly GiaoDbOrderLoadStrategy _strategy;

        public GiaoDbOrderSource(GiaoDbOrderLoadStrategy strategy) => _strategy = strategy;

        public OrderSourceKind SourceKind => OrderSourceKind.GiaoDB;

        public OrderSourceResult Load(OrderLoadContext context)
        {
            var donHang = _strategy.LoadDonHangGoc(context);
            _strategy.MergeLotDaLuu(donHang, context);   // no-op
            var diff = _strategy.SoSanhVoiIFS(donHang, context);   // luôn rỗng

            return new OrderSourceResult
            {
                Orders = donHang,
                Difference = diff,
                SourceKind = SourceKind
            };
        }
    }
}