using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace PCTP.Shared.Helpers
{
    /// <summary>Ghi file Excel mẫu từ <see cref="ExcelTemplateDefinition"/>: sheet dữ liệu (chỉ tiêu đề) + sheet HUONG_DAN.</summary>
    public static class ExcelTemplateWriter
    {
        public const string GuideSheetName = "HUONG_DAN";
        private const int FormattedRows = 2000;

        public static void Write(string path, ExcelTemplateDefinition def)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Thiếu đường dẫn file.", "path");
            if (def == null) throw new ArgumentNullException("def");

            using (var package = new ExcelPackage())
            {
                // Sheet dữ liệu phải đứng đầu: form upload cũ chọn sheet đầu làm mặc định.
                ExcelWorksheet ws = package.Workbook.Worksheets.Add(def.DataSheetName);
                for (int i = 0; i < def.Columns.Count; i++)
                {
                    ExcelTemplateColumn col = def.Columns[i];
                    int c = i + 1;
                    ws.Column(c).Width = col.Width;

                    // Định dạng ô dữ liệu trước (hàng 2 trở xuống) để ngày / mã người dùng nhập vào được đọc đúng.
                    ExcelRange body = ws.Cells[2, c, FormattedRows, c];
                    if (col.IsText) body.Style.Numberformat.Format = "@";
                    else if (!string.IsNullOrEmpty(col.NumberFormat)) body.Style.Numberformat.Format = col.NumberFormat;

                    ExcelRange head = ws.Cells[1, c];
                    head.Value = col.Name;   // đúng tên cột reader tìm, không thêm dấu * hay ghi chú vào tiêu đề
                    head.Style.Font.Bold = true;
                    head.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    head.Style.Fill.BackgroundColor.SetColor(col.Required ? Color.FromArgb(255, 192, 0) : Color.FromArgb(217, 217, 217));
                    head.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }
                ws.Cells[1, 1, 1, def.Columns.Count].AutoFilter = true;
                ws.View.FreezePanes(2, 1);

                WriteGuide(package.Workbook.Worksheets.Add(GuideSheetName), def);
                package.SaveAs(new FileInfo(path));
            }
        }

        private static void WriteGuide(ExcelWorksheet g, ExcelTemplateDefinition def)
        {
            int r = 1;
            g.Cells[r, 1].Value = def.Title;
            g.Cells[r, 1].Style.Font.Bold = true;
            g.Cells[r, 1].Style.Font.Size = 14;
            r += 2;
            foreach (string note in def.Notes)
            {
                g.Cells[r, 1].Value = "• " + note;
                r++;
            }
            r++;

            string[] headers = { "Cột", "Bắt buộc", "Mô tả", "Ví dụ" };
            for (int c = 0; c < headers.Length; c++)
            {
                ExcelRange h = g.Cells[r, c + 1];
                h.Value = headers[c];
                h.Style.Font.Bold = true;
                h.Style.Fill.PatternType = ExcelFillStyle.Solid;
                h.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(221, 235, 247));
            }
            r++;
            foreach (ExcelTemplateColumn col in def.Columns)
            {
                g.Cells[r, 1].Value = col.Name;
                g.Cells[r, 2].Value = col.Required ? "Có" : "Không";
                g.Cells[r, 3].Value = col.Description;
                g.Cells[r, 4].Style.Numberformat.Format = "@";
                g.Cells[r, 4].Value = col.Example;
                r++;
            }
            g.Column(1).Width = 24;
            g.Column(2).Width = 11;
            g.Column(3).Width = 80;
            g.Column(4).Width = 22;
        }
    }

    /// <summary>Hộp thoại "Lưu template…" dùng chung cho các màn hình upload Excel.</summary>
    public static class ExcelTemplateFlow
    {
        /// <summary>Trả về dòng trạng thái, hoặc null khi người dùng hủy. Ném lỗi khi không ghi được file.</summary>
        public static string Run(IWin32Window owner, ExcelTemplateDefinition def)
        {
            if (def == null) throw new ArgumentNullException("def");

            string path;
            using (var dlg = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                Title = "Lưu file template",
                FileName = def.DefaultFileName,
                DefaultExt = "xlsx",
                AddExtension = true,
                OverwritePrompt = true
            })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                path = dlg.FileName;
            }

            try { ExcelTemplateWriter.Write(path, def); }
            catch (IOException ex)
            {
                throw new InvalidOperationException("Không ghi được file (đang mở trong Excel?): " + path + "\r\n" + ex.Message, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException("Không có quyền ghi file: " + path + "\r\n" + ex.Message, ex);
            }

            if (XtraMessageBox.Show(owner, "Đã tạo template:\r\n" + path + "\r\n\r\nMở file ngay?", def.Title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
                catch (Exception) { /* mở không được thì thôi, file đã lưu */ }
            }
            return "Đã tạo template: " + path;
        }
    }
}
