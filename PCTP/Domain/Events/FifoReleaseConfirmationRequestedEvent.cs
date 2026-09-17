using PCTP.Modules.GiaoHangKhach;
using System.Collections.Generic;

namespace PCTP.Domain.Events
{
    /// <summary>
    /// Raised before an authoritative FIFO release at CNK.
    /// The UI decides whether the invalid QR rows may be released.
    /// No database mutation is performed by this event itself.
    /// </summary>
    public sealed class FifoReleaseConfirmationRequestedEvent
    {
        public IReadOnlyList<FifoViolation> Violations { get; }
        public bool Confirmed { get; set; }

        public FifoReleaseConfirmationRequestedEvent(IReadOnlyList<FifoViolation> violations)
        {
            Violations = violations ?? new List<FifoViolation>();
        }
    }
}
