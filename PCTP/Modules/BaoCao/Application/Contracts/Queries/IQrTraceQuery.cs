using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCTP.Modules.BaoCao.Application.Contracts.Queries
{
    public interface IQrTraceQuery
    {
        Task<IReadOnlyList<DeliveryTraceRow>> SearchAsync(
            string qrCode,
            string customerLabelData,
            string partNo,
            string customerName,
            System.DateTime? from,
            System.DateTime? to,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<DeliveryLotTraceRow>> GetLotsAsync(
            string deliveryKey,
            string qrCode,
            CancellationToken cancellationToken);
    }
}
