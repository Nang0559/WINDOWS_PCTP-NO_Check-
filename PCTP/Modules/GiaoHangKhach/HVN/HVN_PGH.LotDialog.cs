using PCTP.Modules.GiaoHangKhach.HVN.Controls;
using System;
using System.Data;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        private bool _spModeUiWired;

        public int ShowChonSttTrungMa(DataTable danhSachTrung)
            => _phieuDialogControl.ShowChonSttTrungMa(danhSachTrung);

        private void WireSpModeUi()
        {
            if (_spModeUiWired || _phieuHeaderControl == null)
                return;

            _spModeUiWired = true;
            _phieuHeaderControl.LoaiPhieuChanged += OnSpModeChanged;
            ApplySpModeUi(_phieuHeaderControl.IsLoaiSP);
        }

        private void OnSpModeChanged(object sender, EventArgs e)
        {
            if (_phieuHeaderControl == null)
                return;

            ApplySpModeUi(_phieuHeaderControl.IsLoaiSP);
        }

        private void ApplySpModeUi(bool isSP)
        {
            // Hide the real legacy layout container. The RadioGroups are nested
            // inside tabPaneHVN -> tabVP/tabHN -> groupControl, so hiding only
            // the RadioGroup leaves the hour panel/header area visible.
            if (sidePanel1 != null)
            {
                sidePanel1.Visible = !isSP;
                sidePanel1.Enabled = !isSP;
            }

            if (tabPaneHVN != null)
            {
                tabPaneHVN.Visible = !isSP;
                tabPaneHVN.Enabled = !isSP;
            }

            if (radioGroup2 != null)
            {
                radioGroup2.Visible = !isSP;
                radioGroup2.Enabled = !isSP;
            }

            if (RDO_GXHN != null)
            {
                RDO_GXHN.Visible = !isSP;
                RDO_GXHN.Enabled = !isSP;
            }
        }
    }
}
