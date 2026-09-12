using PCTP.Applications.Services;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.Presentation.Views;
using System;
using System.Windows.Forms;

namespace PCTP.Presentation.Presenters
{
    internal sealed class GiaoDbPresenter : IDisposable
    {
        private readonly HVNPresenterContext _c;
        private readonly IGiaoDbView _v;
        internal GiaoDbPresenter(HVNPresenterContext context) { _c = context; _v = _c.GiaoDbView; _v.UploadGiaoDBClicked += OnUploadGiaoDB; _v.LuuGiaoDBClicked += OnLuuGiaoDB; }
        internal bool OnGiaoDBChanging(int addNm) { return true; }
        private void OnUploadGiaoDB(object sender, EventArgs e)
        {
            using (var frm = new FRM_UploadGiaoDB(_c.PhieuSvc)) { if (frm.ShowDialog() != DialogResult.OK) return; }
            _c.RunWithLoadingSync(() => _c.PhieuSvc.XuLySauUploadGiaoDB(), "Đang xử lý đơn hàng GIAO DB...");
            _c.UiContext.Post(_ => _c.LoadPhieuGiaoDB(), null);
        }
        private void OnLuuGiaoDB(object sender, EventArgs e) => _c.RunWithLoadingSync(() => { _v.SwitchToPhieuDBView(); _c.PhieuSvc.LuuGiaoDB(_c.View.GetDonHangTable(), _c.GioXuatHienTai, _c.AddNM); _v.ShowInfo("Xong !!!"); }, "Đang lưu dữ liệu giao DB...");
        public void Dispose() { _v.UploadGiaoDBClicked -= OnUploadGiaoDB; _v.LuuGiaoDBClicked -= OnLuuGiaoDB; }
    }
}
