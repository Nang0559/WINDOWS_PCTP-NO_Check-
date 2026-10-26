using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.UiMd
{
    public interface IWorkflowRepository
    {
        IReadOnlyList<WorkflowTransition> GetTransitions(string processCode);
        void InvalidateCache(string processCode = null);
    }

}
