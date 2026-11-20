using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace PCTP.Framework.Documents.UI
{
    /// <summary>
    /// Module-supplied "special editor" for commands that need more than the document's own fields
    /// (for example choosing a LOT and slot). The runtime never invents inputs: it only forwards what
    /// this provider returns, and the dispatcher rejects anything that is not a declared parameter.
    /// </summary>
    public interface IDocumentCommandInputProvider
    {
        bool TryGetInput(ResolvedDocument document, ResolvedCommand command, DataRow headerRow,
            DataRow selectedDetailRow, IWin32Window owner, out IDictionary<string, object> input);
    }
}
