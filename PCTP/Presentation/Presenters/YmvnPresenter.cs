using System.Data;
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
            // Khóa checkbox giờ theo DỮ LIỆU (như HVN khóa theo phiên QR còn dữ liệu): còn dòng TMP chưa cập nhật kho
            // hoặc YMVN_DOCQRCODE còn dữ liệu thì giữ khóa, kể cả sau khi mở lại form.
            _c.Bus.Subscribe<PhieuLoadedEvent>(OnPhieuLoadedApplyLock);
            _c.Bus.Subscribe<KhoUpdatedEvent>(OnKhoUpdatedApplyLock);
        }

        private void OnPhieuLoadedApplyLock(PhieuLoadedEvent e) { ApplyDataLock(); }
        private void OnKhoUpdatedApplyLock(KhoUpdatedEvent e) { ApplyDataLock(); }

        /// <summary>
        /// Khóa checkbox giờ YMVN khi còn dữ liệu dở dang: YMVN_DOCQRCODE còn dòng, hoặc TMPPHIEUGIAOHANG_100002
        /// còn dòng đã có LOT mà STATUS chưa 'OK'. Hết cả hai thì mở khóa (chỉ khi trước đó do chính logic này khóa).
        /// </summary>
        private void ApplyDataLock()
        {
            if (!_c.Cfg.Delivery.CoGear) return;
            bool pending;
            try
            {
                pending = _c.QrSvc.CoDocQRNao();
                if (!pending)
                {
                    DataTable tmp = _c.PhieuSvc.GetDonHangHienTai(_c.Cfg.Delivery.TmpTable);
                    if (tmp != null && tmp.Columns.Contains("LOT") && tmp.Columns.Contains("STATUS"))
                    {
                        foreach (DataRow r in tmp.Rows)
                        {
                            string lot = Convert.ToString(r["LOT"]).Trim();
                            string st = Convert.ToString(r["STATUS"]).Trim();
                            if (lot.Length > 0 && !string.Equals(st, "OK", StringComparison.OrdinalIgnoreCase)) { pending = true; break; }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YMVN ApplyDataLock] " + ex.Message);
                return;
            }

            _c.RunOnUi(() =>
            {
                if (pending)
                {
                    if (_qrSessionLocked) return;
                    _v.LockCheckListYMVN();
                    _qrSessionLocked = true;
                }
                else
                {
                    UnlockQrSession();
                }
            });
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

            // Bỏ chọn toàn bộ là một lựa chọn hợp lệ: phải tải tập rỗng và
            // đồng bộ xóa TMP cũ, không được giữ lại phiên giờ trước.
            var list = _v.GetCheckedGioXuat();
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
                // Đối chiếu với TMP của phiên (đúng tập dòng đã nạp để bắn QR), không dùng lưới Phiếu.
                if (!YmvnGearQuantityValidator.ValidateCompletion(
                    _c.PhieuSvc.GetDonHangYmvnTuTmp(),
                    _c.QrSvc.LoadAll(),
                    out validationError,
                    code => _c.QrSvc.GetGearName(code)))
                {
                    // Thiếu/sai số lượng theo Gear: hiện thông báo (OK) rồi quay về màn
                    // hình Phiếu như HVN (100001).
                    //  - Chưa có tem nào trong phiên QR: nhả phiên (mở lại chọn giờ/ngày),
                    //    giống "Xóa Toàn Bộ Dữ Liệu" nhưng không xóa gì.
                    //  - Đã có tem: GIỮ phiên QR (checklist giờ vẫn khóa) vì đổi giờ sẽ làm
                    //    lệch dữ liệu đã bắn; bấm DOC QRCODE đọc tiếp hoặc xóa toàn bộ để nhả.
                    bool coDuLieuQr = _c.QrSvc.CoDocQRNao();
                    _c.UiContext.Post(_ =>
                    {
                        if (coDuLieuQr)
                        {
                            _v.ShowWarning(validationError +
                                "\r\n\r\nPhiên đọc QR vẫn được giữ, bấm DOC QRCODE để quay lại đọc tiếp.");
                            _c.PhieuView.SwitchToPhieuView();
                            return;
                        }

                        _v.ShowWarning(validationError +
                            "\r\n\r\nChưa có tem nào được đọc, quay lại màn hình Phiếu.");
                        _c.PhieuView.SwitchToPhieuView();
                        _c.IsBanQR = false;
                        _c.QrSvc.SetCheDoBan("");
                        _c.ClearDeliverySession();
                        _c.DocQrView.UnlockDocQrDeliveryContext();
                        UnlockQrSession();
                        _c.PhieuView.UnlockAllRadio();
                        _c.PhieuView.UnlockDatePicker();
                    }, null);
                    return;
                }

                _c.PhieuSvc.HoanThanhYMVN(_c.PhieuView.IsLoaiSP);
                _c.UiContext.Post(_ =>
                {
                    // Reload history while the QR session is still locked. This lets
                    // SetCheckedGiosYMVN restore the newly delivered hours before the
                    // checklist becomes interactive again.
                    _c.LoadPhieuHienTai();
                    ApplyDataLock(); // chỉ mở khóa giờ khi TMP đã cập nhật hết và YMVN_DOCQRCODE trống
                    _c.QrSvc.SetCheDoBanSP(false);
                    _c.PhieuView.UnlockAllRadio();
                    _c.PhieuView.SwitchToPhieuView();

                    // Giống HVN (PhieuPresenter.OnHoanThanh): kết thúc phiên QR. Nếu không reset, IsBanQR kẹt true
                    // nên nút "Lấy Lại Lot" (điều kiện !IsBanQR) bị ẩn và đổi ngày/giờ bị chặn.
                    _c.IsBanQR = false;
                    _c.QrSvc.SetCheDoBan("");
                    _c.ClearDeliverySession();
                    _c.DocQrView.UnlockDocQrDeliveryContext();
                    _c.PhieuView.UnlockDatePicker();
                    DataTable hienTai = _c.PhieuView.GetDonHangTable();
                    if (hienTai != null)
                        _c.SetupPhieuButtonsDefault(true, false, _c.PhieuSvc.CheckCoLotChuaCNK(hienTai));
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
            _c.Bus.Unsubscribe<PhieuLoadedEvent>(OnPhieuLoadedApplyLock);
            _c.Bus.Unsubscribe<KhoUpdatedEvent>(OnKhoUpdatedApplyLock);
        }
    }
}