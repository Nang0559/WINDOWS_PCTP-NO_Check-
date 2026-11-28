using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraGrid.Views.Grid;

namespace PCTP.Shared.MesControls
{
    /// <summary>
    /// Quy ước giao diện chung cho các màn hình MES / Kho linh kiện: nền sáng, tiêu đề + thanh trạng thái đáy,
    /// tiêu đề cột tiếng Việt. Chỉ trình bày, không chứa nghiệp vụ.
    /// </summary>
    public static class FvnConsoleTheme
    {
        /// <summary>Nền vùng nội dung (thẻ / lưới trắng nổi lên trên nền này).</summary>
        public static readonly Color Background = Color.FromArgb(243, 246, 250);

        /// <summary>Đặt nền sáng cho khung bố cục gốc của màn hình.</summary>
        public static void ApplyBackground(Control root)
        {
            if (root != null) root.BackColor = Background;
        }

        /// <summary>
        /// Đặt tiêu đề cột theo cặp (FieldName, Caption); cột không có trong lưới thì bỏ qua,
        /// cột không khai báo giữ tiêu đề mặc định của DevExpress.
        /// </summary>
        public static void ApplyCaptions(GridView view, IDictionary<string, string> captions)
        {
            if (view == null || captions == null) return;
            foreach (KeyValuePair<string, string> pair in captions)
            {
                var column = view.Columns[pair.Key];
                if (column != null) column.Caption = pair.Value;
            }
        }
    }
}