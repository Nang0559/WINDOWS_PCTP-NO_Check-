using PCTP.Common;
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
    /// Application-facing read service for delivery traceability.
    /// SQL/DataTable details remain in the repository.
    /// </summary>
    public sealed class DeliveryTraceReadService
        : IQrTraceQuery, ILotTraceQuery, ICustomerDeliveryQuery
    {
        private readonly IDeliveryTraceRepository _repository;

        public DeliveryTraceReadService()
            : this(new DeliveryTraceRepository())
        {
        }

        public DeliveryTraceReadService(IDeliveryTraceRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException("repository");
        }

        public Task<IReadOnlyList<string>> GetItemCodesAsync(CancellationToken cancellationToken)
        {
            return _repository.GetItemCodesAsync(cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCustomersAsync(CancellationToken cancellationToken)
        {
            return _repository.GetCustomersAsync(cancellationToken);
        }

        public Task<IReadOnlyList<DeliveryTraceRow>> SearchAsync(
            string qrCode,
            string customerLabelData,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDateRange(from, to);

            return _repository.SearchQrAsync(
                qrCode,
                customerLabelData,
                partNo,
                from,
                to,
                cancellationToken);
        }

        public async Task<IReadOnlyList<DeliveryLotTraceRow>> GetLotsAsync(
            string deliveryKey,
            string qrCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(deliveryKey))
                return new List<DeliveryLotTraceRow>();

            IReadOnlyList<DeliveryTraceRow> rows = await _repository.FindByQrAsync(
                qrCode,
                cancellationToken);

            var result = new List<DeliveryLotTraceRow>();
            string requestedDeliveryKey = deliveryKey.Trim();

            foreach (DeliveryTraceRow delivery in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.Equals(
                    delivery.DeliveryKey,
                    requestedDeliveryKey,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    foreach (var lot in LotCodeHelper.ParseCompositeLot(delivery.LotNoRaw))
                    {
                        result.Add(new DeliveryLotTraceRow
                        {
                            DeliveryKey = delivery.DeliveryKey,
                            QRCode = delivery.QRCode,
                            LotNo = lot.Key,
                            Quantity = lot.Value
                        });
                    }
                }
                catch (FormatException)
                {
                    if (!string.IsNullOrWhiteSpace(delivery.LotNoRaw))
                    {
                        result.Add(new DeliveryLotTraceRow
                        {
                            DeliveryKey = delivery.DeliveryKey,
                            QRCode = delivery.QRCode,
                            LotNo = delivery.LotNoRaw.Trim(),
                            Quantity = delivery.Quantity ?? 0m
                        });
                    }
                }
            }

            return result;
        }

        Task<IReadOnlyList<DeliveryTraceRow>> ILotTraceQuery.SearchAsync(
            string lotNo,
            string partNo,
            string customerName,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDateRange(from, to);

            return _repository.SearchLotAsync(
                lotNo,
                partNo,
                customerName,
                from,
                to,
                cancellationToken);
        }

        Task<IReadOnlyList<DeliveryTraceRow>> ICustomerDeliveryQuery.SearchAsync(
            string customerName,
            string partNo,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDateRange(from, to);

            return _repository.SearchCustomerAsync(
                customerName,
                partNo,
                from,
                to,
                cancellationToken);
        }

        private static void ValidateDateRange(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                throw new ArgumentException(
                    "Ngày bắt đầu không được lớn hơn ngày kết thúc.");
            }
        }
    }
}
