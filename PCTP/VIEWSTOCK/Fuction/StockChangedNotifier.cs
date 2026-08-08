using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.Fuction
{
    /// <summary>
    /// Cầu nối thông báo thay đổi kho ảo A0 giữa các form độc lập
    /// (HVN_PGH ghi DB → MainStockSV lắng nghe để vẽ lại Canvas).
    /// KHÔNG dùng IEventBus hiện có vì mỗi HVN_PGH tự new 1 InProcessEventBus
    /// riêng theo instance — không lan tới form khác được.
    /// </summary>
    public static class StockChangedNotifier
    {
        public static event Action StockChanged;

        public static void RaiseStockChanged()
            => StockChanged?.Invoke();
    }
}
