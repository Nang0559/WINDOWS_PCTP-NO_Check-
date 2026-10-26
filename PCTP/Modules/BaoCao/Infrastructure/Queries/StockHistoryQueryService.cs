using PCTP.Modules.BaoCao.Application.Contracts;
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
    /// Application-facing adapter for stock history queries.
    /// Database access is delegated entirely to IStockHistoryRepository.
    /// </summary>
    public sealed class StockHistoryQueryService : IStockHistoryQuery
    {
        private readonly IStockHistoryRepository _repository;

        public StockHistoryQueryService()
            : this(new StockHistoryRepository())
        {
        }

        public StockHistoryQueryService(IStockHistoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException("repository");
        }

        public Task<IReadOnlyList<string>> GetItemCodesAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetItemCodesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<ItemHistoryRow>> SearchAsync(
            HistorySearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (criteria == null)
                throw new ArgumentNullException("criteria");

            if (criteria.From.HasValue && criteria.To.HasValue &&
                criteria.From.Value.Date > criteria.To.Value.Date)
            {
                throw new ArgumentException(
                    "Ngày bắt đầu không được lớn hơn ngày kết thúc.");
            }

            return _repository.SearchAsync(criteria, cancellationToken);
        }
    }
}
