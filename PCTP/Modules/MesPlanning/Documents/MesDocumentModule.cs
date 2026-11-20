using System.Collections.Generic;
using PCTP.Framework.Documents;

namespace PCTP.Modules.MesPlanning.Documents
{
    /// <summary>Startup registration point for MES documents (opt-in; existing forms stay the entry points).</summary>
    public static class MesDocumentModule
    {
        public static IEnumerable<DocumentFormDefinition> Definitions()
        {
            yield return ComponentDemandDefinition.Create();
        }

        public static DocumentRuntimeServices CreateRuntime()
        {
            return DocumentRuntimeServices.CreateDefault(Definitions());
        }
    }
}
