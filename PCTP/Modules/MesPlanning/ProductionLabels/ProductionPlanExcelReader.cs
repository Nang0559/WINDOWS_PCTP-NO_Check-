using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// Đọc file Excel kế hoạch sản xuất theo ca (hàng 1 là tiêu đề cột). Dùng chung cho form Upload kế hoạch cũ và nút
    /// "Upload Excel kế hoạch…" của màn hình layout. Cột bắt buộc: SiteCode, PlanNo, PlanDate, LineNo, ShiftCode,
    /// ProductionLineCode, ProductPartNo, PlannedQty, UomCode; PlannedStart / PlannedEnd tùy chọn.
    /// </summary>
    public static class ProductionPlanExcelReader
    {
        public static IList<string> GetSheetNames(string path)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
                return package.Workbook.Worksheets.Select(w => w.Name).ToList();
        }

        public static List<ProductionPlanImportRow> Read(string path, string sheetName)
        {
            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                var ws = package.Workbook.Worksheets[sheetName];
                if (ws == null || ws.Dimension == null) throw new InvalidOperationException("Sheet không có dữ liệu.");
                var headers = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
                for (int c=1;c<=ws.Dimension.End.Column;c++)
                {
                    var h=(ws.Cells[1,c].Text ?? "").Trim();
                    if (h.Length>0) headers[h]=c;
                }
                string[] required={"SiteCode","PlanNo","PlanDate","LineNo","ShiftCode","ProductionLineCode","ProductPartNo","PlannedQty","UomCode"};
                foreach(var h in required) if(!headers.ContainsKey(h)) throw new InvalidOperationException("Thiếu cột bắt buộc: "+h);
                Func<int,string,string> cell=(row,key)=>headers.ContainsKey(key)?(ws.Cells[row,headers[key]].Text ?? "").Trim():null;
                var result=new List<ProductionPlanImportRow>();
                for(int r=2;r<=ws.Dimension.End.Row;r++)
                {
                    if(Enumerable.Range(1,ws.Dimension.End.Column).All(c=>string.IsNullOrWhiteSpace(ws.Cells[r,c].Text))) continue;
                    DateTime planDate;
                    if(!DateTime.TryParse(cell(r,"PlanDate"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out planDate)
                       && !DateTime.TryParse(cell(r,"PlanDate"),out planDate))
                        throw new InvalidOperationException("PlanDate không hợp lệ tại dòng "+r+".");
                    int lineNo; if(!int.TryParse(cell(r,"LineNo"),out lineNo)) throw new InvalidOperationException("LineNo không hợp lệ tại dòng "+r+".");
                    decimal qty; if(!decimal.TryParse(cell(r,"PlannedQty"),NumberStyles.Number,CultureInfo.InvariantCulture,out qty)
                       && !decimal.TryParse(cell(r,"PlannedQty"),out qty)) throw new InvalidOperationException("PlannedQty không hợp lệ tại dòng "+r+".");
                    DateTime start,end;
                    var startText=cell(r,"PlannedStart"); var endText=cell(r,"PlannedEnd");
                    DateTime? startValue=DateTime.TryParse(startText,out start)?(DateTime?)start:null;
                    DateTime? endValue=DateTime.TryParse(endText,out end)?(DateTime?)end:null;
                    result.Add(new ProductionPlanImportRow {
                        SiteCode=cell(r,"SiteCode"),PlanNo=cell(r,"PlanNo"),PlanDate=planDate,LineNo=lineNo,
                        ShiftCode=cell(r,"ShiftCode"),ProductionLineCode=cell(r,"ProductionLineCode"),
                        ProductPartNo=cell(r,"ProductPartNo"),PlannedQty=qty,UomCode=cell(r,"UomCode"),
                        PlannedStart=startValue,PlannedEnd=endValue
                    });
                }
                return result;
            }
        }
    }
}
