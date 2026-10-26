
using DevExpress.XtraSplashScreen;
using PCTP.Shared.Notifiers;
using System;

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

            await _waitForm.RunAsync(async () =>
            {
                InitCanvasSettings();
                InitPEditInput();
                await LoadAllWarehouses();
            }, "Đang tải cấu trúc kho...");

            this.WindowState = FormWindowState.Maximized;
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
