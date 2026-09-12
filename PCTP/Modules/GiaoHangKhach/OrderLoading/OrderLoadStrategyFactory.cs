using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.IFSORDER;
using PCTP.Modules.GiaoHangKhach.TableOrderLoad;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Services
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

        public OrderLoadStrategyFactory(IfsOrderLoadStrategy ifs, OrderTableLoadStrategy orderTable)
        {
            _ifsStrategy = ifs;
            _orderTableStrategy = orderTable;
        }

        public IOrderLoadStrategy GetStrategy(OrderLoadContext ctx)
        {
            var cfg = ctx.Cfg;

            // Trường hợp lai (HVN): mặc định IFS, TRỪ KHI đang ở chế độ giao đặc biệt
            // và config có khai báo bảng giao đặc biệt.
            if (ctx.CheDoGiaoDacBiet && cfg.CoGiaoDacBiet)
                return _orderTableStrategy; // dùng OrderTableLoadStrategy nhưng trỏ tới OrderTableGiaoDacBiet

            // Trường hợp thuần bảng riêng (YMVN/HTN)
            if (cfg.LoadTuBangRieng)
                return _orderTableStrategy;

            // Mặc định: IFS (HVN luồng thường)
            return _ifsStrategy;
        }
    }
}
