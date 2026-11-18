using System;

namespace PCTP.Modules.NhapKho.ComponentReceiving
{
    public sealed class ComponentReceivingService : IComponentReceivingService
    {
        private readonly IComponentReceivingRepository _repository;

        public ComponentReceivingService(IComponentReceivingRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public ComponentReceiptResult Receive(ComponentReceiptRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.SiteCode = (request.SiteCode ?? string.Empty).Trim().ToUpperInvariant();
            request.ReceiptNo = (request.ReceiptNo ?? string.Empty).Trim();
            request.QrPayload = (request.QrPayload ?? string.Empty).Trim();
            request.ReceivedBy = (request.ReceivedBy ?? string.Empty).Trim();
            request.IdempotencyKey = (request.IdempotencyKey ?? string.Empty).Trim();

            if (request.SiteCode.Length == 0) throw new ArgumentException("Chưa nhập Site.");
            if (request.ReceiptNo.Length == 0) throw new ArgumentException("Chưa nhập số phiếu nhận.");
            if (!request.QrPayload.StartsWith("FVN1|", StringComparison.OrdinalIgnoreCase)
                || request.QrPayload.Length <= 5)
                throw new ArgumentException("QR linh kiện không hợp lệ. Tem phải có định dạng FVN1|LOT.");
            if (request.Quantity <= 0) throw new ArgumentException("Số lượng nhận phải lớn hơn 0.");
            if (request.TargetSlotId <= 0) throw new ArgumentException("Slot đích không hợp lệ.");
            if (request.ReceivedBy.Length == 0) throw new ArgumentException("Chưa nhập người nhận.");
            if (request.IdempotencyKey.Length == 0) throw new ArgumentException("Thiếu khóa chống ghi nhận trùng.");

            return _repository.Post(request);
        }
    }
}