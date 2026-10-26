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
    /// Application-facing adapter for quality history queries.
    /// Database access is delegated entirely to IQualityHistoryRepository.
    /// </summary>
    public sealed class QualityHistoryQueryService : IQualityHistoryQuery
    {
        private readonly IQualityHistoryRepository _repository;

        public QualityHistoryQueryService()
            : this(new QualityHistoryRepository())
        {
        }

        public QualityHistoryQueryService(IQualityHistoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException("repository");
        }

        public Task<IReadOnlyList<QualityHistoryRow>> SearchAsync(
            DateTime from,
            DateTime to,
            string itemCode,
            string result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (from.Date > to.Date)
            {
                throw new ArgumentException(
                    "Ngày bắt đầu không được lớn hơn ngày kết thúc.");
            }

            return _repository.SearchAsync(
                from,
                to,
                itemCode,
                result,
                cancellationToken);
        }

        public Task<IReadOnlyList<QualityHistoryDetailRow>> GetDetailsAsync(
            string inspectionCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _repository.GetDetailsAsync(inspectionCode, cancellationToken);
        }
    }
}
