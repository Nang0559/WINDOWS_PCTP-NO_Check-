using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public interface IQualityHistoryQuery
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
