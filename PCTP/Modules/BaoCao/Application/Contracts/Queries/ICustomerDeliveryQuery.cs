using System;
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
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> GetItemCodesAsync(
            CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> GetCustomersAsync(
            CancellationToken cancellationToken);
    }
}
