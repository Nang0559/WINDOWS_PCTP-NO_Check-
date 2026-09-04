using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.UiMd
{
    public sealed class WorkflowTransitionService : IWorkflowTransitionService
    {
        private readonly IWorkflowRepository _repo;

        public WorkflowTransitionService(IWorkflowRepository repo)
        {
            _repo = repo;
        }

        public IReadOnlyList<WorkflowTransition> GetAvailableTransitions(string processCode, int fromStatus)
        {
            return _repo.GetTransitions(processCode)
                .Where(t => t.FromStatus == fromStatus)
                .OrderBy(t => t.ToStatus)
                .ToList();
        }

        public WorkflowTransition GetTransition(string processCode, int fromStatus, int toStatus)
        {
            return _repo.GetTransitions(processCode)
                .FirstOrDefault(t => t.FromStatus == fromStatus && t.ToStatus == toStatus);
        }

        public bool CanTransition(string processCode, int fromStatus, int toStatus)
        {
            return GetTransition(processCode, fromStatus, toStatus) != null;
        }

        public void EnsureCanTransition(string processCode, int fromStatus, int toStatus)
        {
            if (!CanTransition(processCode, fromStatus, toStatus))
            {
                throw new InvalidOperationException(
                    $"Không có transition hợp lệ: {processCode} {fromStatus} -> {toStatus}.");
            }
        }
    }
}
