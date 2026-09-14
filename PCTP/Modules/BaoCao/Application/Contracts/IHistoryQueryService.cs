using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts
{
    /// <summary>
    /// Read-only query contract for cross-module item traceability/history.
    /// The reporting module must not own transaction rules of business modules.
    /// </summary>
    public interface IHistoryQueryService
    {
        Task<IReadOnlyList<ItemHistoryRow>> SearchAsync(
            HistorySearchCriteria criteria,
            CancellationToken cancellationToken);
    }
}
