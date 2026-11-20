using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Merges validated SQL metadata over the trusted definition. Missing metadata falls back to
    /// safe code defaults (visible, read-only, captioned by code); metadata can only restrict.
    /// </summary>
    public static class DocumentLayoutResolver
    {
        public static ResolvedDocument Resolve(DocumentFormDefinition trusted, DocumentMetadata metadata)
        {
            if (trusted == null) throw new ArgumentNullException("trusted");
            if (metadata != null) DocumentDefinitionValidator.DemandMatchesRegistry(trusted, metadata);

            var layouts = metadata == null ? new List<DocumentLayoutMetadata>() : metadata.Layouts;
            var header = layouts.FirstOrDefault(l => string.Equals(l.LayoutCode, "HEADER", StringComparison.OrdinalIgnoreCase));
            var detail = layouts.FirstOrDefault(l => string.Equals(l.LayoutCode, "DETAIL", StringComparison.OrdinalIgnoreCase));

            return new ResolvedDocument
            {
                Definition = trusted,
                DisplayName = metadata != null && !string.IsNullOrWhiteSpace(metadata.DisplayName)
                    ? metadata.DisplayName : trusted.ModuleCode,
                HeaderCaption = header != null && !string.IsNullOrWhiteSpace(header.Caption) ? header.Caption : "Thông tin chung",
                DetailCaption = detail != null && !string.IsNullOrWhiteSpace(detail.Caption) ? detail.Caption : "Chi tiết",
                HeaderVisible = header == null || header.IsVisible,
                DetailVisible = detail == null || detail.IsVisible,
                HeaderFields = ResolveFields(trusted, metadata, DocumentRegion.Header),
                DetailFields = ResolveFields(trusted, metadata, DocumentRegion.Detail),
                Commands = ResolveCommands(trusted, metadata)
            };
        }

        private static IReadOnlyList<ResolvedField> ResolveFields(DocumentFormDefinition trusted,
            DocumentMetadata metadata, DocumentRegion region)
        {
            var result = new List<ResolvedField>();
            int order = 0;
            foreach (var definition in trusted.FieldsOf(region))
            {
                var configured = metadata == null ? null : metadata.Fields.FirstOrDefault(f =>
                    string.Equals(f.FieldCode, definition.FieldCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.RegionCode, DocumentEditorCodes.ToRegionCode(region), StringComparison.OrdinalIgnoreCase));
                var field = new ResolvedField
                {
                    Definition = definition,
                    Caption = definition.FieldCode,
                    SequenceNo = 10000 + order,
                    Editor = definition.Type,
                    IsVisible = true,
                    IsReadOnly = !definition.IsEditable
                };
                if (configured != null)
                {
                    DocumentFieldType editor;
                    if (!DocumentEditorCodes.TryParse(configured.EditorCode, out editor)) editor = definition.Type;
                    field.Caption = string.IsNullOrWhiteSpace(configured.Caption) ? definition.FieldCode : configured.Caption;
                    field.SequenceNo = configured.SequenceNo;
                    field.Width = configured.Width;
                    field.DisplayFormat = string.IsNullOrWhiteSpace(configured.DisplayFormat) ? null : configured.DisplayFormat;
                    field.Editor = editor;
                    field.IsVisible = configured.IsVisible;
                    field.IsReadOnly = configured.IsReadOnly || !definition.IsEditable; // restrict-only
                    field.IsRequired = configured.IsRequired;
                    field.LookupCode = string.IsNullOrWhiteSpace(configured.LookupCode) ? null : configured.LookupCode;
                }
                result.Add(field);
                order++;
            }
            return new ReadOnlyCollection<ResolvedField>(result.OrderBy(f => f.SequenceNo).ToList());
        }

        private static IReadOnlyList<ResolvedCommand> ResolveCommands(DocumentFormDefinition trusted, DocumentMetadata metadata)
        {
            var result = new List<ResolvedCommand>();
            int order = 0;
            foreach (var definition in trusted.Commands)
            {
                var configured = metadata == null ? null : metadata.Commands.FirstOrDefault(c =>
                    string.Equals(c.CommandCode, definition.CommandCode, StringComparison.OrdinalIgnoreCase));
                result.Add(new ResolvedCommand
                {
                    Definition = definition,
                    Caption = configured != null && !string.IsNullOrWhiteSpace(configured.Caption)
                        ? configured.Caption : definition.CommandCode,
                    SequenceNo = configured != null ? configured.SequenceNo : 10000 + order,
                    IsVisible = configured == null || configured.IsVisible,
                    RequiredStatusCode = configured == null ? null : configured.RequiredStatusCode
                });
                order++;
            }
            return new ReadOnlyCollection<ResolvedCommand>(result.OrderBy(c => c.SequenceNo).ToList());
        }
    }
}
