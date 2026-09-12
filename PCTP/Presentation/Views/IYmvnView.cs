using System;
using System.Collections.Generic;
using System.Data;

namespace PCTP.Presentation.Views
{
    /// <summary>
    /// UI contract for the YMVN-specific delivery workflow.
    /// </summary>
    public interface IYmvnView
    {
        void SetupGridDonHangYMVN(bool coGear);
        void LockCheckListYMVN();
        void UnlockCheckListYMVN();
        void BindHoanThanhYMVN(DataTable dt);
        void BindGioXuatCheckList(List<string> danhSachGio);
        List<string> GetCheckedGioXuat();
        void BindGhepLotYMVN(DataTable dt);
        void ShowReportYMVN(DataTable reportData);
        void SetCheckedGiosYMVN(List<string> checkedGios);

        event EventHandler GioXuatCheckedChanged;
        event EventHandler CheckGX_ItemCheck;
        event EventHandler HoanThanhYMVNClicked;
        event EventHandler UploadMilkrunSPClicked;
    }
}
