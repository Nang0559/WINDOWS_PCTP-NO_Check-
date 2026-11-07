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
            string customerName,
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
                customerName,
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

            IReadOnlyList<DeliveryTraceRow> rows = string.IsNullOrWhiteSpace(qrCode)
                ? await _repository.FindByDeliveryKeyAsync(deliveryKey, cancellationToken)
                : await _repository.FindByQrAsync(qrCode, cancellationToken);

            string requestedDeliveryKey = deliveryKey.Trim();
            DeliveryTraceRow source = (rows ?? new List<DeliveryTraceRow>())
                .FirstOrDefault(x => x != null && string.Equals(
                    x.DeliveryKey, requestedDeliveryKey, StringComparison.OrdinalIgnoreCase));

            if (source == null || string.IsNullOrWhiteSpace(source.LotNoRaw))
                return new List<DeliveryLotTraceRow>();

            // LUUPHIEUGIAOHANG.LOT is one delivery-row allocation list. The QR join
            // can return N rows for N scans, but must not multiply the same LOT allocations.
            // QR is only attached as the currently selected carton context; it is not
            // treated as a proven per-LOT foreign key.
            var result = new List<DeliveryLotTraceRow>();
            try
            {
                foreach (var lot in LotCodeHelper.ParseCompositeLot(source.LotNoRaw))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(new DeliveryLotTraceRow
                    {
                        DeliveryKey = source.DeliveryKey,
                        QRCode = string.IsNullOrWhiteSpace(qrCode) ? string.Empty : qrCode.Trim(),
                        LotNo = lot.Key,
                        Quantity = lot.Value
                    });
                }
            }
            catch (FormatException)
            {
                // Invalid composite allocations are not silently converted into a
                // single LOT with an invented quantity. The caller can show the raw
                // delivery LOT as an unresolved evidence issue.
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
                throw new ArgumentException("Ngày bắt đầu không được lớn hơn ngày kết thúc.");
        }
    }
}
