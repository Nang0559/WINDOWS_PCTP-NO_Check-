using System;
using PCTP.Shared.UiMd;

namespace PCTP.Framework.Documents
{
    /// <summary>Bridges the document runtime to the existing shared workflow engine (no parallel transition rules).</summary>
    public sealed class WorkflowTransitionPolicyAdapter : IDocumentTransitionPolicy
    {
        private readonly IWorkflowTransitionService _transitions;

        public WorkflowTransitionPolicyAdapter(IWorkflowTransitionService transitions)
        {
            if (transitions == null) throw new ArgumentNullException("transitions");
            _transitions = transitions;
        }

        public void EnsureCanTransition(string workflowCode, int fromStatus, int toStatus)
        {
            _transitions.EnsureCanTransition(workflowCode, fromStatus, toStatus);
        }
    }
}
