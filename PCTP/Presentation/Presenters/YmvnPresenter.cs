using PCTP.ClassSQL;
using PCTP.Domain.Events;
using PCTP.Modules.GiaoHangKhach.Services;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.Presentation.Views;
using System;

namespace PCTP.Presentation.Presenters
{
    internal sealed class YmvnPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IYmvnView _v;
        private bool _qrSessionLocked;

        internal YmvnPresenter(HVNPresenterContext context)
        {
            _c = context;
            _v = _c.YmvnView;
            _v.HoanThanhYMVNClicked += OnHoanThanhYMVN;
            _v.UploadMilkrunSPClicked += OnUploadMilkrunSP;
            _v.GioXuatCheckedChanged += OnGioXuatCheckedChanged;
            _c.DocQrView.DocQRCodeClicked += OnDocQrStarted;
            _c.DocQrView.XoaToanBoQRClicked += OnDocQrCancelled;
            _c.Bus.Subscribe<HoanThanhYMVNCompletedEvent>(OnHoanThanhYMVNCompleted);
        }

        private void OnDocQrStarted(object sender, EventArgs e)
        {
            if (!_c.Cfg.Delivery.CoGear || _qrSessionLocked)
                return;

            _v.LockCheckListYMVN();
            _qrSessionLocked = true;
        }

        private void OnDocQrCancelled(object sender, EventArgs e)
        {
            UnlockQrSession();
        }

        private void OnGioXuatCheckedChanged(object sender, EventArgs e)
        {
            if (_qrSessionLocked)
                return;

            var list = _v.GetCheckedGioXuat();
            if (list.Count == 0) return;
            _c.UpdateGioXuatFromCheckList(list);
            _c.LoadPhieuHienTai();
        }

        private void OnHoanThanhYMVN(object sender, EventArgs e)
        {
            _c.RunWithLoading(() =>
            {
                if (!_c.Cfg.Delivery.CoGear)
                {
                    _c.PhieuSvc.HoanThanhYMVN(_c.PhieuView.IsLoaiSP);
                    return;
                }

                string validationError;
                if (!YmvnGearQuantityValidator.ValidateCompletion(
                    _c.PhieuView.GetDonHangTable(),
                    _c.QrSvc.LoadAll(),
                    out validationError))
                {
                    _c.UiContext.Post(_ => _v.ShowError(validationError), null);
                    return;
                }

                _c.PhieuSvc.HoanThanhYMVN(_c.PhieuView.IsLoaiSP);
                _c.UiContext.Post(_ =>
                {
                    // Reload history while the QR session is still locked. This lets
                    // SetCheckedGiosYMVN restore the newly delivered hours before the
                    // checklist becomes interactive again.
                    _c.LoadPhieuHienTai();
                    UnlockQrSession();
                    _c.QrSvc.SetCheDoBanSP(false);
                    _c.PhieuView.UnlockAllRadio();
                    _c.PhieuView.SwitchToPhieuView();
                }, null);
            }, "Đang xử lý hoàn thành...");
        }

        private void OnUploadMilkrunSP(object sender, EventArgs e)
        {
            if (_c.Cfg.Delivery.CoGear)
            {
                using (var frm = new FRM_UploadMikrun(new SQLPROVIDER(), _c.Cfg))
                    frm.ShowDialog();
            }
            else if (_c.CustomerBehavior.UsesDateBasedOrderUpload)
            {
                using (var frm = new FRM_UploadMikrun(
                    new SQLPROVIDER(),
                    _c.Cfg,
                    targetTable: _c.CustomerBehavior.GetOrderUploadTable(),
                    title: _c.CustomerBehavior.GetOrderUploadTitle()))
                    frm.ShowDialog();
            }
            else return;

            _v.ShowLoading(true);
            try
            {
                _c.LoadPhieuHienTai();
            }
            catch (Exception ex)
            {
                _v.ShowError("Lỗi reload sau upload: " + ex.Message);
            }
            finally
            {
                _c.HideLoadingUnlessAwaitingPhieuLoad();
            }
        }

        private void OnHoanThanhYMVNCompleted(HoanThanhYMVNCompletedEvent e)
        {
            _v.BindHoanThanhYMVN(e.Result);
        }

        private void UnlockQrSession()
        {
            if (!_qrSessionLocked)
                return;

            _v.UnlockCheckListYMVN();
            _qrSessionLocked = false;
        }

        public void Dispose()
        {
            _v.HoanThanhYMVNClicked -= OnHoanThanhYMVN;
            _v.UploadMilkrunSPClicked -= OnUploadMilkrunSP;
            _v.GioXuatCheckedChanged -= OnGioXuatCheckedChanged;
            _c.DocQrView.DocQRCodeClicked -= OnDocQrStarted;
            _c.DocQrView.XoaToanBoQRClicked -= OnDocQrCancelled;
            _c.Bus.Unsubscribe<HoanThanhYMVNCompletedEvent>(OnHoanThanhYMVNCompleted);
        }
    }
}