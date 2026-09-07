using DevExpress.XtraEditors.DXErrorProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PCTP.Shared.Validation
{
    /// <summary>
    /// Tổng nhiều control số ≤ 1 giá trị max (đọc động — vì max thường là
    /// SL LOT gốc / SL đơn hàng của dòng đang chọn trên grid, thay đổi theo
    /// selection). Dùng cho: UF_TACHLOT (SL1+SL2+SL3 ≤ SL LOT gốc), và mọi
    /// form "chia nhỏ số lượng" tương tự (ghép lot, chia thùng...).
    /// </summary>
    public class SumNotExceedRule : ValidationRule
    {
        private readonly Func<int> _getMax;
        private readonly Func<IEnumerable<Control>> _getParticipants;

        public SumNotExceedRule(Func<int> getMax, Func<IEnumerable<Control>> getParticipants)
        {
            _getMax = getMax;
            _getParticipants = getParticipants;
        }

        public override bool Validate(Control control, object value)
        {
            int sum = _getParticipants()
                .Sum(c => int.TryParse(c.Text, out int v) ? v : 0);
            return sum <= _getMax();
        }
    }

    /// <summary>Bắt buộc phải có giá trị (không rỗng) — thay MessageBox "chưa chọn/nhập gì".</summary>
    public class RequiredTextRule : ValidationRule
    {
        public override bool Validate(Control control, object value)
            => !string.IsNullOrWhiteSpace(control.Text);
    }

    /// <summary>Giá trị số nằm trong [min, max] — ví dụ số lượng phải > 0.</summary>
    public class NumericRangeRule : ValidationRule
    {
        private readonly int _min, _max;
        public NumericRangeRule(int min, int max) { _min = min; _max = max; }

        public override bool Validate(Control control, object value)
            => int.TryParse(control.Text, out int v) && v >= _min && v <= _max;
    }
}