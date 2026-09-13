using PCTP.Applications.Services;
using PCTP.Modules.GiaoHangKhach.HVN.Controls;
using PCTP.Presentation.Views;
using System;

namespace PCTP.Presentation.Presenters
{
    internal sealed class GiaoDbPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IGiaoDbView _v;
        private readonly GiaoDbControl _ui;

        internal GiaoDbPresenter(HVNPresenterContext context)
        {
            _c = context ?? throw new ArgumentNullException(nameof(context));
            _v = _c.GiaoDbView;
            _ui = new GiaoDbControl();
            _ui.Configure(_c.PhieuSvc);

            _v.UploadGiaoDBClicked += OnUploadGiaoDB;
            _v.LuuGiaoDBClicked += OnLuuGiaoDB;
        }

        internal bool OnGiaoDBChanging(int addNm) { return true; }

        private void OnUploadGiaoDB(object sender, EventArgs e)
        {
            if (_ui.ShowUploadDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            _c.RunWithLoadingSync(
                () => _c.PhieuSvc.XuLySauUploadGiaoDB(),
                "Đang xử lý đơn hàng GIAO DB...");

            _c.UiContext.Post(_ => _c.LoadPhieuGiaoDB(), null);
        }

        private void OnLuuGiaoDB(object sender, EventArgs e)
        {
            _c.RunWithLoadingSync(
                () =>
                {
                    _c.PhieuSvc.LuuGiaoDB(
                        _c.PhieuView.GetDonHangTable(),
                        _c.GioXuatHienTai,
                        _c.AddNM);

                    _v.ShowInfo("Xong !!!");
                },
                "Đang lưu dữ liệu giao DB...");
        }

        public void Dispose()
        {
            _v.UploadGiaoDBClicked -= OnUploadGiaoDB;
            _v.LuuGiaoDBClicked -= OnLuuGiaoDB;
            _ui.Dispose();
        }
    }
}
