using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PCTP.Framework.Documents
{
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

        public IEnumerable<DocumentFormDefinition> All
        {
            get { return _definitions.Values.ToList(); }
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
}
