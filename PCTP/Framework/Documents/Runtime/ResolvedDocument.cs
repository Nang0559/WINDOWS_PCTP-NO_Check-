using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PCTP.Framework.Documents
{
    public sealed class ResolvedField
    {
        public DocumentFieldDefinition Definition { get; internal set; }
        public string FieldCode { get { return Definition.FieldCode; } }
        public string Caption { get; internal set; }
        public int SequenceNo { get; internal set; }
        public int? Width { get; internal set; }
        public string DisplayFormat { get; internal set; }
        public DocumentFieldType Editor { get; internal set; }
        public bool IsVisible { get; internal set; }
        public bool IsReadOnly { get; internal set; }
        public bool IsRequired { get; internal set; }
        public string LookupCode { get; internal set; }
    }

    public sealed class ResolvedCommand
    {
        public DocumentCommandDefinition Definition { get; internal set; }
        public string CommandCode { get { return Definition.CommandCode; } }
        public string Caption { get; internal set; }
        public int SequenceNo { get; internal set; }
        public bool IsVisible { get; internal set; }
        public int? RequiredStatusCode { get; internal set; }
    }

    /// <summary>Trusted definition merged with validated presentation metadata, ready for a renderer.</summary>
    public sealed class ResolvedDocument
    {
        public DocumentFormDefinition Definition { get; internal set; }
        public string DisplayName { get; internal set; }
        public string HeaderCaption { get; internal set; }
        public string DetailCaption { get; internal set; }
        public bool HeaderVisible { get; internal set; }
        public bool DetailVisible { get; internal set; }
        public IReadOnlyList<ResolvedField> HeaderFields { get; internal set; }
        public IReadOnlyList<ResolvedField> DetailFields { get; internal set; }
        public IReadOnlyList<ResolvedCommand> Commands { get; internal set; }

        public ResolvedField FindField(DocumentRegion region, string fieldCode)
        {
            var list = region == DocumentRegion.Header ? HeaderFields : DetailFields;
            return list.FirstOrDefault(f => string.Equals(f.FieldCode, fieldCode, StringComparison.OrdinalIgnoreCase));
        }
    }
}
