using System.Collections.Generic;
using PCTP.Shared.Helpers;

namespace PCTP.Modules.KhoLinhKien
{
    /// <summary>
    /// File mẫu cho "Upload Excel PO…". Cột và thứ tự khớp <c>ComponentPoExcelReader</c> (cột bắt buộc: SiteCode, PurchaseOrderNo,
    /// SupplierCode, OrderDate, LineNo, PartNo, OrderQty). File xuất từ Bravo (sheet header + detail) vẫn upload được như cũ.
    /// </summary>
    public static class ComponentPoTemplateDefinition
    {
        public const string SheetName = "PO";

        public static ExcelTemplateDefinition Create()
        {
            var columns = new List<ExcelTemplateColumn>
            {
                new ExcelTemplateColumn("SiteCode", true, "Mã Site nhận hàng. Phải thuộc Site bạn có quyền tạo (CREATE).", "FVN", 12, null, true),
                new ExcelTemplateColumn("PurchaseOrderNo", true, "Số PO. Nhiều dòng hàng cùng số PO thuộc một PO.", "PO-2026-0001", 20, null, true),
                new ExcelTemplateColumn("SupplierCode", true, "Mã nhà cung cấp.", "NCC001", 16, null, true),
                new ExcelTemplateColumn("OrderDate", true, "Ngày đặt (dd/mm/yyyy).", "01/12/2026", 14, "dd/mm/yyyy"),
                new ExcelTemplateColumn("LineNo", true, "Số thứ tự dòng trong PO (số nguyên).", "1", 9, "0"),
                new ExcelTemplateColumn("PartNo", true, "Mã linh kiện. Phải có trong danh mục IFS của Site, nếu không upload bị chặn.", "PART-0001", 20, null, true),
                new ExcelTemplateColumn("PartDescription", false, "Mô tả linh kiện (tùy chọn).", "Bánh răng nhựa 12T", 30, null, true),
                new ExcelTemplateColumn("OrderQty", true, "Số lượng đặt (số).", "1000", 12),
                new ExcelTemplateColumn("UnitCode", false, "Đơn vị tính (tùy chọn).", "PCS", 10, null, true),
                new ExcelTemplateColumn("NeedByDate", false, "Ngày cần hàng (dd/mm/yyyy, tùy chọn).", "15/12/2026", 14, "dd/mm/yyyy"),
                new ExcelTemplateColumn("Remarks", false, "Ghi chú (tùy chọn).", "", 30, null, true)
            };
            var notes = new List<string>
            {
                "Điền dữ liệu vào sheet \"" + SheetName + "\" từ hàng 2; hàng 1 là tiêu đề, không đổi tên cột (không phân biệt hoa thường).",
                "Cột nền cam là bắt buộc, cột nền xám là tùy chọn. Có thể thêm cột khác, hệ thống bỏ qua cột lạ.",
                "Upload tạo PO ở trạng thái Nháp (hoặc thay thế PO Nháp cùng số). PO đã Kiểm tra / Phát hành không bị ghi đè.",
                "Toàn bộ file chạy trong một transaction: một dòng lỗi thì cả file bị hủy.",
                "Dòng trống được bỏ qua. Không để dòng ví dụ trong sheet \"" + SheetName + "\"."
            };
            return new ExcelTemplateDefinition("Template upload yêu cầu mua hàng linh kiện (PO)",
                "Template_DatHangLinhKien.xlsx", SheetName, columns, notes);
        }
    }
}
