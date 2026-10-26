using PCTP.Modules.BaoCao.Application.Contracts.Queries;
using PCTP.Modules.BaoCao.Application.Contracts.Repositories;
using PCTP.Modules.BaoCao.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Infrastructure.Queries
{
    /// <summary>
    /// Application-facing adapter for the current-stock query.
    /// Database access is delegated entirely to ICurrentStockRepository.
    /// </summary>
    public sealed class CurrentStockQueryService : ICurrentStockQuery
    {
        private readonly ICurrentStockRepository _repository;

        public CurrentStockQueryService()
            : this(new CurrentStockRepository())
        {
        }

        public CurrentStockQueryService(ICurrentStockRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException("repository");
        }

        public Task<IReadOnlyList<CurrentStockRow>> GetAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _repository.GetAsync(cancellationToken);
        }
    }
}
