using DevExpress.XtraEditors;
using PCTP.Modules.GiaoHangKhach.HVN.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.HVN
{
    public partial class HVN_PGH
    {
        private bool _spModeUiWired;

        // Hook at field-initialization time so this file does not have to modify
        // the legacy designer/constructor. The actual controls exist by Load.
        private readonly object _spModeHook = HookSpModeUi();

        private object HookSpModeUi()
        {
            Load += OnSpModeUiLoad;
            return null;
        }

        private void OnSpModeUiLoad(object sender, EventArgs e)
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

        /// <summary>
        /// SP is a day + DOCK_CODE mode, not an hour-selection mode.
        /// Keep the normal factory/date controls, but remove the hour selectors
        /// from the header while XEM SP is active.
        /// </summary>
        private void ApplySpModeUi(bool isSP)
        {
            var content = _phieuHeaderControl.ContentControl;
            if (content == null)
                return;

            var radioVp = FindControl<Control>(content, "radioGroup2");
            var radioHn = FindControl<Control>(content, "RDO_GXHN");

            if (radioVp != null)
                radioVp.Visible = !isSP;
            if (radioHn != null)
                radioHn.Visible = !isSP;

            // Hide the immediate hour-selector container when it is a dedicated
            // panel. Never hide a TabPane/TabNavigationPage because those are the
            // factory-selection surface for customers having multiple plants.
            SetHourContainerVisibility(radioVp, isSP);
            SetHourContainerVisibility(radioHn, isSP);
        }

        private static void SetHourContainerVisibility(Control radio, bool visible)
        {
            if (radio == null || !IsDedicatedHourContainer(radio.Parent))
                return;

            radio.Parent.Visible = visible;
        }

        private static bool IsDedicatedHourContainer(Control parent)
        {
            if (parent == null || parent is TabControl || parent is UserControl)
                return false;

            string typeName = parent.GetType().Name ?? string.Empty;
            if (typeName.IndexOf("TabNavigationPage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("TabPane", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            // A dedicated hour panel normally contains the RadioGroup plus its
            // caption/labels. Do not collapse larger layout containers.
            if (parent.Controls.Count > 6)
                return false;

            foreach (Control child in parent.Controls)
            {
                if (child == null) continue;
                string name = child.Name ?? string.Empty;
                string childType = child.GetType().Name ?? string.Empty;
                bool hourControl = ReferenceEquals(child, parent) ||
                                    name.Equals("radioGroup2", StringComparison.OrdinalIgnoreCase) ||
                                    name.Equals("RDO_GXHN", StringComparison.OrdinalIgnoreCase) ||
                                    childType.IndexOf("Label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    childType.IndexOf("RadioGroup", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hourControl)
                    return false;
            }

            return true;
        }

        private static T FindControl<T>(Control parent, string name) where T : Control
        {
            if (parent == null)
                return null;

            foreach (Control child in parent.Controls)
            {
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    return child as T;

                var nested = FindControl<T>(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
