using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.Domain.Events;
using PCTP.Modules.GiaoHangKhach.SubForm;
using System;

namespace PCTP.Presentation.Presenters
{
    internal sealed class YmvnPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        internal YmvnPresenter(HVNPresenterContext context) { _c = context; _c.View.HoanThanhYMVNClicked += OnHoanThanhYMVN; _c.View.UploadMilkrunSPClicked += OnUploadMilkrunSP; _c.View.GioXuatCheckedChanged += OnGioXuatCheckedChanged; _c.Bus.Subscribe<HoanThanhYMVNCompletedEvent>(OnHoanThanhYMVNCompleted); }
        private void OnGioXuatCheckedChanged(object sender, EventArgs e) { var list = _c.View.GetCheckedGioXuat(); if (list.Count == 0) return; _c.UpdateGioXuatFromCheckList(list); _c.LoadPhieuHienTai(); }
        private void OnHoanThanhYMVN(object sender, EventArgs e) => _c.RunWithLoading(() => { _c.PhieuSvc.HoanThanhYMVN(_c.View.IsLoaiSP); _c.UiContext.Post(_ => { _c.QrSvc.SetCheDoBanSP(false); _c.View.UnlockAllRadio(); _c.View.SwitchToPhieuView(); _c.LoadPhieuHienTai(); }, null); }, "Đang xử lý hoàn thành...");
        private void OnUploadMilkrunSP(object sender, EventArgs e)
        {
            if (_c.Cfg.Delivery.CoGear) { using (var frm = new FRM_UploadMikrun(new SQLPROVIDER(), _c.Cfg)) frm.ShowDialog(); }
            else if (_c.CustomerBehavior.UsesDateBasedOrderUpload)
            {
                using (var frm = new FRM_UploadMikrun(new SQLPROVIDER(), _c.Cfg, targetTable: _c.CustomerBehavior.GetOrderUploadTable(), title: _c.CustomerBehavior.GetOrderUploadTitle()))
                    frm.ShowDialog();
            }
            else return;
            _c.View.ShowLoading(true); try { _c.LoadPhieuHienTai(); } catch (Exception ex) { _c.View.ShowError("Lỗi reload sau upload: " + ex.Message); } finally { _c.HideLoadingUnlessAwaitingPhieuLoad(); }
        }
        private void OnHoanThanhYMVNCompleted(HoanThanhYMVNCompletedEvent e) { _c.View.BindHoanThanhYMVN(e.Result); }
        public void Dispose() { _c.View.HoanThanhYMVNClicked -= OnHoanThanhYMVN; _c.View.UploadMilkrunSPClicked -= OnUploadMilkrunSP; _c.View.GioXuatCheckedChanged -= OnGioXuatCheckedChanged; _c.Bus.Unsubscribe<HoanThanhYMVNCompletedEvent>(OnHoanThanhYMVNCompleted); }
    }
}
