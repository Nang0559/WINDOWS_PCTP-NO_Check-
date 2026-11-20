using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Fail-closed check of mutable SQL metadata against the trusted C# definition. It accepts only
    /// plain metadata; there is no API that takes SQL text or an executable object name.
    /// </summary>
    public static class DocumentDefinitionValidator
    {
        private const string AllowedFormatPunctuation = " #,.:/-%";

        public static void DemandMatchesRegistry(DocumentFormDefinition trusted, DocumentMetadata metadata)
        {
            if (trusted == null) throw new ArgumentNullException("trusted");
            if (metadata == null) throw new ArgumentNullException("metadata");
            try
            {
                Validate(trusted, metadata);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("SQL document metadata is malformed: " + ex.Message, ex);
            }
        }

        private static void Validate(DocumentFormDefinition trusted, DocumentMetadata metadata)
        {
            string workflow = string.IsNullOrWhiteSpace(metadata.WorkflowCode)
                ? DocumentFormDefinition.NoWorkflow : metadata.WorkflowCode.Trim();
            if (!Same(trusted.ModuleCode, metadata.ModuleCode) ||
                !Same(trusted.HeaderView, DocumentIdentifiers.SqlObject(metadata.HeaderView, "HeaderView")) ||
                !Same(trusted.DetailView, DocumentIdentifiers.SqlObject(metadata.DetailView, "DetailView")) ||
                !Same(trusted.WorkflowCode, workflow))
                Fail("module, view or workflow binding does not match the trusted application registry");

            var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in metadata.Fields)
            {
                DocumentRegion region;
                if (!DocumentEditorCodes.TryParseRegion(field.RegionCode, out region))
                    Fail("unknown region '" + field.RegionCode + "' for field " + field.FieldCode);
                var registered = trusted.FindField(region, field.FieldCode);
                if (registered == null)
                    Fail("field '" + field.FieldCode + "' is not registered for " + region);
                if (!seenFields.Add(region + ":" + registered.FieldCode))
                    Fail("duplicate field metadata '" + field.FieldCode + "'");
                DocumentFieldType editor;
                if (!DocumentEditorCodes.TryParse(field.EditorCode, out editor) || !registered.AllowsEditor(editor))
                    Fail("editor '" + field.EditorCode + "' is not allowed for field " + field.FieldCode);
                if (field.Width.HasValue && (field.Width.Value < 20 || field.Width.Value > 2000))
                    Fail("width out of range for field " + field.FieldCode);
                if (!IsSafeDisplayFormat(field.DisplayFormat))
                    Fail("display format is not allowed for field " + field.FieldCode);
            }

            var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in metadata.Commands)
            {
                if (!trusted.AllowsCommand(command.CommandCode))
                    Fail("command '" + command.CommandCode + "' is not registered");
                if (!seenCommands.Add(command.CommandCode.Trim()))
                    Fail("duplicate command metadata '" + command.CommandCode + "'");
            }
            if (trusted.Commands.Any(c => !seenCommands.Contains(c.CommandCode)))
                Fail("registered command is missing from SQL metadata");

            var seenLayouts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layout in metadata.Layouts)
            {
                DocumentRegion region;
                if (!DocumentEditorCodes.TryParseRegion(layout.LayoutCode, out region))
                    Fail("unknown layout '" + layout.LayoutCode + "'");
                if (!seenLayouts.Add(layout.LayoutCode.Trim()))
                    Fail("duplicate layout '" + layout.LayoutCode + "'");
            }
        }

        /// <summary>Display formats are data, so only a small punctuation/letter/digit set is accepted.</summary>
        public static bool IsSafeDisplayFormat(string format)
        {
            if (string.IsNullOrEmpty(format)) return true;
            if (format.Length > 40) return false;
            return format.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || AllowedFormatPunctuation.IndexOf(c) >= 0);
        }

        private static bool Same(string a, string b)
        {
            return string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void Fail(string reason)
        {
            throw new InvalidOperationException("SQL document metadata rejected: " + reason + ".");
        }
    }
}
