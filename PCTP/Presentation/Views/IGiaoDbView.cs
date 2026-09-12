using System;
using System.Data;

namespace PCTP.Presentation.Views
{
    /// <summary>
    /// UI contract for the GiaoDB delivery scenario.
    /// </summary>
    public interface IGiaoDbView : IViewFeedback
    {
        void SwitchToPhieuDBView();
        void XoaDongGiaoDB();
        void ThemDongGiaoDB(DataTable danhSachMaHang);
        void UpdateGioXuatFromDB(string gioFCC);
        bool IsLoaiSP { get; }
        event EventHandler UploadGiaoDBClicked;
        event EventHandler LuuGiaoDBClicked;
    }
}
