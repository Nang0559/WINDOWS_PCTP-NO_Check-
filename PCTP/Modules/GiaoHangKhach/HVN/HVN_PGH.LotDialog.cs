using DevExpress.XtraBars.Docking2010;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using PCTP.Domain.Events;
using PCTP.Modules.GiaoHangKhach.HVN.Controls;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public void BindGioXuatCheckList(List<string> danhSachGio)
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.BindGioXuatCheckList(danhSachGio);
        }

        public void LockCheckListYMVN()
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.LockCheckListYMVN();
        }

        public void UnlockCheckListYMVN()
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.UnlockCheckListYMVN();
        }

        private void GridViewDONHANG_ShowingEditor_LOT(object sender, CancelEventArgs e)
        {
            if (!_phieuGridControl.IsFocusedLotColumn())
                return;

            e.Cancel = true;
            int stt = GetFocusedDonHangStt();
            if (stt < 0)
                return;

            string status = _phieuGridControl.GetFocusedStatus();
            if (status == "OK")
            {
                ShowInfo("Dòng này đã được Cập Nhập Kho!");
                return;
            }

            string maHang = _phieuGridControl.GetFocusedMaHang();
            int soLuong = _phieuGridControl.GetFocusedQuantity();
            ChonLotThuCongClicked.Invoke(this, new ChonLotThuCongEventArgs(stt, maHang, soLuong));
        }

        public ChonLotResult ShowChonLotTuKho(int stt, string maHang, int soLuong, DataTable danhSachLot)
            => _phieuDialogControl.ShowChonLotTuKho(maHang, soLuong, danhSachLot);

        public List<string> GetCheckedGioXuat()
            => _phieuHeaderControl != null ? _phieuHeaderControl.GetCheckedGioXuat() : new List<string>();

        public void BindGhepLotYMVN(DataTable dt)
            => _phieuBottomStateControl.BindGhepLot(dt);

        public void ShowReportYMVN(DataTable reportData)
            => _phieuDialogControl.ShowReportYMVN(reportData);

        public void XoaDongGiaoDB()
            => _phieuGridControl.DeleteSelectedRows();

        private void radioGroup2_EditValueChanging(object sender, ChangingEventArgs e)
        {
            if ((int)e.NewValue == 8)
                e.Cancel = !_presenter.OnGiaoDBChanging(_presenter.AddNM);
        }

        private void RDO_GXHN_EditValueChanging(object sender, ChangingEventArgs e)
        {
            if ((int)e.NewValue == 10)
                e.Cancel = !_presenter.OnGiaoDBChanging(_presenter.AddNM);
        }

        public void SetDate(DateTime date)
        {
            if (_phieuHeaderControl != null)
            {
                _phieuHeaderControl.SetDate(date);
                return;
            }

            dateNX.DateTime = date;
        }

        public void SuspendGioXuatChanged()
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.SuspendGioXuatChanged();
        }

        public void ResumeGioXuatChanged()
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.ResumeGioXuatChanged();
        }

        public void SetTab(int addNM)
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.SetTab(addNM);
        }

        public void LockDatePicker()
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.LockDatePicker();
        }

        public void UnlockDatePicker()
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.UnlockDatePicker();
        }

        public void LockRadioExcept(string gioFCC)
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.LockRadioExcept(gioFCC);
        }

        public void UnlockAllRadio()
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.UnlockAllRadio();
        }

        public void UpdateGioXuatFromDB(string gioFCC)
        {
            if (_phieuHeaderControl != null)
                _phieuHeaderControl.UpdateGioXuatFromDB(gioFCC);
        }

        public bool HoiXoaDocQR()
            => XtraMessageBox.Show(
                "Dữ liệu không phù hợp:\nDữ liệu đọc QRCode không khớp với phiếu!\nBạn muốn xóa dữ liệu đọc?\n(Nếu không xóa, phiếu giao hàng sẽ không được tải đúng)",
                "Thông Báo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;

        private void UIButtonHOME_ButtonClick(object sender, ButtonEventArgs e)
        {
            switch (((WindowsUIButton)e.Button).Caption)
            {
                case "HOME":
                    SwitchToPhieuView();
                    break;
            }
        }

        private void gridVDOCQRCODE_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            int stt = GetFocusedDocQRStt();
            if (stt < 0)
            {
                _phieuBottomStateControl.HideSuaSoLuong();
                _phieuBottomStateControl.ShowGhepLot();
                return;
            }

            var (lotFcc, slFcc, slHvn) = GetFocusedDocQRTemInfo();
            _sttSuaSl = stt;
            _phieuBottomStateControl.BindSuaSoLuong(BuildSuaSlTable(stt, lotFcc, slFcc, slHvn));
            _phieuBottomStateControl.ShowSuaSoLuong();
            TXT_FCCTU.Text = "";
            TXT_FCCTHANH.Text = "";
            TXT_HVNTU.Text = "";
            TXT_HVNTHANH.Text = "";
            LOTFCCVN.Text = lotFcc;
        }

        private DataTable BuildSuaSlTable(int stt, string lotFcc, int slFcc, int slHvn)
        {
            var tbl = new DataTable();
            tbl.Columns.Add("STT", typeof(int));
            tbl.Columns.Add("LOAI", typeof(string));
            tbl.Columns.Add("LOT", typeof(string));
            tbl.Columns.Add("SLHIEN", typeof(int));
            tbl.Columns.Add("SLTHANH", typeof(int));

            if (!string.IsNullOrEmpty(lotFcc))
            {
                foreach (var part in lotFcc.Split(','))
                {
                    var ls = part.Trim().Split('-');
                    string lot = ls[0].Trim();
                    int sl = ls.Length > 1 && int.TryParse(ls[1], out int v) ? v : slFcc;
                    var row = tbl.NewRow();
                    row["STT"] = stt;
                    row["LOAI"] = "FCC";
                    row["LOT"] = lot;
                    row["SLHIEN"] = sl;
                    row["SLTHANH"] = sl;
                    tbl.Rows.Add(row);
                }
            }

            if (slHvn > 0)
            {
                var row = tbl.NewRow();
                row["STT"] = stt;
                row["LOAI"] = "HVN";
                row["LOT"] = "";
                row["SLHIEN"] = slHvn;
                row["SLTHANH"] = slHvn;
                tbl.Rows.Add(row);
            }

            return tbl;
        }

        private void gridVSUASL_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            var view = sender as GridView;
            if (view == null || view.FocusedRowHandle < 0)
                return;

            string loai = view.GetFocusedRowCellDisplayText("LOAI").Trim();
            string lot = view.GetFocusedRowCellDisplayText("LOT").Trim();
            string slHien = view.GetFocusedRowCellDisplayText("SLHIEN").Trim();

            if (loai == "FCC")
            {
                TXT_FCCTU.Text = slHien;
                TXT_FCCTHANH.Text = slHien;
                TXT_HVNTU.Text = "";
                TXT_HVNTHANH.Text = "";
            }
            else if (loai == "HVN")
            {
                TXT_HVNTU.Text = slHien;
                TXT_HVNTHANH.Text = slHien;
                TXT_FCCTU.Text = "";
                TXT_FCCTHANH.Text = "";
            }

            LOTFCCVN.Text = lot;
        }

        private void cmd_SuaLTemFCC_Click(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0)
            {
                ShowInfo("Vui lòng chọn dòng QR cần sửa!");
                return;
            }

            if (!int.TryParse(TXT_FCCTHANH.Text, out int slMoi) || slMoi <= 0)
            {
                ShowInfo("Số lượng FCC không hợp lệ!");
                return;
            }

            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }

        public int? GetSuaSoLuongResult()
        {
            if (!string.IsNullOrWhiteSpace(TXT_HVNTHANH.Text) &&
                int.TryParse(TXT_HVNTHANH.Text, out int slHvn) && slHvn > 0)
                return slHvn;

            if (!string.IsNullOrWhiteSpace(TXT_FCCTHANH.Text) &&
                int.TryParse(TXT_FCCTHANH.Text, out int slFcc) && slFcc > 0)
                return slFcc;

            return null;
        }

        private void GridViewDONHANG_RowCellStyle(object sender, RowCellStyleEventArgs e)
            => _phieuGridControl.ApplyRowCellStyle(e);

        private void GridViewDONHANG_CellValueChanged(object sender, CellValueChangedEventArgs e) { }
        private void GridViewDONHANG_CellValueChanging(object sender, CellValueChangedEventArgs e) { }
        private void GridViewDONHANG_ClipboardRowCopying(object sender, ClipboardRowCopyingEventArgs e) { }
        private void GridViewDONHANG_PopupMenuShowing(object sender, PopupMenuShowingEventArgs e) { }
        private void GridViewDONHANG_RowUpdated(object sender, RowObjectEventArgs e) { }
        private void GridViewDONHANG_ValidateRow(object sender, ValidateRowEventArgs e) { }
        private void GridViewDONHANG_ValidatingEditor(object sender, BaseContainerValidateEditorEventArgs e) { }
        private void HVN_PGH_ContextMenuStripChanged(object sender, EventArgs e) { }

        private void btnUploadMilkrun_Click(object sender, EventArgs e)
            => UploadMilkrunSPClicked.Invoke(this, EventArgs.Empty);

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_cfg.Delivery.CoGear)
                btnUploadMilkrun.Click -= btnUploadMilkrun_Click;
            else if (_cfg.Delivery.LoadTheoNgay)
                btnUploadMilkrun.Click -= btnUploadMilkrun_Click;

            _presenter.Dispose();
            base.OnFormClosed(e);
        }

        private int GetFocusedDonHangStt()
            => _phieuGridControl.GetFocusedStt();

        private void cmd_SuaSLHVN_Click(object sender, EventArgs e)
        {
            if (_sttSuaSl <= 0)
            {
                ShowInfo("Vui lòng chọn dòng QR cần sửa!");
                return;
            }

            if (!int.TryParse(TXT_HVNTHANH.Text, out int slMoi) || slMoi <= 0)
            {
                ShowInfo("Số lượng HVN không hợp lệ!");
                return;
            }

            SuaSoLuongTemClicked.Invoke(this, EventArgs.Empty);
        }

        private void cmd_SuaLTemFCC_Click_1(object sender, EventArgs e)
            => cmd_SuaLTemFCC_Click(sender, e);
    }

    public class MyWindowsUIButtonPanel : WindowsUIButtonPanel
    {
        public WindowsUIButtonsPanel GetButtonsPanel() => ButtonsPanel;
    }
}
