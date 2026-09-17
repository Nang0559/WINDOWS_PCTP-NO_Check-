using PCTP.Modules.GiaoHangKhach.HVN.Controls;
using System;
using System.Data;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        private bool _spModeUiWired;

        /// <summary>
        /// View-bound duplicate LOT selection. ListView construction is owned by
        /// PhieuDialogControl, not by the presenter/service.
        /// </summary>
        public int ShowChonSttTrungMa(DataTable danhSachTrung)
            => _phieuDialogControl.ShowChonSttTrungMa(danhSachTrung);

        // This partial file is already part of the legacy csproj, so the SP-mode
        // UI behavior stays inside the existing compile surface.
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
            var content = _phieuHeaderControl.ContentControl;
            if (content == null)
                return;

            // The legacy layout puts both hour RadioGroups inside tabPaneHVN:
            //   sidePanel1 -> tabPaneHVN -> tabVP/groupControl2/radioGroup2
            //                         -> tabHN/groupControl1/RDO_GXHN
            // For SP the form is day + plant + dock, therefore the whole
            // hour-selection TabPane must disappear, not only the RadioGroup.
            var hourPane = FindSpModeControl<Control>(content, "tabPaneHVN");
            if (hourPane != null)
            {
                hourPane.Visible = !isSP;
                hourPane.Enabled = !isSP;
            }

            // Keep the individual controls synchronized as well. This protects
            // against a designer/layout change where the RadioGroups are hosted
            // outside the TabPane.
            var radioVp = FindSpModeControl<Control>(content, "radioGroup2");
            var radioHn = FindSpModeControl<Control>(content, "RDO_GXHN");

            if (radioVp != null)
            {
                radioVp.Visible = !isSP;
                radioVp.Enabled = !isSP;
            }

            if (radioHn != null)
            {
                radioHn.Visible = !isSP;
                radioHn.Enabled = !isSP;
            }
        }

        private static T FindSpModeControl<T>(Control parent, string name) where T : Control
        {
            if (parent == null)
                return null;

            if (string.Equals(parent.Name, name, StringComparison.OrdinalIgnoreCase))
                return parent as T;

            foreach (Control child in parent.Controls)
            {
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    return child as T;

                var nested = FindSpModeControl<T>(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
