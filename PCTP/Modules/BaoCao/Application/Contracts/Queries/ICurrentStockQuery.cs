using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    /// <summary>
    /// Read-only current stock query.
    /// </summary>
    public interface ICurrentStockQuery
    {
        Task<IReadOnlyList<CurrentStockRow>> GetAsync(CancellationToken cancellationToken);
    }
}
