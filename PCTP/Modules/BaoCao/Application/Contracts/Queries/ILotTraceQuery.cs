using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public interface ILotTraceQuery
    {
        Task<IReadOnlyList<DeliveryTraceRow>> SearchAsync(
            string lotNo,
            string partNo,
            string customerName,
            System.DateTime? from,
            System.DateTime? to,
            CancellationToken cancellationToken);
    }
}
