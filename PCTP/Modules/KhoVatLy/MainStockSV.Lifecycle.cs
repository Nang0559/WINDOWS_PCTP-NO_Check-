using DevExpress.Utils.Extensions;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraVerticalGrid;
using PCTP.ClassSQL;
using PCTP.Common;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoCore.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuatKho.Services;
using PCTP.Shared.Common;
using PCTP.Shared.Services;
using PCTP.VIEWSTOCK.CanVas;
using PCTP.VIEWSTOCK.Fuction;
using PCTP.VIEWSTOCK.FunctionForm;
using PCTP.VIEWSTOCK.Models;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.UCControls;
using PCTP.VIEWSTOCK.ViewForm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq.SqlClient;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Modules.KhoVatLy
{
    public partial class MainStockSV
    {
        private void MainStock_Load(object sender, EventArgs e) { }
        private async void MainStock_Shown(object sender, EventArgs e)
                    {
                        if (isFirstShown) return;
                        isFirstShown = true;

                        SplashScreenManager.ShowForm(this, typeof(WaitFormExp), true, true, false);
                        SplashScreenManager.Default.SetWaitFormCaption("Đang tải cấu trúc kho...");

                        InitCanvasSettings();
                        InitPEditInput();
                        await LoadAllWarehouses();

                        this.WindowState = FormWindowState.Maximized;
                        SplashScreenManager.CloseForm();
                    }
        private void MainStock_Resize(object sender, EventArgs e)
                    {
                        if (!isFirstShown || this.WindowState == FormWindowState.Minimized) return;
                        _ = LoadAllWarehouses();
                    }
        private void OnExternalStockChanged()
                    {
                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action(OnExternalStockChanged));
                            return;
                        }
                        if (this.IsDisposed || !isFirstShown) return;
                        OnSlotUpdated();
                    }
        public async void OnSlotUpdated()
                    {
                        InitPEditInput(forceRefresh: true);
                        await LoadAllWarehouses();
                    }
        protected override void OnFormClosed(FormClosedEventArgs e)
                    {
                        _rackPopup?.Close();
                        _rackPopup?.Dispose();
                        StockChangedNotifier.StockChanged -= OnExternalStockChanged;
                        base.OnFormClosed(e);
                    }
    }
}
