using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.UiMd
{
    public interface IWorkflowTransitionService
    {
        bool CanTransition(string processCode, int fromStatus, int toStatus);
        WorkflowTransition GetTransition(string processCode, int fromStatus, int toStatus);
        IReadOnlyList<WorkflowTransition> GetAvailableTransitions(string processCode, int fromStatus);
        void EnsureCanTransition(string processCode, int fromStatus, int toStatus); // throw nếu sai
    }
}
