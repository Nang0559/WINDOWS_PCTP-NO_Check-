using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;

namespace PCTP.Modules.BaoCao.Application.Contracts.Repositories
{
    public interface ICurrentStockRepository
    {
        Task<IReadOnlyList<CurrentStockRow>> GetAsync(
            CancellationToken cancellationToken);
    }
}
