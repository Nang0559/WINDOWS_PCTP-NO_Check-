using PCTP.Modules.GiaoHangKhach;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PCTP.Domain.Events
{
    /// <summary>
    /// UI confirmation request raised through the existing event bus before an
    /// authoritative FIFO release at CNK.
    ///
    /// This request does not mutate database state. The UI completes it through
    /// Confirm() or Cancel(); the CNK service then continues only after a
    /// positive decision.
    ///
    /// The type remains under Domain.Events only because the current IEventBus
    /// transports DomainEvent instances. It is not a business-state domain event.
    /// </summary>
    public sealed class FifoReleaseConfirmationRequestedEvent : DomainEvent
    {
        private readonly TaskCompletionSource<bool> _decision =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<FifoViolation> Violations { get; }

        public FifoReleaseConfirmationRequestedEvent(IReadOnlyList<FifoViolation> violations)
        {
            Violations = violations ?? new List<FifoViolation>();
        }

        public void Confirm()
        {
            _decision.TrySetResult(true);
        }

        public void Cancel()
        {
            _decision.TrySetResult(false);
        }

        public bool WaitForDecision()
        {
            return _decision.Task.GetAwaiter().GetResult();
        }
    }
}
