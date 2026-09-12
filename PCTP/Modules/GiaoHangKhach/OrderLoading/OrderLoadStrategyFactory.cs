using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.IFSORDER;
using PCTP.Modules.GiaoHangKhach.Mode;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.IFS;
using PCTP.Modules.GiaoHangKhach.TableOrderLoad;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading
{
    /// <summary>
    /// Chọn <see cref="IOrderLoadStrategy"/> phù hợp cho 1 lần load đơn hàng, dựa trên
    /// cấu hình khách hàng (<c>CustomerConfig</c>) chứ không hard-code theo tên khách
    /// (HVN/YMVN/HTN) như code cũ trong <c>PhieuService</c>. Xem WORKFLOW_GIAOHANGKHACH.md
    /// mục 3. LƯU Ý: factory này (và cả 2 strategy) hiện CHƯA được khởi tạo/inject ở
    /// composition root (<c>HVN_PGH.cs</c>) — <c>PhieuService</c> vẫn gọi thẳng
    /// <c>ITableOrderRepository</c>/<c>IIFSRepository</c> theo nhánh if/else cũ.
    /// </summary>
    public class OrderLoadStrategyFactory : IOrderLoadStrategyFactory
    {
        private readonly IfsOrderLoadStrategy _ifsStrategy;
        private readonly OrderTableLoadStrategy _orderTableStrategy;
        private IOrderLoadStrategy _orderLoadStrategy;

        public OrderLoadStrategyFactory(IfsOrderLoadStrategy ifs, OrderTableLoadStrategy orderTable)
        {
            _ifsStrategy = ifs;
            _orderTableStrategy = orderTable;
        }

        public IOrderLoadStrategy GetStrategy(OrderLoadContext ctx)
        {
            switch (ctx.Source)
            {
                case OrderSourceKind.IFS: return _ifsStrategy;
                case OrderSourceKind.MilkRun: return _orderTableStrategy;
                case OrderSourceKind.GiaoDB: return _giaoDbStrategy;
                default: throw new ArgumentOutOfRangeException(nameof(ctx.Source));
            }
        }
    }
}
