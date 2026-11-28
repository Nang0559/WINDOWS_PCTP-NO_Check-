using System.Drawing;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Sơ đồ "Quy trình &amp; luồng dữ liệu quản lý linh kiện MES" 6 cột:
    /// Upload PO → Tem thùng / pallet → Nhập kho → Tiếp nhận yêu cầu → Xuất linh kiện → Tồn theo LotNo &amp; toàn nhà máy.
    /// Khóa ô lệnh: upload, validate, release, print, receive, stock, transfer, issue, wipmove, trace, qc, request, pack, other.
    /// Khóa nhãn: draft, validated, released, partial, received, closed, issued, stockctl.
    /// Ô qc / pack / other chưa có bảng dữ liệu trong schema nên luôn ở trạng thái không thao tác được.
    /// </summary>
    public sealed class MesComponentFlowMapControl : MesFlowCanvasControl
    {
        public MesComponentFlowMapControl()
        {
            var blue = Color.FromArgb(46, 110, 190);
            var green = Color.FromArgb(34, 139, 84);
            var orange = Color.FromArgb(214, 122, 0);
            var purple = Color.FromArgb(96, 84, 170);
            var teal = Color.FromArgb(0, 112, 160);
            var slate = Color.FromArgb(84, 100, 120);
            var gray = Color.FromArgb(110, 120, 135);
            var note = Color.FromArgb(70, 80, 95);

            AddLane("UPLOAD PO LINH KIỆN", 0, 205);
            AddLane("TEM THÙNG / TEM PALLET", 205, 410);
            AddLane("NHẬP KHO LINH KIỆN", 410, 615);
            AddLane("TIẾP NHẬN YÊU CẦU", 615, 900);
            AddLane("XUẤT LINH KIỆN", 900, 1090);
            AddLane("QUẢN LÝ TỒN THEO LOTNO", 1090, MesFlowCanvasControl.CanvasWidth);

            // Cột 0 - Upload PO
            AddBox(22, 56, 160, 46, "Đơn hàng mua linh kiện (PO)", "NCC: Supplier A, B, C");
            AddNode("upload", "UPLOAD PO\nLINH KIỆN", 16, 160, 172, 38, blue);
            AddNode("validate", "KIỂM TRA\nDỮ LIỆU PO", 16, 214, 172, 38, blue);
            AddPill("draft", "DRAFT / NHÁP", 30, 266, 145, 24, gray, false);
            AddPill("validated", "VALIDATED / ĐÃ KIỂM TRA", 22, 300, 160, 24, blue, false);
            AddConnector(false, 102, 102, 102, 160);
            AddConnector(true, 102, 198, 102, 214);
            AddConnector(false, 102, 252, 102, 266);
            AddConnector(false, 102, 290, 102, 300);

            // Cột 1 - Tem thùng / pallet
            AddNode("release", "PHÁT HÀNH TEM THÙNG\n/ TEM PALLET", 225, 160, 165, 44, blue);
            AddPill("released", "RELEASED / ĐÃ PHÁT HÀNH", 230, 214, 155, 24, blue, false);
            AddNote(228, 248, "Tạo mã vạch & QR Code LotNo", false, note);
            AddBox(225, 282, 165, 40, "Mã tem thùng: BOX-LOT-…", "Mã tem pallet: PAL-LOT-…");
            AddNode("print", "IN TEM NHÃN\n(QR / Barcode)", 225, 336, 165, 40, slate);
            AddConnector(true, 188, 233, 207, 233, 207, 182, 225, 182);
            AddConnector(false, 307, 204, 307, 214);
            AddConnector(false, 307, 238, 307, 248);
            AddConnector(true, 307, 264, 307, 282);
            AddConnector(true, 307, 322, 307, 336);

            // Cột 2 - Nhập kho
            AddNode("receive", "NHẬP KHO\nLINH KIỆN", 430, 160, 164, 44, blue);
            AddPill("partial", "PARTIALLY RECEIVED", 432, 214, 160, 24, orange, false);
            AddPill("received", "ĐÃ NHẬP KHO (RECEIVED)", 432, 244, 160, 24, green, false);
            AddPill("closed", "CLOSED / ĐÃ ĐÓNG PO", 432, 274, 160, 24, slate, false);
            AddNote(432, 308, "•  Kiểm đếm số lượng (Qty Check)\n•  Quét nhận LotNo vào kho Part", false, note);
            AddBox(430, 352, 164, 40, "KHO PART", "(Location: Rack A-04)");
            AddConnector(true, 390, 182, 430, 182);
            AddConnector(false, 512, 204, 512, 214);
            AddConnector(false, 512, 298, 512, 308);

            // Cột 3 - Tiếp nhận yêu cầu
            AddGroup(630, 56, 255, 330, "YÊU CẦU LINH KIỆN", "(Requests)");
            AddNode("qc", "YÊU CẦU QC /\nKIỂM ĐỊNH", 645, 120, 225, 44, teal);
            AddNote(648, 167, "Đạt chuẩn / Approved", false, note);
            AddNode("request", "YÊU CẦU\nSẢN XUẤT", 645, 190, 225, 44, green);
            AddNote(648, 237, "Theo BOM / Line", false, note);
            AddNode("pack", "YÊU CẦU\nĐÓNG GÓI", 645, 260, 225, 44, orange);
            AddNote(648, 307, "Vật liệu phụ trợ", false, note);
            AddNode("other", "YÊU CẦU KHÁC\n(Bảo trì / R&D)", 645, 330, 225, 44, gray);
            AddConnector(true, 594, 182, 630, 182);
            AddConnector(true, 885, 182, 915, 182);

            // Cột 4 - Xuất linh kiện
            AddNode("issue", "XUẤT LINH KIỆN", 915, 160, 160, 44, teal);
            AddNode("wipmove", "XUẤT KHO /\nCHUYỂN WIP", 915, 220, 160, 40, blue);
            AddPill("issued", "ĐÃ XUẤT (ISSUED)", 925, 272, 140, 24, green, false);
            AddNote(918, 304, "Chuyển kho từ Part\n→ WIP / Sản xuất", false, note);
            AddBox(915, 350, 160, 40, "Mã phiếu xuất: GI-…", "LotNo tiêu hao: LOT-…");
            AddConnector(true, 995, 204, 995, 220);
            AddConnector(false, 995, 260, 995, 272);

            // Cột 5 - Tồn theo LotNo & toàn nhà máy
            AddNode("stock", "TỒN KHO PART &\nTRA CỨU LOTNO", 1102, 120, 174, 44, green);
            AddNode("transfer", "ĐIỀU CHUYỂN LOT", 1102, 190, 174, 40, purple);
            AddNode("trace", "TRUY VẾT LOTNO\nVẬT TƯ", 1102, 256, 174, 44, slate);
            AddPill("stockctl", "KIỂM SOÁT TỒN THEO LOT", 1108, 320, 162, 24, green, true);
            AddNote(1104, 356, "Tồn chưa tiêu hao sản xuất\n& tồn toàn nhà máy", false, note);
            AddConnector(true, 1075, 182, 1102, 182);
            AddConnector(false, 1189, 164, 1189, 190);
            AddConnector(false, 1189, 230, 1189, 256);
            AddConnector(false, 1189, 300, 1189, 320);

            SetBanner("PO → tem thùng/pallet → nhập kho PART → tiếp nhận yêu cầu → xuất PART → WIP → tồn theo LotNo đều ghi nhật ký workflow/ledger của MES.");
        }

        /// <summary>Sáng nhãn theo mã trạng thái PO (0,10,20,30,40,50,900); -1 = chưa chọn PO.</summary>
        public void ApplyPoStatus(int status)
        {
            SetPill("draft", status == 0);
            SetPill("validated", status == 10);
            SetPill("released", status == 20);
            SetPill("partial", status == 30);
            SetPill("received", status == 40);
            SetPill("closed", status == 50);
            int lane = status == 0 || status == 10 ? 0 : status == 20 ? 1 : status == 30 || status == 40 ? 2 : status == 50 ? 5 : -1;
            SetActiveLane(lane);
        }
    }
}
