using System.Collections.Generic;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Layer 2 - approved presentation metadata as stored in SQL (migration 039). Plain data only:
    /// there is deliberately no SQL text, expression, type name or callback anywhere in this model.
    /// </summary>
    public sealed class DocumentMetadata
    {
        public string ModuleCode { get; set; }
        public string HeaderView { get; set; }
        public string DetailView { get; set; }
        public string WorkflowCode { get; set; }
        public string DisplayName { get; set; }
        public int ConfigVersion { get; set; }
        public List<DocumentFieldMetadata> Fields { get; private set; }
        public List<DocumentCommandMetadata> Commands { get; private set; }
        public List<DocumentLayoutMetadata> Layouts { get; private set; }

        public DocumentMetadata()
        {
            Fields = new List<DocumentFieldMetadata>();
            Commands = new List<DocumentCommandMetadata>();
            Layouts = new List<DocumentLayoutMetadata>();
        }
    }

    public sealed class DocumentFieldMetadata
    {
        public string FieldCode { get; set; }
        public string RegionCode { get; set; }
        public string Caption { get; set; }
        public int SequenceNo { get; set; }
        public int? Width { get; set; }
        public string DisplayFormat { get; set; }
        public string EditorCode { get; set; }
        public bool IsVisible { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsRequired { get; set; }
        public string LookupCode { get; set; }
    }

    public sealed class DocumentCommandMetadata
    {
        public string CommandCode { get; set; }
        public string Caption { get; set; }
        public int SequenceNo { get; set; }
        public bool IsVisible { get; set; }
        public int? RequiredStatusCode { get; set; }
    }

    /// <summary>LayoutCode HEADER or DETAIL: caption/visibility of the two document regions.</summary>
    public sealed class DocumentLayoutMetadata
    {
        public string LayoutCode { get; set; }
        public string RegionCode { get; set; }
        public string Caption { get; set; }
        public int SequenceNo { get; set; }
        public bool IsVisible { get; set; }
    }
}
