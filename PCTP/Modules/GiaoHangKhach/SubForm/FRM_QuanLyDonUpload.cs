using DevExpress.XtraEditors;
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach.Controls;
using System;
using System.Windows.Forms;

namespace PCTP.Modules.GiaoHangKhach.SubForm
{
    /// <summary>Cửa sổ chứa <see cref="UploadedOrderManagerControl"/>: xem / sửa / xóa đơn đã upload.</summary>
    public class FRM_QuanLyDonUpload : XtraForm
    {
        private readonly UploadedOrderManagerControl _ctrl = new UploadedOrderManagerControl();

        /// <summary>Có lưu thay đổi ít nhất một lần (để form gọi biết cần nạp lại lưới đơn).</summary>
        public bool DaThayDoi { get; private set; }

        public FRM_QuanLyDonUpload(SQLPROVIDER sql, string orderTable, string title)
        {
            Text = string.IsNullOrWhiteSpace(title) ? $"Đơn đã upload — {orderTable}" : title;
            Size = new System.Drawing.Size(1150, 650);
            StartPosition = FormStartPosition.CenterParent;

            _ctrl.DaLuu += (s, e) => DaThayDoi = true;
            Controls.Add(_ctrl);
            _ctrl.Configure(sql.B7R2_FCCdb, orderTable);
        }
    }
}
