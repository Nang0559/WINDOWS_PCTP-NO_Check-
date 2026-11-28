using System.Drawing;

namespace PCTP.Modules.MesPlanning.UI
{
    /// <summary>
    /// Sơ đồ "Quy trình &amp; luồng dữ liệu điều hành sản xuất MES" 5 cột:
    /// Khởi tạo &amp; lập kế hoạch → Phát hành lệnh → Tạo LOT sản xuất &amp; BOM → Vận hành sản xuất thực tế → Hoàn thành &amp; đóng lệnh.
    /// Mỗi ô lệnh vừa hiển thị trạng thái (đã xong / đang ở bước này / có thể bấm / chưa thể bấm) vừa là nút lệnh;
    /// các nhãn (DRAFT, RELEASED, LOT CREATED, RUNNING, PAUSED, COMPLETED, CLOSED) sáng lên theo trạng thái lệnh.
    /// Khóa ô lệnh: select, create, release, lot, consume, output, start, pause, resume, complete, close, history.
    /// Khóa nhãn: draft, released, lotcreated, running, paused, completed, closed, ok, ng.
    /// </summary>
    public sealed class MesProductionFlowMapControl : MesFlowCanvasControl
    {
        public MesProductionFlowMapControl()
        {
            var blue = Color.FromArgb(46, 110, 190);
            var green = Color.FromArgb(34, 139, 84);
            var orange = Color.FromArgb(214, 122, 0);
            var purple = Color.FromArgb(96, 84, 170);
            var teal = Color.FromArgb(0, 112, 160);
            var slate = Color.FromArgb(84, 100, 120);
            var gray = Color.FromArgb(110, 120, 135);
            var note = Color.FromArgb(70, 80, 95);

            AddLane("KHỞI TẠO & LẬP KẾ HOẠCH", 0, 233);
            AddLane("PHÁT HÀNH LỆNH", 233, 438);
            AddLane("TẠO LOT SẢN XUẤT & BOM", 438, 644);
            AddLane("VẬN HÀNH SẢN XUẤT THỰC TẾ", 644, 1059);
            AddLane("HOÀN THÀNH & ĐÓNG LỆNH", 1059, MesFlowCanvasControl.CanvasWidth);

            // Cột 0
            AddBox(45, 56, 157, 48, "Kế hoạch sản xuất FIRM", "(ProductionPlanLineId)");
            AddBox(91, 128, 64, 25, "OrderNo", null);
            AddNode("select", "CHỌN KẾ HOẠCH FIRM", 34, 177, 179, 38, blue);
            AddNode("create", "TẠO LỆNH TỪ KẾ HOẠCH", 34, 226, 179, 38, blue);
            AddNote(116, 216, "&", true, note);
            AddPill("draft", "DRAFT / KHỞI TẠO", 66, 281, 115, 24, gray, false);
            AddNote(187, 140, "ProductionPlanLineId\nOrderNo", false, note);
            AddConnector(false, 123, 104, 123, 128);
            AddConnector(true, 123, 153, 123, 177);
            AddConnector(false, 123, 264, 123, 281);

            // Cột 1
            AddNode("release", "RELEASE LỆNH", 253, 192, 165, 40, blue);
            AddPill("released", "RELEASED / ĐÃ PHÁT HÀNH", 256, 253, 160, 24, blue, false);
            AddBox(265, 303, 142, 25, "Tạo ProductionOrderId", null);
            AddNote(392, 150, "ProductionOrderId", false, note);
            AddConnector(true, 213, 215, 233, 215, 233, 212, 253, 212);
            AddConnector(false, 335, 232, 335, 253);
            AddConnector(true, 335, 277, 335, 303);

            // Cột 2
            AddNode("lot", "TẠO LOT SẢN XUẤT", 459, 192, 164, 40, blue);
            AddPill("lotcreated", "LOT CREATED / ĐÃ TẠO LOT", 461, 253, 160, 24, gray, false);
            AddBox(477, 303, 128, 25, "Tạo ProductionLotId", null);
            AddNote(547, 342, "Component LOT", false, note);
            AddNode("consume", "TIÊU HAO LOT\nLINH KIỆN", 459, 374, 164, 51, purple);
            AddNote(459, 432, "Truy vấn Cấu trúc Sản phẩm BOM\n(Bill of Materials)\n•  Quét/Trừ tồn LOT linh kiện\n•  Khớp danh mục BOM vật tư", false, note);
            AddConnector(true, 418, 212, 459, 212);
            AddConnector(false, 541, 232, 541, 253);
            AddConnector(true, 541, 277, 541, 303);
            AddConnector(true, 541, 328, 541, 374);

            // Cột 3 - vận hành
            AddGroup(665, 56, 374, 278, "VẬN HÀNH & THEO DÕI SẢN XUẤT", "(Production Runtime)");
            AddPill("running", "RUNNING\nĐANG SẢN XUẤT", 789, 120, 126, 40, green, false);
            AddNode("start", "START LỆNH", 683, 192, 126, 40, green);
            AddNode("pause", "PAUSE LỆNH", 894, 192, 126, 40, orange);
            AddPill("paused", "PAUSED / TẠM DỪNG", 894, 238, 126, 22, orange, false);
            AddNode("resume", "RESUME LỆNH", 789, 271, 126, 40, blue);
            AddConnector(true, 746, 192, 746, 140, 789, 140);
            AddConnector(true, 915, 140, 957, 140, 957, 192);
            AddConnector(true, 957, 260, 957, 291, 915, 291);
            AddConnector(true, 789, 291, 746, 291, 746, 232);
            AddConnector(true, 623, 212, 683, 212);
            // nhánh từ LOT / tiêu hao sang ghi nhận sản lượng
            AddConnector(false, 623, 399, 644, 399, 644, 212);
            AddConnector(true, 644, 399, 644, 420, 681, 420);

            // Ghi nhận sản lượng
            AddLabeledConnector("OK/NG Qty", 809, 352, 851, 334, 851, 352, 767, 352, 767, 394);
            AddNode("output", "GHI NHẬN SẢN LƯỢNG\n(Output Recording)", 681, 395, 171, 51, gray);
            AddConnector(false, 852, 420, 881, 420);
            AddConnector(true, 881, 420, 881, 382, 913, 382);
            AddConnector(true, 881, 420, 881, 454, 913, 454);
            AddPill("ok", "Số lượng OK", 913, 369, 91, 26, green, true);
            AddNote(913, 399, "LOT thành phẩm", true, Color.FromArgb(30, 36, 46));
            AddNote(913, 414, "(Finished Goods LOT)", false, note);
            AddPill("ng", "Số lượng NG", 913, 441, 91, 26, Color.FromArgb(200, 55, 45), true);
            AddNote(913, 471, "Defect/Scrap", true, Color.FromArgb(30, 36, 46));
            AddNote(913, 486, "Phế phẩm & Lỗi", false, note);

            // Cột 4
            AddNode("complete", "COMPLETE LỆNH", 1080, 192, 175, 40, teal);
            AddPill("completed", "COMPLETED / HOÀN THÀNH", 1085, 253, 165, 24, teal, false);
            AddNode("close", "CLOSE LỆNH", 1080, 314, 175, 40, slate);
            AddPill("closed", "CLOSED / ĐÃ ĐÓNG", 1107, 376, 123, 24, slate, false);
            AddNode("history", "LỊCH SỬ LỆNH", 1080, 437, 175, 40, gray);
            AddConnector(true, 1020, 212, 1080, 212);
            AddConnector(false, 1167, 232, 1167, 253);
            AddConnector(true, 1167, 277, 1167, 314);
            AddConnector(false, 1167, 354, 1167, 376);
            AddConnector(true, 1167, 400, 1167, 437);

            SetBanner("Lệnh/LOT/công đoạn/tiêu hao đều ghi vào MES; DIRECT không tạo tồn WIP, VIA_WIP dùng ledger chung.");
        }

        /// <summary>Sáng các nhãn trạng thái theo mã trạng thái lệnh (0 mới tạo, 10 phát hành, 20 chạy, 30 tạm dừng, 40 hoàn thành, 50 đóng); -1 = chưa có lệnh.</summary>
        public void ApplyOrderStatus(int status, bool hasLot)
        {
            SetPill("draft", status == 0);
            SetPill("released", status == 10);
            SetPill("lotcreated", hasLot && status >= 10 && status < 900);
            SetPill("running", status == 20);
            SetPill("paused", status == 30);
            SetPill("completed", status == 40);
            SetPill("closed", status == 50);
            int lane = status == 0 ? 0 : status == 10 ? 1 : status == 20 || status == 30 ? 3 : status == 40 || status == 50 ? 4 : -1;
            if (status == 10 && hasLot) lane = 2;
            SetActiveLane(lane);
        }
    }
}
