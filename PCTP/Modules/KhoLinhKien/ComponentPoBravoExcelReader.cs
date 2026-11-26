using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// Đọc file Excel xuất từ Bravo gồm hai sheet: header (vB30BizDoc_Edit) và detail (vB30BizDocDetail_Edit).
    /// Mỗi sheet có dòng 1 là tiêu đề hiển thị, dòng 2 là tên trường (CustomerCode, ItemCode, ...), dữ liệu từ dòng 3.
    /// Sheet được nhận diện theo tên trường chứ không theo tên sheet; các dòng định dạng trống ở cuối bị bỏ qua.
    /// </summary>
    public static class ComponentPoBravoExcelReader
    {
        private const int MaxFieldRowScan = 5;

        /// <summary>True khi workbook có cả sheet header lẫn sheet detail kiểu Bravo.</summary>
        public static bool IsBravoWorkbook(string path)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                return FindTable(package, BravoPoMapper.HeaderRequired) != null
                    && FindTable(package, BravoPoMapper.DetailRequired) != null;
            }
        }

        public static List<BravoPoDocument> Read(string path)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                List<BravoRawRow> header = FindTable(package, BravoPoMapper.HeaderRequired);
                if (header == null)
                    throw new InvalidOperationException("Không thấy sheet header (cần các cột: " +
                        string.Join(", ", BravoPoMapper.HeaderRequired) + ").");
                List<BravoRawRow> detail = FindTable(package, BravoPoMapper.DetailRequired);
                if (detail == null)
                    throw new InvalidOperationException("Không thấy sheet detail (cần các cột: " +
                        string.Join(", ", BravoPoMapper.DetailRequired) + ").");

                return BravoPoMapper.Build(header, detail, Path.GetFileName(path), File.GetLastWriteTime(path));
            }
        }

        /// <summary>Mỗi dòng detail thành một dòng PO; PartDescription/UnitCode để trống, SQL lấy đơn vị từ danh mục IFS.</summary>
        public static List<ComponentPoImportRow> ToImportRows(IEnumerable<BravoPoDocument> documents)
        {
            var rows = new List<ComponentPoImportRow>();
            foreach (BravoPoDocument doc in documents)
                foreach (BravoPoLine line in doc.Lines)
                    rows.Add(new ComponentPoImportRow
                    {
                        SiteCode = doc.SiteCode,
                        PurchaseOrderNo = doc.PurchaseOrderNo,
                        SupplierCode = doc.SupplierCode,
                        OrderDate = doc.OrderDate,
                        LineNo = line.LineNo,
                        PartNo = line.PartNo,
                        OrderQty = line.Quantity,
                        NeedByDate = line.NeedByDate,
                        Remarks = doc.Remarks
                    });
            return rows;
        }

        private static List<BravoRawRow> FindTable(ExcelPackage package, string[] required)
        {
            foreach (ExcelWorksheet ws in package.Workbook.Worksheets)
            {
                if (ws.Dimension == null) continue;
                int lastCol = ws.Dimension.End.Column;
                int lastRow = ws.Dimension.End.Row;
                int scanTo = Math.Min(MaxFieldRowScan, lastRow);

                for (int fieldRow = 1; fieldRow <= scanTo; fieldRow++)
                {
                    var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int c = 1; c <= lastCol; c++)
                    {
                        string name = (ws.Cells[fieldRow, c].Text ?? string.Empty).Trim();
                        if (name.Length > 0 && !columns.ContainsKey(name)) columns[name] = c;
                    }
                    if (!required.All(columns.ContainsKey)) continue;

                    var rows = new List<BravoRawRow>();
                    for (int r = fieldRow + 1; r <= lastRow; r++)
                    {
                        var cells = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        foreach (KeyValuePair<string, int> col in columns)
                            cells[col.Key] = ws.Cells[r, col.Value].Value;
                        var row = new BravoRawRow(r, cells);
                        if (!row.IsEmpty) rows.Add(row);
                    }
                    return rows;
                }
            }
            return null;
        }
    }
}
