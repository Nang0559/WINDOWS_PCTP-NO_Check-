using DevExpress.XtraEditors;
using DevExpress.XtraSplashScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.VIEWSTOCK.FunctionForm
{
    /// <summary>
    /// Wait-form runner dùng chung cho các form không đi qua IHVNView/Presenter
    /// (FormTraHangNGNew, frmGiaoBuNG, FormQuetQRGiaoBuNG...).
    /// Tương đương HVN_Presenter.RunWithLoadingSync nhưng show trực tiếp
    /// SplashScreenManager thay vì gọi qua _view.ShowLoading.
    /// </summary>
    public static class WaitFormRunner
    {
        public static void RunWithLoadingSync(Form owner, Action action, string caption = "Đang xử lý...")
        {
            try
            {
                SplashScreenManager.ShowForm(owner, typeof(WaitFormExp), true, true, false);
                SplashScreenManager.Default.SetWaitFormCaption(caption);
                Application.DoEvents(); // ép vẽ wait form trước khi block luồng

                action();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show("Lỗi: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SplashScreenManager.CloseForm();
            }
        }
    }
}
