using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Layer 1 - trusted, code-registered document contract. It is the allow-list for modules, read
    /// views, columns, commands, procedures and workflow binding. SQL metadata (layer 2) may change
    /// presentation only; SQL procedures (layer 3) still enforce RBAC, scope, state and audit.
    /// </summary>
    public sealed class DocumentFormDefinition
    {
        /// <summary>Workflow code for documents whose state machine lives entirely in SQL status checks.</summary>
        public const string NoWorkflow = "NONE";

        public string ModuleCode { get; private set; }
        public string HeaderView { get; private set; }
        public string DetailView { get; private set; }
        public string WorkflowCode { get; private set; }
        /// <summary>Function code required (VIEW + SITE scope) to read the document views.</summary>
        public string ViewFunctionCode { get; private set; }
        /// <summary>Column present in both views that links a detail row to its header.</summary>
        public string KeyField { get; private set; }
        /// <summary>Column present in both views used for the Site data-scope filter.</summary>
        public string SiteField { get; private set; }
        public IReadOnlyList<DocumentFieldDefinition> Fields { get; private set; }
        public IReadOnlyCollection<DocumentCommandDefinition> Commands { get; private set; }

        public DocumentFormDefinition(string moduleCode, string headerView, string detailView,
            string workflowCode, string viewFunctionCode, string keyField, string siteField,
            IEnumerable<DocumentFieldDefinition> fields, IEnumerable<DocumentCommandDefinition> commands)
        {
            ModuleCode = DocumentIdentifiers.Code(moduleCode, "ModuleCode");
            HeaderView = DocumentIdentifiers.SqlObject(headerView, "HeaderView");
            DetailView = DocumentIdentifiers.SqlObject(detailView, "DetailView");
            WorkflowCode = string.IsNullOrWhiteSpace(workflowCode)
                ? NoWorkflow : DocumentIdentifiers.Code(workflowCode, "WorkflowCode");
            ViewFunctionCode = DocumentIdentifiers.FunctionCode(viewFunctionCode, "ViewFunctionCode");
            KeyField = DocumentIdentifiers.Identifier(keyField, "KeyField");
            SiteField = DocumentIdentifiers.Identifier(siteField, "SiteField");

            var fieldItems = (fields ?? Enumerable.Empty<DocumentFieldDefinition>()).ToList();
            if (fieldItems.Any(f => f == null)) throw new ArgumentException("Null field definition.", "fields");
            if (fieldItems.GroupBy(f => f.Region + ":" + f.FieldCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new ArgumentException("Duplicate field in document definition.", "fields");
            foreach (var region in new[] { DocumentRegion.Header, DocumentRegion.Detail })
            {
                foreach (var required in new[] { KeyField, SiteField })
                {
                    if (!fieldItems.Any(f => f.Region == region &&
                            string.Equals(f.FieldCode, required, StringComparison.OrdinalIgnoreCase)))
                        throw new ArgumentException("Field '" + required + "' must be registered in the " + region + " region.", "fields");
                }
            }
            Fields = new ReadOnlyCollection<DocumentFieldDefinition>(fieldItems);

            var items = (commands ?? Enumerable.Empty<DocumentCommandDefinition>()).ToList();
            if (items.Count == 0)
                throw new ArgumentException("At least one command must be registered.", "commands");
            if (items.Any(c => c == null)) throw new ArgumentException("Null command definition.", "commands");
            if (items.GroupBy(x => x.CommandCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new ArgumentException("Duplicate command code in document definition.", "commands");
            Commands = new ReadOnlyCollection<DocumentCommandDefinition>(items);
        }

        public IEnumerable<DocumentFieldDefinition> FieldsOf(DocumentRegion region)
        {
            return Fields.Where(f => f.Region == region);
        }

        public DocumentFieldDefinition FindField(DocumentRegion region, string fieldCode)
        {
            return Fields.FirstOrDefault(f => f.Region == region &&
                string.Equals(f.FieldCode, fieldCode, StringComparison.OrdinalIgnoreCase));
        }

        public bool AllowsCommand(string commandCode)
        {
            return FindCommand(commandCode) != null;
        }

        public DocumentCommandDefinition FindCommand(string commandCode)
        {
            return string.IsNullOrWhiteSpace(commandCode) ? null : Commands.FirstOrDefault(x =>
                string.Equals(x.CommandCode, commandCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public DocumentCommandDefinition GetCommand(string commandCode)
        {
            var command = FindCommand(commandCode);
            if (command == null)
                throw new InvalidOperationException("Command is not registered for document " + ModuleCode + ": " + commandCode);
            return command;
        }
    }
}
