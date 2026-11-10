using System;
using System.Windows.Forms;

using PCTP.Shared.Helpers;

namespace PCTP.Modules.BaoCao.UI
{
    internal static class BaoCaoNavigator
    {
        internal static void OpenTraceability(Control owner, string quickSearch)
        {
            FormWindowManager.ShowSingle(owner == null ? null : owner.FindForm(),
                () => new FormBaoCaoTraceability(quickSearch));
        }

        internal static void OpenMain(Control owner)
        {
            FormWindowManager.ShowSingle(owner == null ? null : owner.FindForm(),
                () => new FormBaoCaoMain());
        }
    }
}
