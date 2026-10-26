using System;
using System.Windows.Forms;

namespace PCTP.Modules.BaoCao.UI
{
    internal static class BaoCaoNavigator
    {
        internal static void OpenTraceability(Control owner, string quickSearch)
        {
            using (FormBaoCaoTraceability form = new FormBaoCaoTraceability(quickSearch))
            {
                form.ShowDialog(owner == null ? null : owner.FindForm());
            }
        }

        internal static void OpenMain(Control owner)
        {
            using (FormBaoCaoMain form = new FormBaoCaoMain())
            {
                form.ShowDialog(owner == null ? null : owner.FindForm());
            }
        }
    }
}
