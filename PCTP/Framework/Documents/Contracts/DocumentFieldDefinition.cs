using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PCTP.Framework.Documents
{
    public enum DocumentRegion { Header, Detail }

    public enum DocumentFieldType { Text, Number, Date, DateTime, Check, Lookup, Memo, Barcode, Qr }

    /// <summary>Maps the editor codes allowed by the SQL CHECK constraint to the trusted enum.</summary>
    public static class DocumentEditorCodes
    {
        public static string ToCode(DocumentFieldType type)
        {
            return type.ToString().ToUpperInvariant();
        }

        public static bool TryParse(string code, out DocumentFieldType type)
        {
            type = DocumentFieldType.Text;
            if (string.IsNullOrWhiteSpace(code)) return false;
            foreach (DocumentFieldType candidate in Enum.GetValues(typeof(DocumentFieldType)))
            {
                if (string.Equals(ToCode(candidate), code.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    type = candidate;
                    return true;
                }
            }
            return false;
        }

        public static string ToRegionCode(DocumentRegion region)
        {
            return region == DocumentRegion.Header ? "HEADER" : "DETAIL";
        }

        public static bool TryParseRegion(string code, out DocumentRegion region)
        {
            region = DocumentRegion.Header;
            if (string.Equals(code, "HEADER", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(code, "DETAIL", StringComparison.OrdinalIgnoreCase))
            {
                region = DocumentRegion.Detail;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A column the application trusts the document to expose. SQL metadata may re-caption, re-order,
    /// hide or tighten a registered field, but cannot add a field, widen editability or pick an
    /// editor outside <see cref="AllowedEditors"/>.
    /// </summary>
    public sealed class DocumentFieldDefinition
    {
        public string FieldCode { get; private set; }
        public DocumentRegion Region { get; private set; }
        public DocumentFieldType Type { get; private set; }
        /// <summary>Trusted ceiling. Metadata can only make a field read-only, never editable.</summary>
        public bool IsEditable { get; private set; }
        public IReadOnlyCollection<DocumentFieldType> AllowedEditors { get; private set; }

        public DocumentFieldDefinition(string fieldCode, DocumentRegion region, DocumentFieldType type,
            bool isEditable = false, IEnumerable<DocumentFieldType> alternateEditors = null)
        {
            FieldCode = DocumentIdentifiers.Identifier(fieldCode, "fieldCode");
            Region = region;
            Type = type;
            IsEditable = isEditable;
            var editors = new List<DocumentFieldType> { type };
            if (alternateEditors != null)
                foreach (var editor in alternateEditors)
                    if (!editors.Contains(editor)) editors.Add(editor);
            AllowedEditors = new ReadOnlyCollection<DocumentFieldType>(editors);
        }

        public bool AllowsEditor(DocumentFieldType editor)
        {
            return AllowedEditors.Contains(editor);
        }
    }
}
