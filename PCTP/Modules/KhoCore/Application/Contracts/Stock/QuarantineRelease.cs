using System;

namespace PCTP.Modules.KhoCore.Application.Contracts.Stock
{
    public sealed class QuarantineReleaseRequest
    {
        public int SlotLotId { get; set; }
        public bool QcDispositionApproved { get; set; }
        public string PerformedBy { get; set; }
        public string ReferenceId { get; set; }
        public string Reason { get; set; }
    }

    public sealed class QuarantineReleaseResult
    {
        public bool Success { get; private set; }
        public bool Unauthorized { get; private set; }
        public string Message { get; private set; }
        public static QuarantineReleaseResult Ok(string message) => new QuarantineReleaseResult { Success = true, Message = message };
        public static QuarantineReleaseResult Fail(string message, bool unauthorized = false) => new QuarantineReleaseResult { Message = message, Unauthorized = unauthorized };
    }

    public interface IQuarantineReleaseAuthorization
    {
        bool CanRelease(string performedBy);
    }
}
