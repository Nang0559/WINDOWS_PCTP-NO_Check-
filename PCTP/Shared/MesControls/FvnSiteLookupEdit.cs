using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using PCTP.Security;

namespace PCTP.Shared.MesControls
{
    /// <summary>
    /// Ô chọn Site chuẩn của hệ thống: danh sách từ <c>dbo.vFVN_Site</c> (FVN_PCTPSite liên kết IFS.FVN_Site),
    /// giới hạn theo phạm vi dữ liệu của tài khoản. Thay cho ô nhập SiteCode gõ tay / combobox tự điền.
    /// Nạp bằng <see cref="LoadForFunction"/> (theo chức năng) hoặc <see cref="LoadCodes"/> (service đã lọc quyền).
    /// Đổi lựa chọn do người dùng phát <see cref="SiteChanged"/>; nạp bằng code thì không phát sự kiện.
    /// </summary>
    public sealed class FvnSiteLookupEdit : LookUpEdit
    {
        private bool _loading;

        public event EventHandler SiteChanged;

        /// <summary>Thông điệp lỗi của lần nạp gần nhất (rỗng nếu nạp được).</summary>
        public string LastError { get; private set; }

        /// <summary>Số Site thật trong danh sách (không tính dòng "Tất cả").</summary>
        public int SiteCount { get; private set; }

        public FvnSiteLookupEdit()
        {
            LastError = string.Empty;
            Properties.NullText = "Site theo quyền";
            Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            Properties.ValueMember = "SiteCode";
            Properties.DisplayMember = "SiteShort";
            Properties.ShowHeader = true;
            Properties.PopupWidth = 320;
            Properties.DropDownRows = 8;
            Properties.Columns.Add(new LookUpColumnInfo("SiteCode", "Site", 70));
            Properties.Columns.Add(new LookUpColumnInfo("SiteName", "Tên Site (IFS)", 230));
            EditValueChanged += (s, e) =>
            {
                if (_loading) return;
                EventHandler handler = SiteChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            };
        }

        /// <summary>True khi đang chọn dòng "Tất cả Site".</summary>
        public bool IsAllSelected
        {
            get { return string.Equals(Convert.ToString(EditValue), FvnSiteDirectory.AllSiteCode, StringComparison.Ordinal); }
        }

        /// <summary>Mã Site đang chọn; rỗng khi chưa chọn hoặc đang chọn "Tất cả Site".</summary>
        public string SiteCode
        {
            get
            {
                if (EditValue == null || EditValue == DBNull.Value || IsAllSelected) return string.Empty;
                return (Convert.ToString(EditValue) ?? string.Empty).Trim();
            }
            set
            {
                string code = (value ?? string.Empty).Trim();
                if (code.Length == 0) { EditValue = null; return; }
                EditValue = FindCode(code) ?? EditValue;
            }
        }

        /// <summary>Nạp Site tài khoản được xem cho chức năng; trả về số Site. Lỗi được ghi vào <see cref="LastError"/>.</summary>
        public int LoadForFunction(string functionCode, bool includeAll = false, string preferred = null)
        {
            try