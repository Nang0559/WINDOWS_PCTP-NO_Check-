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

            var radioVp = FindSpModeControl<Control>(content, "radioGroup2");
            var radioHn = FindSpModeControl<Control>(content, "RDO_GXHN");

            if (radioVp != null)
                radioVp.Visible = !isSP;
            if (radioHn != null)
                radioHn.Visible = !isSP;

            HideDedicatedHourContainer(radioVp, isSP);
            HideDedicatedHourContainer(radioHn, isSP);
        }

        private static void HideDedicatedHourContainer(Control radio, bool visible)
        {
            if (radio == null || !IsDedicatedHourContainer(radio.Parent))
                return;

            radio.Parent.Visible = visible;
        }

        private static bool IsDedicatedHourContainer(Control parent)
        {
            if (parent == null)
                return false;

            string typeName = parent.GetType().Name ?? string.Empty;
            if (typeName.IndexOf("TabNavigationPage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("TabPane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("TabControl", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            // Do not collapse large layout containers; the hour selector panel is
            // a small container around a RadioGroup and its caption controls.
            if (parent.Controls.Count > 6)
                return false;

            foreach (Control child in parent.Controls)
            {
                string name = child.Name ?? string.Empty;
                string childType = child.GetType().Name ?? string.Empty;
                bool allowed = name.Equals("radioGroup2", StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("RDO_GXHN", StringComparison.OrdinalIgnoreCase) ||
                               childType.IndexOf("Label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               childType.IndexOf("RadioGroup", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!allowed)
                    return false;
            }

            return true;
        }

        private static T FindSpModeControl<T>(Control parent, string name) where T : Control
        {
            if (parent == null)
                return null;

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

        /// <summary>
        /// Wire the SP UI after the normal form Load handlers have initialized
        /// the header control. This avoids a field initializer invoking an
        /// instance method before the constructor has run.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            WireSpModeUi();
        }
    }
}
