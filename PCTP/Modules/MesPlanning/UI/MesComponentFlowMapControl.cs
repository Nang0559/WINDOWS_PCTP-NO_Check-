\xef\xbb\xbfusing System.Drawing;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Sơ đồ "Quy trình &amp; luồng dữ liệu quản lý linh kiện PART" 5 cột:
    /// Yêu cầu mua hàng (PO) → Phát hành PO &amp; LOT/QR → Nhận linh kiện (QR) → Tồn kho &amp; điều chuyển → Cấp liệu &amp; truy xuất.
    /// Khóa ô lệnh: upload, validate, release, receive, stock, transfer, issue, trace.
    /// Khóa nhãn: draft, validated, released, partial, received, closed.
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

            AddLane("YÊU CẦU MUA HÀNG (PO)", 0, 233);
            AddLane("PHÁT HÀNH PO & LOT/QR", 233, 438);
            AddLane("NHẬN LINH KIỆN (QR)", 438, 644);
            AddLane("TỒN KHO & ĐIỀU CHUYỂN", 644, 1059);
            AddLane("CẤP LIỆU & TRUY XUẤT", 1059, MesFlowCanvasControl.CanvasWidth);

            // Cột 0 - PO
            AddBox(45, 56, 157, 48, "File Excel PO", "(Bravo / template PO)");
            AddNode("upload", "UPLOAD EXCEL PO", 34, 140, 179, 40, blue);
            AddPill("draft", "DRAFT / NHÁP", 66, 192, 115, 24, gray, false);
            AddNode("validate", "KIỂM TRA PO", 34, 250, 179, 40, blue);
            AddPill("validated", "VALIDATED / ĐÃ KIỂM TRA", 40, 302, 167, 24, blue, false);
            AddConnector(true, 123, 104, 123, 140);
            AddConnector(false, 123, 180, 123, 192);
            AddConnector(true, 123, 216, 123, 250);
            AddConnector(false, 123, 290, 123, 302);

            // Cột 1 - Phát hành
            AddNode("release", "PHÁT HÀNH PO\n+ SINH LOT/QR", 253, 192, 165, 44, blue);
            AddPill("released", "RELEASED / ĐÃ PHÁT HÀNH", 256, 253, 160, 24, blue, false);
            AddBox(265, 303, 142, 25, "Tạo LOT & tem QR", null);
            AddConnector(true, 207, 314, 225, 314, 225, 214, 253, 214);
            AddConnector(false, 335, 236, 335, 253);
            AddConnector(true, 335, 277, 335, 303);

            // Cột 2 - Nhận hàng
            AddNode("receive", "NHẬN LINH KIỆN\n(QR)", 459, 192, 164, 44, blue);
            AddPill("partial", "PARTIALLY RECEIVED", 461, 256, 160, 24, orange, false);
            AddPill("received", "RECEIVED / ĐÃ NHẬN ĐỦ", 461, 298, 160, 24, green, false);
            AddPill("closed", "CLOSED / ĐÃ ĐÓNG PO", 461, 340, 160, 24, slate, false);
            AddNote(461, 376, "Quét tem QR → ghi nhận\nSL đã nhận → tồn PART", false, note);
            AddConnector(true, 418, 214, 459, 214);
            AddConnector(false, 541, 236, 541, 256);
            AddConnector(true, 541, 280, 541, 298);
            AddConnector(true, 541, 322, 541, 340);

            // Cột 3 - Tồn kho & điều chuyển
            AddGroup(665, 56, 374, 278, "KHO LINH KIỆN PART", "(Component Stock Runtime)");
            AddNode("stock", "TỒN KHO PART", 683, 192, 126, 40, green);
            AddNode("transfer", "ĐIỀU CHUYỂN LOT", 894, 192, 126, 40, purple);
            AddNote(688, 240, "Tra cứu theo\nPartNo / LOT / Site / Slot", false, note);
            AddNote(899, 240, "Slot PART → Slot PART", false, note);
            AddNote(690, 296, "Chỉ LOT AVAILABLE, AvailableQty > 0\nmới được điều chuyển / xuất.", false, note);
            AddConnector(true, 623, 212, 683, 212);
            AddConnector(true, 809, 212, 894, 212);
            AddConnector(true, 1020, 212, 1080, 212);

            // Cột 4 - Cấp liệu & truy xuất
            AddNode("issue", "XUẤT PART → WIP", 1080, 192, 175, 40, teal);
            AddPill("wip", "THEO NHU CẦU BOM", 1085, 253, 165, 24, teal, true);
            AddNode("trace", "TRUY XUẤT LOT", 1080, 337, 175, 40, slate);
            AddNote(1085, 384, "LOT linh kiện → thành phẩm", false, note);
            AddConnector(false, 1167, 232, 1167, 253);
            AddConnector(true, 1167, 277, 1167, 337);

            SetBanner("PO → LOT/QR → nhận hàng → tồn PART → điều chuyển → xuất PART → WIP đều ghi nhật ký workflow/ledger của MES.");
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
            int lane = status == 0 || status == 10 ? 0 : status == 20 ? 1 : status == 30 || status == 40 ? 2 : status == 50 ? 3 : -1;
            SetActiveLane(lane);
        }
    }
}
