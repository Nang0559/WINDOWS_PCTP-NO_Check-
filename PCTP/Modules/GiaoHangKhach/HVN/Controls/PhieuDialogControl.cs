using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraReports.UI;
using PCTP.QRCODE_HVN.Report;
using PCTP.Modules.GiaoHangKhach.SubForm;
using PCTP.QRCODE_HVN;
using PCTP.Modules.GiaoHangKhach.HVN.SubForm;
using PCTP.Domain.Events;

namespace PCTP.Modules.GiaoHangKhach.HVN.Controls
{
    /// <summary>
    /// Owns transient dialog/report presentation for the delivery-phieu screen.
    /// Business decisions remain in Presenter/Service; this control only creates and shows UI.
    /// </summary>
    public sealed class PhieuDialogControl : XtraUserControl
    {
        public DialogResult ShowModal(Form form, IWin32Window owner = null)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));
            using (form)
                return owner == null ? form.ShowDialog() : form.ShowDialog(owner);
        }

        public int ShowChonSttTrungMa(DataTable danhSachTrung)
        {
            if (danhSachTrung == null || danhSachTrung.Rows.Count == 0)
                return -1;

            var danhSach = new ListView();
            foreach (DataRow row in danhSachTrung.Rows)
            {
                danhSach.Items.Add(new ListViewItem(new[]
                {
                    row["STT"].ToString(), row["GIOGIAO"].ToString(),
                    row["MAHANG"].ToString(), row["TENHANG"].ToString(),
                    row["SOLUONG"].ToString(), row["STATUS"].ToString()
                }));
            }

            return ShowChonSttTrungMa(danhSach);
        }

        public int ShowChonSttTrungMa(ListView danhSachTrung)
        {
            using (var form = new FRM_LISTRUNGMSL(danhSachTrung))
            {
                form.ShowDialog();
                if (string.IsNullOrWhiteSpace(FRM_LISTRUNGMSL.STTPHIEU)) return -1;
                return int.TryParse(FRM_LISTRUNGMSL.STTPHIEU, out var stt) ? stt : -1;
            }
        }

        public void ShowKiemTraMaNG(string maHang)
        {
            using (var form = new FRM_SUALOTHVN(maHang))
                form.ShowDialog();
        }

        public void ShowTachLot()
        {
            var form = new UF_TACHLOT();
            form.Show();
        }

        public void ShowLoiCapNhapKho(DataTable errors, Action temporarilyDisableOwner)
        {
            if (errors == null) return;
            temporarilyDisableOwner?.Invoke();
            var form = new frm_err_cnk(errors)
            {
                StartPosition = FormStartPosition.CenterParent
            };
            form.FormClosed += (s, e) =>
            {
                if (ParentForm != null)
                {
                    ParentForm.Enabled = true;
                    ParentForm.Activate();
                }
                form.Dispose();
            };
            form.Show(ParentForm);
        }

        public int ShowChonHinhThucIn()
        {
            using (var form = new FRM_HTIN())
            {
                form.ShowDialog();
                return form.HinhThucIn;
            }
        }

        public ChonLotResult ShowChonLotTuKho(string maHang, int soLuong, DataTable danhSachLot)
        {
            using (var form = new FRM_CHON_LOT_KHO(maHang, soLuong, danhSachLot))
            {
                if (form.ShowDialog() != DialogResult.OK)
                    return new ChonLotResult { Confirmed = false };

                return new ChonLotResult
                {
                    LotGhep = form.LotGhep,
                    Confirmed = true
                };
            }
        }

        public void ShowReport(DataTable reportData)
        {
            var report = new rpPhieuGiaoHang { DataSource = reportData };
            new ReportPrintTool(report).ShowPreviewDialog();
        }

        public void ShowReportWithGioHeader(DataTable reportData, string gioHeader)
        {
            var report = new rpPhieuGiaoHang { DataSource = reportData };
            report.SetGioHeader(gioHeader);
            new ReportPrintTool(report).ShowPreviewDialog();
        }

        public void ShowReportYMVN(DataTable reportData)
        {
            var report = new rpPhieuGiaoHangYAM { DataSource = reportData };
            new ReportPrintTool(report).ShowPreviewDialog();
        }
    }
}
