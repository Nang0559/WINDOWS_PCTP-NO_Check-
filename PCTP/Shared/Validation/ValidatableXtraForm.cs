using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using System.Windows.Forms;

namespace PCTP.Shared.Validation
{
    /// <summary>
    /// Base form cho mọi subform cần validate chéo giữa các control (kiểu
    /// UF_TACHLOT). Gói sẵn 1 DXValidationProvider dùng chung + helper AddRule,
    /// form con chỉ cần khai báo rule nào gắn vào control nào.
    /// </summary>
    public abstract class ValidatableXtraForm : XtraForm
    {
        protected readonly DXValidationProvider Validator = new DXValidationProvider();

        /// <summary>Gắn 1 rule vào 1 control, kèm text lỗi hiển thị.</summary>
        protected void AddRule(Control control, ValidationRule rule,
            string errorText, ErrorType errorType = ErrorType.Critical)
        {
            rule.ErrorText = errorText;
            rule.ErrorType = errorType;
            Validator.SetValidationRule(control, rule);
        }

        /// <summary>Validate toàn bộ control đã gắn rule — gọi trước khi Lưu/Sửa.</summary>
        protected bool ValidateAll() => Validator.Validate();

        protected override void Dispose(bool disposing)
        {
            if (disposing) Validator.Dispose();
            base.Dispose(disposing);
        }
    }
}