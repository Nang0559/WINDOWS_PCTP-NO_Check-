using System;
using System.Collections.Generic;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Shared workflow-transition gate. It never decides transitions itself: it reads the current status,
    /// takes the target status from the trusted command definition and asks the transition policy.
    /// Anything it cannot determine (no workflow, no target, no status field) throws, i.e. fails closed.
    /// </summary>
    public sealed class DocumentWorkflowGate : IDocumentWorkflowGate
    {
        private readonly IDocumentTransitionPolicy _policy;
        private readonly IDocumentStatusReader _reader;

        public DocumentWorkflowGate(IDocumentTransitionPolicy policy, IDocumentStatusReader reader)
        {
            if (policy == null) throw new ArgumentNullException("policy");
            if (reader == null) throw new ArgumentNullException("reader");
            _policy = policy;
            _reader = reader;
        }

        public void Demand(DocumentFormDefinition document, DocumentCommandDefinition command, string siteCode,
            IDictionary<string, object> input)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (command == null) throw new ArgumentNullException("command");
            if (string.Equals(document.WorkflowCode, DocumentFormDefinition.NoWorkflow, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Command " + command.CommandCode +
                    " requires a workflow transition but document " + document.ModuleCode + " has no workflow.");
            if (!command.WorkflowTargetStatus.HasValue)
                throw new InvalidOperationException("Command " + command.CommandCode + " has no registered target status.");
            if (document.StatusField == null)
                throw new InvalidOperationException("Document " + document.ModuleCode + " has no registered status field.");

            int current = _reader.ReadStatus(document, command, siteCode, input);
            _policy.EnsureCanTransition(document.WorkflowCode, current, command.WorkflowTargetStatus.Value);
        }
    }
}
