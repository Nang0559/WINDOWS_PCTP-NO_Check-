
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.GiaoDB;
using PCTP.Modules.GiaoHangKhach.OrderLoading.IFS;
using System;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading
{
    /// <summary>
    /// Phase 3 — song song với OrderLoadStrategyFactory (chưa xoá, mục 27).
    /// Chọn IOrderSource theo ctx.Source, giống hệt logic switch của factory cũ,
    /// nhưng trả về OrderSourceResult thay vì DataTable rời rạc.
    /// </summary>
    public class OrderSourceFactory : IOrderSourceFactory
    {
        private readonly IfsOrderSource _ifsSource;
        private readonly TableOrderSource _tableOrderSource;
        private readonly GiaoDbOrderSource _giaoDbSource;

        public OrderSourceFactory(
            IfsOrderSource ifsSource,
            TableOrderSource tableOrderSource,
            GiaoDbOrderSource giaoDbSource)
        {
            _ifsSource = ifsSource;
            _tableOrderSource = tableOrderSource;
            _giaoDbSource = giaoDbSource;
        }

        public IOrderSource GetSource(OrderLoadContext ctx)
        {
            switch (ctx.Source)
            {
                case OrderSourceKind.IFS: return _ifsSource;
                case OrderSourceKind.TableOrder: return _tableOrderSource;
                case OrderSourceKind.GiaoDB: return _giaoDbSource;
                default: throw new ArgumentOutOfRangeException(nameof(ctx.Source));
            }
        }
    }
}