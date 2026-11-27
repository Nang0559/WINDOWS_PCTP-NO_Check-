using System.Drawing;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.Utils;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraReports.UI;

namespace PCTP.Modules.MesPlanning.Reports
{
    /// <summary>
    /// Dựng bố cục hai tem thành phẩm bằng code (A4 ngang, 1 tem / trang) theo hai mẫu tem thực tế của nhà máy.
    /// Bố cục vẫn chỉnh được trong Report Designer (menu Quản trị → Thiết kế layout tem); layout đã chỉnh lưu ở LabelLayoutStore
    /// và thay cho bố cục gốc này. Mỗi điều khiển gắn cột bằng ExpressionBinding [Cột] nên chỉ cần giữ nguyên tên cột.
    /// </summary>
    internal static class ProductionLabelLayout
    {
        public const string Arial = "Arial";
        public const string Times = "Times New Roman";

        public static void SetupPage(XtraReport report, out DetailBand detail)
        {
            var top = new TopMarginBand { Name = "TopMargin", HeightF = 19F, Dpi = 96F };
            var bottom = new BottomMarginBand { Name = "BottomMargin", HeightF = 19F, Dpi = 96F };
            detail = new DetailBand { Name = "Detail", HeightF = 750F, Dpi = 96F, KeepTogether = true };
            report.Bands.AddRange(new Band[] { top, detail, bottom });
            report.DefaultPrinterSettingsUsing.UsePaperKind = true;
            report.Font = new DXFont(Arial, 9.75F);
            report.Landscape = true;
            report.Margins = new DXMargins(19F, 19F, 19F, 19F);
            report.PageHeight = 794;
            report.PageWidth = 1123;
            report.PaperKind = DXPaperKind.A4;
            report.ReportUnit = ReportUnit.Pixels;
        }

        /// <summary>Một ô có khung. text = chữ cố định; column = cột dữ liệu (nếu có thì thay chữ cố định). Cả hai null = ô trống.</summary>
        public static XRLabel Cell(DetailBand band, string name, float x, float y, float w, float h, string text, string column,
            string font, float size, bool bold, bool italic, TextAlignment align, bool border = true, string prefix = null)
        {
            var label = new XRLabel
            {
                Name = name,
                Dpi = 96F,
                LocationFloat = new PointFloat(x, y),
                SizeF = new SizeF(w, h),
                Multiline = true,
                WordWrap = true,
                Borders = border ? BorderSide.All : BorderSide.None,
                BorderColor = Color.Black,
                BorderWidth = 1F,
                ForeColor = Color.Black,
                BackColor = Color.Transparent,
                Padding = new PaddingInfo(4, 4, 0, 0, 96F),
                TextAlignment = align,
                Font = new DXFont(font, size, Style(bold, italic))
            };
            if (column != null)
                label.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
                    prefix == null ? "[" + column + "]" : "'" + prefix + "' + [" + column + "]"));
            else if (text != null) label.Text = text;
            band.Controls.Add(label);
            return label;
        }

        public static XRBarCode Qr(DetailBand band, string name, float x, float y, float w, float h, string column)
        {
            var qr = new XRBarCode
            {
                Name = name,
                Dpi = 96F,
                AutoModule = true,
                ShowText = false,
                Alignment = TextAlignment.MiddleCenter,
                Borders = BorderSide.None,
                LocationFloat = new PointFloat(x, y),
                SizeF = new SizeF(w, h),
                Padding = new PaddingInfo(6, 6, 6, 6, 96F),
                Symbology = new QRCodeGenerator
                {
                    CompactionMode = QRCodeCompactionMode.Byte,
                    ErrorCorrectionLevel = QRCodeErrorCorrectionLevel.M
                }
            };
            qr.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[" + column + "]"));
            band.Controls.Add(qr);
            return qr;
        }

        private static DXFontStyle Style(bool bold, bool italic)
        {
            DXFontStyle s = DXFontStyle.Regular;
            if (bold) s |= DXFontStyle.Bold;
            if (italic) s |= DXFontStyle.Italic;
            return s;
        }
    }

    /// <summary>
    /// Tem thùng thành phẩm (mẫu 1): Ca SX | Ngày SX, Tên hàng, Mã hàng, QR, Nơi SX (nhà máy / bộ phận / địa chỉ), Xuất xứ, SL, số tem 28 ký tự.
    /// DataSource: bảng từ <see cref="ProductionLabelData.PrepareCarton"/>.
    /// </summary>
    public sealed class ProductionCartonLabelReport : XtraReport
    {
        public ProductionCartonLabelReport()
        {
            DetailBand d;
            ProductionLabelLayout.SetupPage(this, out d);
            const float ox = 20F, oy = 10F;   // lề trong của tem trên trang A4
            var L = ProductionLabelLayout.Arial; var T = ProductionLabelLayout.Times;
            var c = TextAlignment.MiddleCenter; var l = TextAlignment.MiddleLeft;

            ProductionLabelLayout.Cell(d, "lblShiftCaption", ox, oy, 368, 70, "Ca SX", null, L, 30, true, false, c);
            ProductionLabelLayout.Cell(d, "lblDateCaption", ox + 368, oy, 615, 70, "Ngày SX", null, L, 30, true, false, c);
            ProductionLabelLayout.Cell(d, "lblShift", ox, oy + 70, 368, 75, null, "ShiftText", T, 28, true, false, c);
            ProductionLabelLayout.Cell(d, "lblDate", ox + 368, oy + 70, 615, 75, null, "DateText", L, 28, true, false, c);
            ProductionLabelLayout.Cell(d, "lblNameCaption", ox, oy + 145, 368, 85, "Tên Hàng:", null, L, 30, true, false, l);
            ProductionLabelLayout.Cell(d, "lblName", ox + 368, oy + 145, 615, 85, null, "ProductDescription", T, 20, true, false, c);
            ProductionLabelLayout.Cell(d, "lblPartCaption", ox, oy + 230, 368, 91, "Mã Hàng:", null, L, 30, true, false, l);
            ProductionLabelLayout.Cell(d, "lblPart", ox + 368, oy + 230, 615, 91, null, "ProductPartNo", T, 28, true, false, c);

            ProductionLabelLayout.Cell(d, "boxQr", ox, oy + 321, 368, 345, null, null, L, 10, false, false, c);
            ProductionLabelLayout.Qr(d, "qrLabel", ox + 4, oy + 325, 360, 337, "QrPayload");

            ProductionLabelLayout.Cell(d, "lblPlaceCaption", ox + 368, oy + 321, 615, 70, "Nơi SX", null, T, 22, true, false, c);
            ProductionLabelLayout.Cell(d, "boxFactory", ox + 368, oy + 391, 615, 156, null, null, T, 10, false, false, c);
            ProductionLabelLayout.Cell(d, "lblFactory", ox + 368, oy + 405, 615, 36, null, "FactoryName", T, 16, true, true, c, false);
            ProductionLabelLayout.Cell(d, "lblDepartment", ox + 368, oy + 445, 615, 40, null, "DepartmentLine", T, 22, true, false, c, false);
            ProductionLabelLayout.Cell(d, "lblAddress", ox + 368, oy + 495, 615, 40, null, "FactoryAddress", T, 11, true, true, c, false);

            ProductionLabelLayout.Cell(d, "lblOriginCaption", ox + 368, oy + 547, 240, 70, "Xuất Xứ", null, T, 22, true, false, c);
            ProductionLabelLayout.Cell(d, "lblOrigin", ox + 368, oy + 617, 240, 49, null, "OriginText", L, 16, false, true, c);
            ProductionLabelLayout.Cell(d, "lblQty", ox + 608, oy + 547, 375, 119, null, "QtyText", T, 36, true, false, c);

            ProductionLabelLayout.Cell(d, "lblLabelNo", ox, oy + 666, 983, 44, null, "LabelNo", T, 24, true, false, c);
        }
    }

    /// <summary>
    /// Phiếu quản lý nhập hàng thành phẩm ca (mẫu 2, palet): STT xe, Tên / Mã sản phẩm, Lot No, Số lượng, QR, khung xác nhận giao nhận
    /// (Assy → PC), ghi chú, khung Quản lý sản xuất. DataSource: bảng từ <see cref="ProductionLabelData.PreparePallet"/>.
    /// </summary>
    public sealed class ProductionPalletLabelReport : XtraReport
    {
        public ProductionPalletLabelReport()
        {
            DetailBand d;
            ProductionLabelLayout.SetupPage(this, out d);
            const float ox = 10F, oy = 10F;
            var L = ProductionLabelLayout.Arial; var T = ProductionLabelLayout.Times;
            var c = TextAlignment.MiddleCenter; var l = TextAlignment.MiddleLeft;
            const string box = "Segoe UI Symbol";

            // Tiêu đề: PHIẾU QUẢN LÝ NHẬP HÀNG THÀNH PHẨM CA : <ca>
            ProductionLabelLayout.Cell(d, "lblTitle", ox, oy, 1070, 43, null, "ShiftText", T, 26, true, false, c, true,
                "PHIẾU QUẢN LÝ NHẬP HÀNG THÀNH PHẨM CA :   ");

            // Hàng 2: nơi phát hành + khung xác nhận giao hàng
            ProductionLabelLayout.Cell(d, "lblIssuer", ox, oy + 43, 602, 49, "Nơi phát hành phiếu( Công đoạn đóng hàng)", null, L, 20, false, false, l);
            ProductionLabelLayout.Cell(d, "lblConfirm", ox + 602, oy + 43, 468, 31, "Xác Nhận Giao Hàng", null, L, 16, true, false, c);
            ProductionLabelLayout.Cell(d, "lblAssy", ox + 602, oy + 74, 126, 18, "Giao Hàng (Assy)", null, L, 8, true, false, c);
            ProductionLabelLayout.Cell(d, "lblPc", ox + 728, oy + 74, 135, 18, "Nhận Hàng (PC)", null, L, 8, true, false, c);
            ProductionLabelLayout.Cell(d, "lblHandDate", ox + 863, oy + 74, 207, 18, "Ngày Giao Nhận", null, L, 8, true, false, c);

            // Các hàng thông tin (bên trái) + ô tick / ngày (bên phải) cùng chiều cao.
            string[] caption = { "Số Thứ Tự Xe(Pallet)", "Tên Sản Phẩm", "Mã Sản Phẩm", "Lot No", "Số Lượng", "Check Tem", "Người Thực Hiện" };
            string[] column = { "SeqText", "ProductDescription", "ProductPartNo", "LabelNo", "QtyText", "", "" };
            float[] height = { 38, 38, 44, 44, 38, 40, 50 };
            float[] size = { 20, 20, 22, 20, 20, 12, 12 };
            float y = oy + 92;
            for (int i = 0; i < caption.Length; i++)
            {
                ProductionLabelLayout.Cell(d, "lblCap" + i, ox, y, 210, height[i], caption[i], null, L, 13, true, false, l);
                ProductionLabelLayout.Cell(d, "lblVal" + i, ox + 210, y, 392, height[i], null, column[i].Length == 0 ? null : column[i], T, size[i], true, false, c);
                if (i < 6)
                {
                    ProductionLabelLayout.Cell(d, "chkAssy" + i, ox + 602, y, 126, height[i], "☐", null, box, 12, false, false, c);
                    ProductionLabelLayout.Cell(d, "chkPc" + i, ox + 728, y, 135, height[i], "☐", null, box, 12, false, false, c);
                }
                else
                {
                    ProductionLabelLayout.Cell(d, "chkAssy" + i, ox + 602, y, 126, height[i], null, null, box, 12, false, false, c);
                    ProductionLabelLayout.Cell(d, "chkPc" + i, ox + 728, y, 135, height[i], null, null, box, 12, false, false, c);
                }
                ProductionLabelLayout.Cell(d, "cellDate" + i, ox + 863, y, 207, height[i], null, null, L, 10, false, false, c);
                y += height[i];
            }

            // Khối dưới: QR + ghi chú | Quản lý sản xuất
            float by = y;   // = oy + 92 + 292
            ProductionLabelLayout.Cell(d, "boxQr", ox, by, 250, 174, null, null, L, 10, false, false, c);
            ProductionLabelLayout.Qr(d, "qrLabel", ox + 2, by + 2, 246, 170, "QrPayload");
            ProductionLabelLayout.Cell(d, "lblNoteHead", ox + 250, by, 352, 54,
                "* Chi Chú:\nGhi lại nội dung của vật phẩm đầu hoặc xử lý bất thường khi phát sinh", null, T, 8.5F, false, true, c);
            ProductionLabelLayout.Cell(d, "lblHowTo", ox + 250, by + 54, 352, 120,
                "* Cách xác nhận hàng khi giao nhận :\n- Kiểm tra hàng thực tế .\n-So sánh với hạng mục ghi tại vị trí\nphát hành phiếu và tích vào ô trống", null, T, 9, true, false, c);
            ProductionLabelLayout.Cell(d, "lblMgmt", ox + 602, by, 468, 54, "QUẢN LÝ SẢN XUẤT", null, T, 16, true, false, c);
            float[] mw = { 126, 60, 70, 100, 112 };
            string[] mh = { "NGÀY", "GIỜ", "SỐ LƯỢNG", "NGƯỜI XUẤT", "SỐ LƯỢNG TỒN" };
            float mx = ox + 602;
            for (int i = 0; i < mw.Length; i++)
            {
                ProductionLabelLayout.Cell(d, "mgmtHead" + i, mx, by + 54, mw[i], 42, mh[i], null, L, 8.5F, false, false, c);
                for (int r = 0; r < 4; r++)
                    ProductionLabelLayout.Cell(d, "mgmtCell" + i + "_" + r, mx, by + 96 + r * 39, mw[i], 39, null, null, L, 10, false, false, c);
                mx += mw[i];
            }

            // Hướng dẫn tích V / X
            float gy = by + 174;
            ProductionLabelLayout.Cell(d, "lblRule", ox, gy, 602, 82,
                "- Nếu thông tin và số liệu trùng khớp \"V\".\n-Nếu thông tin và số liệu có sự sai lệch \"X\".\n(Báo cáo ngay với trưởng ca khi có bất thường)",
                null, T, 8.5F, false, false, c);
            // Số tem (= Lot No) ở dòng cuối
            ProductionLabelLayout.Cell(d, "lblLabelNo", ox, gy + 82 + 4, 1070, 36, null, "LabelNo", T, 14, true, false, c);
        }
    }
}
