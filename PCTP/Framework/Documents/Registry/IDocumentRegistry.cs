namespace PCTP.Framework.Documents
{
    /// <summary>Immutable allow-list populated only by application startup code.</summary>
    public interface IDocumentRegistry
    {
        DocumentFormDefinition GetRequired(string moduleCode);
        bool TryGet(string moduleCode, out DocumentFormDefinition definition);
        /// <summary>Every trusted definition, used by startup validation.</summary>
        System.Collections.Generic.IEnumerable<DocumentFormDefinition> All { get; }
    }
}
