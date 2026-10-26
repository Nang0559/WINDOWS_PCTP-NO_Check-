using System;
using System.Windows.Forms;

namespace PCTP.QC.TestRunner;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}