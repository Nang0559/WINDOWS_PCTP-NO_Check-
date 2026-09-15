using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;

namespace PCTP.Modules.BaoCao.Application.Contracts.Repositories
{
    public interface IQualityHistoryRepository
    {
        Task<IReadOnlyList<QualityHistoryRow>> SearchAsync(
            DateTime from,
            DateTime to,
            string itemCode,
            string result,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<QualityHistoryDetailRow>> GetDetailsAsync(
            string inspectionCode,
            CancellationToken cancellationToken);
    }
}
