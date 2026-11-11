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

            bool daXuLy = _c.RunWithLoadingSync(
                () => _c.PhieuSvc.XuLySauUploadGiaoDB(),
                "Đang xử lý đơn hàng GIAO DB...");

            // Bị bỏ qua (đang bận) hoặc lỗi: đã báo cho người dùng, không load lưới như thể đã thành công.
            if (!daXuLy) return;

            _c.UiContext.Post(_ => LoadLuoiSauUpload(), null);
        }

        // Load lưới sau upload. Chạy qua UiContext.Post nên PHẢI tự bắt lỗi (nếu không sẽ rơi vào
        // handler toàn cục của WinForms và người dùng không thấy gì). Lưới rỗng thì giải thích lý do.
        private void LoadLuoiSauUpload()
        {
            try
            {
                var dt = _c.LoadPhieuGiaoDB();
                if (dt != null && dt.Rows.Count > 0) return;

                string canhBao = _c.PhieuSvc.CanhBaoLuoiRongGiaoDB(_c.View.SelectedDate, _c.AddNM);
                if (!string.IsNullOrEmpty(canhBao))
                    _v.ShowWarning(canhBao);
            }
            catch (Exception ex)
            {
                _v.ShowError("Lỗi khi tải lưới GIAO DB: " + ex.Message);
            }
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
