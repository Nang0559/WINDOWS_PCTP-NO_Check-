using PCTP.Modules.BaoCao.Application.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Repositories
{
    public interface IDeliveryDetailQuery
    {
        /// <summary>Lịch sử vào/ra slot của 1 LOT — StockHistory.</summary>
        Task<IReadOnlyList<SlotMovementRow>> GetSlotHistoryAsync(
            string lotNo, CancellationToken cancellationToken);

        /// <summary>Hàng đã pick, đang chờ giao cho 1 phiếu (STT của LUUPHIEUGIAOHANG) — FVN_HangChoGiao.</summary>
        Task<IReadOnlyList<HangChoGiaoRow>> GetChoGiaoAsync(
            int stt, CancellationToken cancellationToken);
    }
}
