using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCTP.Modules.BaoCao.Application.Contracts;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;

namespace PCTP.Modules.BaoCao.Application.Contracts.Repositories
{
    public interface IStockHistoryRepository
    {
        Task<IReadOnlyList<ItemHistoryRow>> SearchAsync(
            HistorySearchCriteria criteria,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> GetItemCodesAsync(
            CancellationToken cancellationToken);
    }
}
