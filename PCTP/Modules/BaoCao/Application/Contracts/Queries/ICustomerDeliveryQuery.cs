using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public interface ICustomerDeliveryQuery
    {
        Task<IReadOnlyList<DeliveryTraceRow>> SearchAsync(
            string customerName,
            string partNo,
            System.DateTime? from,
            System.DateTime? to,
            CancellationToken cancellationToken);
    }
}
