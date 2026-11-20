using System;
using System.Collections.Generic;

namespace PCTP.Framework.Documents
{
    /// <summary>Composition root for the document runtime: registry, metadata, data, dispatch and authorization.</summary>
    public sealed class DocumentRuntimeServices
    {
        public IDocumentRegistry Registry { get; private set; }
        public IDocumentMetadataRepository Metadata { get; private set; }
        public IDocumentDataProvider Data { get; private set; }
        public DocumentCommandDispatcher Dispatcher { get; private set; }
        public IDocumentAuthorizer Authorizer { get; private set; }
        public IDocumentSchemaProbe SchemaProbe { get; private set; }

        public DocumentRuntimeServices(IDocumentRegistry registry, IDocumentMetadataRepository metadata,
            IDocumentDataProvider data, DocumentCommandDispatcher dispatcher, IDocumentAuthorizer authorizer,
            IDocumentSchemaProbe schemaProbe = null)
        {
            if (registry == null) throw new ArgumentNullException("registry");
            if (metadata == null) throw new ArgumentNullException("metadata");
            if (data == null) throw new ArgumentNullException("data");
            if (dispatcher == null) throw new ArgumentNullException("dispatcher");
            if (authorizer == null) throw new ArgumentNullException("authorizer");
            Registry = registry;
            Metadata = metadata;
            Data = data;
            Dispatcher = dispatcher;
            Authorizer = authorizer;
            SchemaProbe = schemaProbe;
        }

        public static DocumentRuntimeServices CreateDefault(IEnumerable<DocumentFormDefinition> definitions,
            IDocumentWorkflowGate workflowGate = null, string connectionString = null)
        {
            var registry = new DocumentRegistry(definitions);
            var authorizer = new SessionDocumentAuthorizer();
            return new DocumentRuntimeServices(
                registry,
                new SqlDocumentMetadataRepository(connectionString),
                new SqlDocumentDataProvider(registry, authorizer, connectionString),
                new DocumentCommandDispatcher(registry, authorizer, new SqlDocumentCommandExecutor(connectionString), workflowGate),
                authorizer,
                new SqlDocumentSchemaProbe(connectionString));
        }

        /// <summary>
        /// Startup validation: for every registered document, loads the SQL metadata and checks it against the
        /// trusted definition, then checks the definition against the real SQL views and procedures.
        /// Never throws for a bad module; the failure is recorded so callers can disable that module (fail-closed).
        /// </summary>
        public DocumentStartupReport ValidateAll()
        {
            var report = new DocumentStartupReport();
            foreach (var definition in Registry.All)
            {
                try
                {
                    var metadata = Metadata.Load(definition.ModuleCode);
                    if (metadata != null) DocumentDefinitionValidator.DemandMatchesRegistry(definition, metadata);
                    if (SchemaProbe == null)
                        throw new InvalidOperationException("No schema probe is configured; the real SQL schema cannot be verified.");
                    DocumentDefinitionValidator.DemandMatchesSchema(definition, SchemaProbe);
                    report.Add(definition.ModuleCode, true, metadata == null ? "no SQL metadata, using code defaults" : null);
                }
                catch (Exception ex)
                {
                    report.Add(definition.ModuleCode, false, ex.GetBaseException().Message);
                }
            }
            return report;
        }

        /// <summary>Loads metadata, validates it against the trusted definition (fail-closed) and merges it.</summary>
        public ResolvedDocument Resolve(string moduleCode)
        {
            var definition = Registry.GetRequired(moduleCode);
            return DocumentLayoutResolver.Resolve(definition, Metadata.Load(definition.ModuleCode));
        }
    }
}
