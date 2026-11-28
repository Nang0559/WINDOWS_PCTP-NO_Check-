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
            {
                return Bind(FvnSiteDirectory.ForFunction(functionCode, includeAll), preferred);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        /// <summary>Nạp Site nằm trong phạm vi của tất cả các chức năng (màn hình dùng nhiều quyền).</summary>
        public int LoadForFunctions(IEnumerable<string> functionCodes, bool includeAll = false, string preferred = null)
        {
            try
            {
                return Bind(FvnSiteDirectory.ForFunctions(functionCodes, includeAll), preferred);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        /// <summary>Nạp theo danh sách mã Site mà service đã lọc quyền; tên Site lấy từ dbo.vFVN_Site.</summary>
        public int LoadCodes(IEnumerable<string> authorizedCodes, bool includeAll = false, string preferred = null)
        {
            try
            {
                return Bind(FvnSiteDirectory.ForCodes(authorizedCodes, includeAll), preferred);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        private int Fail(Exception ex)
        {
            Bind(FvnSiteDirectory.CreateTable(), null);
            LastError = ex.Message;
            return 0;
        }

        private int Bind(DataTable table, string preferred)
        {
            _loading = true;
            try
            {
                LastError = string.Empty;
                Properties.DataSource = table;
                SiteCount = table.Rows.Cast<DataRow>().Count(r =>
                    !string.Equals(Convert.ToString(r["SiteCode"]), FvnSiteDirectory.AllSiteCode, StringComparison.Ordinal));

                string pick = FindCode(preferred);
                if (pick == null)
                {
                    DataRow first = table.Rows.Cast<DataRow>().FirstOrDefault(r =>
                        !string.Equals(Convert.ToString(r["SiteCode"]), FvnSiteDirectory.AllSiteCode, StringComparison.Ordinal));
                    pick = first == null ? null : Convert.ToString(first["SiteCode"]);
                }
                EditValue = pick;
                // Chỉ có một Site trong phạm vi: không cần mở danh sách.
                Properties.ReadOnly = SiteCount <= 1;
            }
            finally { _loading = false; }
            return SiteCount;
        }

        private string FindCode(string code)
        {
            code = (code ?? string.Empty).Trim();
            if (code.Length == 0) return null;
            DataTable table = Properties.DataSource as DataTable;
            if (table == null) return null;
            foreach (DataRow row in table.Rows)
            {
                string value = Convert.ToString(row["SiteCode"]);
                if (string.Equals(value, code, StringComparison.OrdinalIgnoreCase)) return value;
            }
            return null;
        }
    }
}
