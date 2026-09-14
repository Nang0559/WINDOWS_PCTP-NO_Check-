using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCTP.Modules.BaoCao.Application.Contracts;

namespace PCTP.Modules.BaoCao.Application.Services
{
    /// <summary>
    /// Application service for read-only reporting/traceability use cases.
    /// Infrastructure implementation is intentionally injected later; this type
    /// must not access business-module repositories directly.
    /// </summary>
    public sealed class BaoCaoQueryService : IHistoryQueryService
    {
        private readonly IHistoryQueryService _reader;

        public BaoCaoQueryService(IHistoryQueryService reader)
        {
            _reader = reader;
        }

        public Task<IReadOnlyList<ItemHistoryRow>> SearchAsync(
            HistorySearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            return _reader.SearchAsync(criteria, cancellationToken);
        }
    }
}
