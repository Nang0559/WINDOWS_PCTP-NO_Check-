using System.Collections.Generic;
using PCTP.Shared.Helpers;

namespace PCTP.Modules.MesPlanning
{
    /// <summary>
    /// File mẫu cho "Upload Excel kế hoạch…" / "Upload kế hoạch sản xuất theo ca". Cột khớp <c>ProductionPlanExcelReader</c>
    /// (bắt buộc: SiteCode, PlanNo, PlanDate, LineNo, ShiftCode, ProductionLineCode, ProductPartNo, PlannedQty, UomCode).
    /// </summary>
    public static class ProductionPlanTemplateDefinition
    {
        public const string SheetName = "PLAN";

        public static ExcelTemplateDefinition Create()
        {
            var columns = new List<ExcelTemplateColumn>
            {
                new ExcelTemplateColumn("SiteCode", true, "Mã Site sản xuất. Phải thuộc Site bạn có quyền tạo (CREATE).", "FVN", 12, null, true),
                new ExcelTemplateColumn("PlanNo", true, "Số kế hoạch. Nhiều dòng cùng PlanNo thuộc một kế hoạch.", "KH-2026-1201", 18, null, true),
                new ExcelTemplateColumn("PlanDate", true, "Ngày kế hoạch (yyyy-mm-dd).", "2026-12-01", 14, "yyyy-mm-dd"),
                new ExcelTemplateColumn("LineNo", true, "Số thứ tự dòng trong kế hoạch (số nguyên).", "1", 9, "0"),
                new ExcelTemplateColumn("ShiftCode", true, "Mã ca sản xuất.", "CA1", 12, null, true),
                new ExcelTemplateColumn("ProductionLineCode", true, "Mã chuyền / line sản xuất.", "LINE01", 18, null, true),
                new ExcelTemplateColumn("ProductPartNo", true, "Mã thành phẩm cần sản xuất.", "FG-0001", 18, null, true),
                new ExcelTemplateColumn("PlannedQty", true, "Số lượng kế hoạch (số).", "500", 12),
                new ExcelTemplateColumn("UomCode", true, "Đơn vị tính.", "PCS", 10, null, true),
                new ExcelTemplateColumn("PlannedStart", false, "Giờ bắt đầu dự kiến (yyyy-mm-dd hh:mm, tùy chọn).", "2026-12-01 07:30", 18, "yyyy-mm-dd hh:mm"),
                new ExcelTemplateColumn("PlannedEnd", false, "Giờ kết thúc dự kiến (yyyy-mm-dd hh:mm, tùy chọn).", "2026-12-01 16:00", 18, "yyyy-mm-dd hh:mm")
            };
            var notes = new List<string>
            {
                "Điền dữ liệu vào sheet \"" + SheetName + "\" từ hàng 2; hàng 1 là tiêu đề, không đổi tên cột (không phân biệt hoa thường).",
                "Cột nền cam là bắt buộc, cột nền xám là tùy chọn. Có thể thêm cột khác, hệ thống bỏ qua cột lạ.",
                "Upload tạo kế hoạch ở trạng thái Mới upload; sau đó Kiểm tra → Chốt kế hoạch trên màn hình Kế hoạch sản xuất.",
                "Toàn bộ file chạy trong một transaction: một dòng lỗi thì cả file bị hủy.",
                "Dòng trống được bỏ qua. Không để dòng ví dụ trong sheet \"" + SheetName + "\"."
            };
            return new ExcelTemplateDefinition("Template upload kế hoạch sản xuất theo ca",
                "Template_KeHoachSanXuat.xlsx", SheetName, columns, notes);
        }
    }
}
