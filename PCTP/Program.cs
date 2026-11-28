
using System;
using System.Windows.Forms;
using PCTP.Security;

using PCTP.Shell.Help;

namespace PCTP
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Load mutable configuration from %ProgramData% before any SQL/EF/XPO settings are read.
            PCTP.Infrastructure.Configuration.RuntimeConfigurationBootstrap.Initialize();

            // B7R2_FCCdb is the only SQL declaration; derive the XPO name used by the DevExpress reports from it.
            PCTP.Infrastructure.Configuration.ReportConnectionAlias.Apply();

            OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("YMVN_APP");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Contextual help is attached centrally so operational forms do not
            // need to duplicate the same button/event wiring.
            WmsHelpOverlay.Start();

            // Ctrl+C trên mọi GridView chỉ copy giá trị cell, không kèm tiêu đề cột.
            GridViewSettings.Start();

            //Application.Run(new UF_TACHLOT());
            //Application.Run(new YAMAHAQRCDE_SP());
            //Application.Run(new FrmTEST());
            //Application.Run(new GIAOHANGYMN());
            //Application.Run(new MENU_AUTO_QRCODE());
            //Application.Run(new PGH_XK());
            //Application.Run(new MainStock());
            var authentication = new AuthenticationService(new AuthenticationRepository());
            using (var login = new LoginForm(authentication))
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }

            // Validate every registered document (SQL metadata + real schema) once per session. Never throws: a failure
            // only disables the document screens, and MesDocumentModule.GetRuntime reports the reason when one is opened.
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var report = PCTP.Modules.MesPlanning.Documents.MesDocumentModule.Initialize();
                    System.Diagnostics.Trace.TraceInformation("Document Runtime startup validation:" + Environment.NewLine +
                        (report == null ? PCTP.Modules.MesPlanning.Documents.MesDocumentModule.DescribeProblem(
                            PCTP.Modules.MesPlanning.Documents.ComponentDemandDefinition.ModuleCode) : report.ToString()));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Document Runtime startup validation failed: " + ex);
                }
            });

            Application.Run(new Main_APP());
            PCTP.Shared.Common.ActiveSiteStore.Resolver = null;
            PCTP.Shared.Common.ActiveSiteStore.Clear();
            UserSession.SignOut();
            // Application.Run(new UF_QRCodeERR());
            //Application.Run(new QRCODE_HVN.ComaprePart.ComaparePart());
            //Application.Run(new IFS_PUR_OR.FRM_PURCHASE_ODERScs());
            //Application.Run(new PCTP.QRCODE_HVN.NhanLaiNG.frm_NhanHangHVN());
        }
    }
}
