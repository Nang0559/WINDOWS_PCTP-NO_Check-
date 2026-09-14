using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    /// <summary>
    /// Read-only stock movement/history query. No write-side dependency.
    /// </summary>
    public interface IStockHistoryQuery
    {
        Task<IReadOnlyList<ItemHistoryRow>> SearchAsync(
            HistorySearchCriteria criteria,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> GetItemCodesAsync(
            CancellationToken cancellationToken);
    }
}
