using DevExpress.XtraEditors;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Modules.XuLyHangLoi
{
    /// <summary>
    /// Một Slot đích trong danh sách chọn khi nhập lại hàng (OK / NG) sau rework.
    /// </summary>
    public sealed class SlotDichLookupItem
    {
        public int SlotId { get; set; }
        public string Kho { get; set; }
        public string Rack { get; set; }
        public int SlotNumber { get; set; }
        public string MaHangHienCo { get; set; }
        public int SoLuongHienCo { get; set; }

        /// <summary>Text hiển thị trên ô đã chọn: "KHO / RACK / Slot n".</summary>
        public string ViTri
        {
            get { return Kho + " / " + Rack + " / Slot " + SlotNumber; }
        }
    }

    /// <summary>
    /// Chọn Slot đích từ danh sách Slot THẬT trong kho thay cho việc gõ số SlotId
    /// (gõ sai Id → vi phạm FK_SlotLot_Slot khi nhập tồn).
    /// </summary>
    public static class SlotDichLookup
    {
        public static List<SlotDichLookupItem> Build(ISlotService slotService)
        {
            if (slotService == null)
                return new List<SlotDichLookupItem>();

            return (slotService.GetAllSlots() ?? new List<KhoCore.Models.Slot>())
                .Select(s => new SlotDichLookupItem
                {
                    SlotId = s.SlotId,
                    Kho = s.whname,
                    Rack = s.RackName,
                    SlotNumber = s.SlotNumber,
                    MaHangHienCo = s.ItemCode,
                    SoLuongHienCo = s.Quantity
                })
                .ToList();
        }

        public static SearchLookUpEdit Create(List<SlotDichLookupItem> items)
        {
            var look = new SearchLookUpEdit { Dock = DockStyle.Fill };
            look.Properties.NullText = string.Empty;
            look.Properties.ShowClearButton = true;
            look.Properties.ImmediatePopup = true;
            look.Properties.PopupFormSize = new System.Drawing.Size(640, 380);
            look.Properties.DataSource = items;
            look.Properties.ValueMember = nameof(SlotDichLookupItem.SlotId);
            look.Properties.DisplayMember = nameof(SlotDichLookupItem.ViTri);
            look.Properties.PopulateViewColumns();

            var view = look.Properties.View;
            view.OptionsView.ShowAutoFilterRow = false;
            view.OptionsFind.AlwaysVisible = true;
            view.OptionsFind.FindNullPrompt = "Tìm theo kho, rack, slot, mã hàng...";
            view.OptionsFind.ShowCloseButton = false;

            Cot(view, nameof(SlotDichLookupItem.SlotId), "Id", false, 0);
            Cot(view, nameof(SlotDichLookupItem.ViTri), "Vị trí", false, 0);
            Cot(view, nameof(SlotDichLookupItem.Kho), "Kho", true, 120);
            Cot(view, nameof(SlotDichLookupItem.Rack), "Rack", true, 120);
            Cot(view, nameof(SlotDichLookupItem.SlotNumber), "Slot", true, 60);
            Cot(view, nameof(SlotDichLookupItem.MaHangHienCo), "Mã hàng hiện có", true, 160);
            Cot(view, nameof(SlotDichLookupItem.SoLuongHienCo), "SL hiện có", true, 90);

            return look;
        }

        private static void Cot(
            DevExpress.XtraGrid.Views.Grid.GridView view,
            string field, string caption, bool visible, int width)
        {
            var col = view.Columns[field];
            if (col == null) return;

            col.Caption = caption;
            col.Visible = visible;
            if (visible && width > 0)
                col.Width = width;
        }

        /// <summary>SlotId đang chọn; null nếu chưa chọn.</summary>
        public static int? GetSlotId(SearchLookUpEdit look)
        {
            object v = look == null ? null : look.EditValue;
            if (v == null || v == DBNull.Value) return null;

            int id;
            return int.TryParse(Convert.ToString(v), out id) && id > 0 ? (int?)id : null;
        }
    }

    /// <summary>Hộp thoại nhỏ chọn Slot nhập NG (dùng khi nhập kho NG hàng khách trả).</summary>
    public static class SlotNhapNGPicker
    {
        /// <summary>Trả SlotId đã chọn; null nếu người dùng hủy.</summary>
        public static int? Chon(IWin32Window owner, ISlotService slotService, string tieuDe)
        {
            using (var f = new XtraForm())
            {
                f.Text = tieuDe ?? "Chọn Slot nhập NG";
                f.StartPosition = FormStartPosition.CenterParent;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ClientSize = new System.Drawing.Size(520, 120);

                var lbl = new LabelControl { Text = "Slot nhập hàng NG (cách ly):", Left = 12, Top = 14, AutoSizeMode = LabelAutoSizeMode.None, Width = 480 };
                var look = SlotDichLookup.Create(SlotDichLookup.Build(slotService));
                look.Dock = DockStyle.None;
                look.Left = 12; look.Top = 38; look.Width = 496;

                var ok = new SimpleButton { Text = "Nhập kho NG", Left = 300, Top = 80, Width = 120 };
                var cancel = new SimpleButton { Text = "Hủy", Left = 428, Top = 80, Width = 80, DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) =>
                {
                    if (!SlotDichLookup.GetSlotId(look).HasValue)
                    {
                        XtraMessageBox.Show(f, "Vui lòng chọn Slot nhập NG.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    f.DialogResult = DialogResult.OK;
                };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] { lbl, look, ok, cancel });

                return f.ShowDialog(owner) == DialogResult.OK ? SlotDichLookup.GetSlotId(look) : null;
            }
        }
    }
}
