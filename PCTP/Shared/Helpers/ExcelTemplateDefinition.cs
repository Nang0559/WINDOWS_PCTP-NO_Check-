using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Shared.Helpers
{
    /// <summary>Một cột của file Excel mẫu. <see cref="Name"/> phải trùng đúng tên cột mà reader tìm ở hàng 1.</summary>
    public sealed class ExcelTemplateColumn
    {
        public ExcelTemplateColumn(string name, bool required, string description, string example,
            int width = 16, string numberFormat = null, bool isText = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tên cột là bắt buộc.", "name");
            Name = name;
            Required = required;
            Description = description ?? string.Empty;
            Example = example ?? string.Empty;
            Width = width;
            NumberFormat = numberFormat;
            IsText = isText;
        }

        public string Name { get; private set; }
        public bool Required { get; private set; }
        public string Description { get; private set; }
        public string Example { get; private set; }
        public int Width { get; private set; }
        /// <summary>Định dạng Excel áp cho các ô dữ liệu (ví dụ yyyy-mm-dd). Null = General.</summary>
        public string NumberFormat { get; private set; }
        /// <summary>Ép ô là Text để Excel không đổi mã (000123 → 123).</summary>
        public bool IsText { get; private set; }
    }

    /// <summary>
    /// Mô tả file Excel mẫu: sheet dữ liệu (chỉ có hàng tiêu đề, KHÔNG có dòng ví dụ để người dùng lỡ upload nguyên file
    /// không bị nhập dữ liệu mẫu) và sheet hướng dẫn. Không phụ thuộc EPPlus / WinForms nên test được trực tiếp.
    /// </summary>
    public sealed class ExcelTemplateDefinition
    {
        public ExcelTemplateDefinition(string title, string defaultFileName, string dataSheetName,
            IList<ExcelTemplateColumn> columns, IList<string> notes)
        {
            if (columns == null || columns.Count == 0) throw new ArgumentException("Cần ít nhất một cột.", "columns");
            if (string.IsNullOrWhiteSpace(dataSheetName)) throw new ArgumentException("Tên sheet là bắt buộc.", "dataSheetName");
            Title = title ?? string.Empty;
            DefaultFileName = string.IsNullOrWhiteSpace(defaultFileName) ? "Template.xlsx" : defaultFileName;
            DataSheetName = dataSheetName;
            Columns = columns.ToList().AsReadOnly();
            Notes = (notes ?? new List<string>()).ToList().AsReadOnly();
        }

        public string Title { get; private set; }
        public string DefaultFileName { get; private set; }
        /// <summary>Sheet dữ liệu, luôn là sheet đầu tiên (form upload cũ lấy sheet đầu làm mặc định).</summary>
        public string DataSheetName { get; private set; }
        public IReadOnlyList<ExcelTemplateColumn> Columns { get; private set; }
        public IReadOnlyList<string> Notes { get; private set; }

        public IEnumerable<string> RequiredColumnNames
        {
            get { return Columns.Where(c => c.Required).Select(c => c.Name); }
        }
    }
}
