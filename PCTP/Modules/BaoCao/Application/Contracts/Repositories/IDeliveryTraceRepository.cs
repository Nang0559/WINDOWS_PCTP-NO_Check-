using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCTP.Modules.BaoCao.Application.Contracts.Queries;

namespace PCTP.Modules.BaoCao.Application.Contracts.Repositories
{
    /// <summary>
    /// Read-only persistence boundary for delivery traceability.
    /// SQL/DataTable details stay in Infrastructure.
    /// </summary>
    public interface IDeliveryTraceRepository
    {
        Task<IReadOnlyList<string>> GetItemCodesAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> GetCustomersAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<DeliveryTraceRow>> SearchQrAsync(
            string qrCode,
            string customerLabelData,
            string partNo,
            string customerName,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<DeliveryTraceRow>> SearchLotAsync(
            string lotNo,
            string partNo,
            string customerName,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<DeliveryTraceRow>> SearchCustomerAsync(
            string customerName,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<DeliveryTraceRow>> FindByQrAsync(
            string qrCode,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<DeliveryTraceRow>> FindByDeliveryKeyAsync(
            string deliveryKey,
            CancellationToken cancellationToken);
    }
}
