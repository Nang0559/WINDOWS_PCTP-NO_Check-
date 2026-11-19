using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Trusted, code-registered document contract. SQL metadata may change presentation,
    /// but cannot introduce a new view, procedure, command or workflow at runtime.
    /// </summary>
    public sealed class DocumentFormDefinition
    {
        public string ModuleCode { get; private set; }
        public string HeaderView { get; private set; }
        public string DetailView { get; private set; }
        public string WorkflowCode { get; private set; }
        public IReadOnlyCollection<DocumentCommandDefinition> Commands { get; private set; }

        public DocumentFormDefinition(string moduleCode, string headerView, string detailView,
            string workflowCode, IEnumerable<DocumentCommandDefinition> commands)
        {
            ModuleCode = RequiredCode(moduleCode, "ModuleCode");
            HeaderView = RequiredSqlObject(headerView, "HeaderView");
            DetailView = RequiredSqlObject(detailView, "DetailView");
            WorkflowCode = RequiredCode(workflowCode, "WorkflowCode");

            var items = (commands ?? Enumerable.Empty<DocumentCommandDefinition>()).ToList();
            if (items.Count == 0)
                throw new ArgumentException("At least one command must be registered.", "commands");
            if (items.GroupBy(x => x.CommandCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new ArgumentException("Duplicate command code in document definition.", "commands");
            Commands = new ReadOnlyCollection<DocumentCommandDefinition>(items);
        }

        public bool AllowsCommand(string commandCode)
        {
            return Commands.Any(x => string.Equals(x.CommandCode, commandCode, StringComparison.OrdinalIgnoreCase));
        }

        internal static string RequiredCode(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
                throw new ArgumentException("Invalid registered code: " + name, name);
            return value.Trim().ToUpperInvariant();
        }

        internal static string RequiredSqlObject(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(name + " is required.", name);
            var parts = value.Split('.');
            if (parts.Length != 2 || parts.Any(p => p.Length == 0 ||
                p.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))))
                throw new ArgumentException("SQL object must be a two-part schema-qualified identifier.", name);
            return "[" + parts[0] + "].[" + parts[1] + "]";
        }
    }

    public sealed class DocumentCommandDefinition
    {
        public string CommandCode { get; private set; }
        public string PermissionCode { get; private set; }
        public string ProcedureName { get; private set; }
        public bool RequiresWorkflowTransition { get; private set; }

        public DocumentCommandDefinition(string commandCode, string permissionCode,
            string procedureName, bool requiresWorkflowTransition)
        {
            CommandCode = DocumentFormDefinition.RequiredCode(commandCode, "CommandCode");
            PermissionCode = DocumentFormDefinition.RequiredCode(permissionCode, "PermissionCode");
            ProcedureName = DocumentFormDefinition.RequiredSqlObject(procedureName, "ProcedureName");
            RequiresWorkflowTransition = requiresWorkflowTransition;
        }
    }

    /// <summary>Immutable allow-list populated only by application startup code.</summary>
    public interface IDocumentRegistry
    {
        DocumentFormDefinition GetRequired(string moduleCode);
        bool TryGet(string moduleCode, out DocumentFormDefinition definition);
    }

    public sealed class DocumentRegistry : IDocumentRegistry
    {
        private readonly IDictionary<string, DocumentFormDefinition> _definitions;

        public DocumentRegistry(IEnumerable<DocumentFormDefinition> definitions)
        {
            var map = new Dictionary<string, DocumentFormDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions ?? Enumerable.Empty<DocumentFormDefinition>())
            {
                if (definition == null) throw new ArgumentException("Null document definition.");
                if (map.ContainsKey(definition.ModuleCode))
                    throw new InvalidOperationException("Duplicate document module registration: " + definition.ModuleCode);
                map.Add(definition.ModuleCode, definition);
            }
            _definitions = new ReadOnlyDictionary<string, DocumentFormDefinition>(map);
        }

        public DocumentFormDefinition GetRequired(string moduleCode)
        {
            DocumentFormDefinition definition;
            if (!TryGet(moduleCode, out definition))
                throw new InvalidOperationException("Document module is not trusted/registered: " + moduleCode);
            return definition;
        }

        public bool TryGet(string moduleCode, out DocumentFormDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(moduleCode) &&
                _definitions.TryGetValue(moduleCode.Trim(), out definition);
        }
    }

    /// <summary>
    /// Validates mutable SQL presentation metadata against the trusted code contract.
    /// It deliberately has no API for accepting SQL text or executable object names.
    /// </summary>
    public static class DocumentMetadataGuard
    {
        public static void DemandMatchesRegistry(DocumentFormDefinition trusted,
            string configuredModuleCode, string configuredHeaderView,
            string configuredDetailView, string configuredWorkflowCode,
            IEnumerable<string> configuredCommands)
        {
            if (trusted == null) throw new ArgumentNullException("trusted");
            if (!string.Equals(trusted.ModuleCode, configuredModuleCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(trusted.HeaderView, DocumentFormDefinition.RequiredSqlObject(configuredHeaderView, "HeaderView"), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(trusted.DetailView, DocumentFormDefinition.RequiredSqlObject(configuredDetailView, "DetailView"), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(trusted.WorkflowCode, configuredWorkflowCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQL document metadata does not match the trusted application registry.");

            var commands = (configuredCommands ?? Enumerable.Empty<string>()).ToList();
            if (commands.Any(c => !trusted.AllowsCommand(c)) ||
                trusted.Commands.Any(c => !commands.Contains(c.CommandCode, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException("SQL command metadata does not match the trusted application registry.");
        }
    }
}
