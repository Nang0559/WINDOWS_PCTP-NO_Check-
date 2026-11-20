using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Resolves a command only through the trusted definition, authorizes it, builds the declared
    /// parameter list and hands it to a fixed procedure binding. Business enforcement (state,
    /// concurrency, audit) remains inside the SQL procedure; this class never replaces it.
    /// </summary>
    public sealed class DocumentCommandDispatcher
    {
        private readonly IDocumentRegistry _registry;
        private readonly IDocumentAuthorizer _authorizer;
        private readonly IDocumentCommandExecutor _executor;
        private readonly IDocumentWorkflowGate _workflowGate;

        public DocumentCommandDispatcher(IDocumentRegistry registry, IDocumentAuthorizer authorizer,
            IDocumentCommandExecutor executor, IDocumentWorkflowGate workflowGate = null)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (authorizer == null) throw new ArgumentNullException("authorizer");
            if (executor == null) throw new ArgumentNullException("executor");
            _registry = registry;
            _authorizer = authorizer;
            _executor = executor;
            _workflowGate = workflowGate;
        }

        public DataTable Execute(string moduleCode, string commandCode, string siteCode,
            IDictionary<string, object> input)
        {
            if (string.IsNullOrWhiteSpace(siteCode)) throw new ArgumentException("SiteCode là bắt buộc.", "siteCode");
            siteCode = siteCode.Trim();
            var document = _registry.GetRequired(moduleCode);
            var command = document.GetCommand(commandCode);
            var supplied = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (input != null) foreach (var pair in input) supplied[pair.Key] = pair.Value;

            _authorizer.DemandCommand(document, command, siteCode);

            if (command.RequiresWorkflowTransition)
            {
                if (_workflowGate == null)
                    throw new InvalidOperationException("Command " + command.CommandCode +
                        " requires a workflow transition but no workflow gate is configured.");
                _workflowGate.Demand(document, command, siteCode, supplied);
            }

            var declaredInputs = new HashSet<string>(command.Parameters
                .Where(p => p.Source == DocumentParameterSource.Input).Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var unknown = supplied.Keys.FirstOrDefault(k => !declaredInputs.Contains(k));
            if (unknown != null)
                throw new ArgumentException("Undeclared input '" + unknown + "' for command " + command.CommandCode + ".");

            var values = new List<KeyValuePair<DocumentParameterDefinition, object>>();
            foreach (var parameter in command.Parameters)
            {
                object value;
                switch (parameter.Source)
                {
                    case DocumentParameterSource.Actor:
                        value = _authorizer.ActorCode;
                        if (value == null || string.IsNullOrWhiteSpace(Convert.ToString(value)))
                            throw new ArgumentException("Thiếu người thao tác.");
                        break;
                    case DocumentParameterSource.Site:
                        value = siteCode;
                        break;
                    case DocumentParameterSource.IdempotencyKey:
                        value = Guid.NewGuid().ToString("N");
                        break;
                    default:
                        if (!supplied.TryGetValue(parameter.Name, out value) || value == null || value == DBNull.Value)
                            throw new ArgumentException("Thiếu tham số bắt buộc: " + parameter.Name + ".");
                        break;
                }
                values.Add(new KeyValuePair<DocumentParameterDefinition, object>(parameter, value));
            }
            return _executor.Execute(command, values);
        }
    }
}
