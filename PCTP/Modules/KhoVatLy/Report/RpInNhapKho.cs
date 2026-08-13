using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraReports.UI;
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;

namespace PCTP.VIEWSTOCK.RpIn
{
    public partial class RpInNhapKho : DevExpress.XtraReports.UI.XtraReport
    {
        public RpInNhapKho()
        {
            // 1. CẤU HÌNH KHỔ GIẤY A5 NẰM NGANG
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.A5;
            this.Landscape = true;
            this.Margins = new System.Drawing.Printing.Margins(30, 30, 30, 30);

            DetailBand detail = new DetailBand() { HeightF = 460 };
            this.Bands.Add(detail);

            Font fontTitle = new Font("Times New Roman", 19, FontStyle.Bold);
            Font fontHeader = new Font("Times New Roman", 11, FontStyle.Bold);
            Font fontSubHeader = new Font("Times New Roman", 9, FontStyle.Bold);
            Font fontBody = new Font("Times New Roman", 10.5f, FontStyle.Bold);
            Font fontDetailVal = new Font("Times New Roman", 11, FontStyle.Regular);
            Font fontNote = new Font("Times New Roman", 8.5f, FontStyle.Italic);

            // =========================================================================
            // BƯỚC 1: TIÊU ĐỀ CHÍNH
            // =========================================================================
            XRLabel lblTitle = new XRLabel()
            {
                Text = "PHIẾU QUẢN LÝ NHẬP HÀNG THÀNH PHẨM CA :",
                Font = fontTitle,
                LocationF = new PointF(0, 0),
                SizeF = new SizeF(630, 35),
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight
            };
            XRLabel lblCaValue = new XRLabel()
            {
                Text = "[Ca]",
                Font = new Font("Times New Roman", 20, FontStyle.Bold),
                LocationF = new PointF(635, 0),
                SizeF = new SizeF(80, 35),
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft
            };
            detail.Controls.AddRange(new XRControl[] { lblTitle, lblCaValue });

            // =========================================================================
            // BƯỚC 2: BẢNG THÔNG TIN CHÍNH (ĐÃ SỬA GỘP Ô BẰNG WIDTH)
            // =========================================================================
            XRTable tableMain = new XRTable()
            {
                LocationF = new PointF(0, 45),
                SizeF = new SizeF(725, 240),
                Borders = DevExpress.XtraPrinting.BorderSide.All
            };
            tableMain.BeginInit();

            // --- DÒNG HEADER 1: Gộp cột bằng cách chỉ tạo 2 ô với tổng WidthF = 725 ---
            XRTableRow rowH1 = new XRTableRow() { HeightF = 25F };
            rowH1.Cells.Add(new XRTableCell() { Text = " Nơi phát hành phiếu( Công đoạn đóng hàng)", Font = fontHeader, WidthF = 405F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft });
            // Ô này chiếm toàn bộ phần còn lại (95 + 95 + 130 = 320) thay thế cho ColumnSpan
            rowH1.Cells.Add(new XRTableCell() { Text = "Xác nhận Giao Hàng", Font = fontHeader, WidthF = 320F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            tableMain.Rows.Add(rowH1);

            // --- DÒNG HEADER 2: Chia nhỏ cột chức vụ ---
            XRTableRow rowH2 = new XRTableRow() { HeightF = 20F };
            // Thay vì dùng RowSpan cho ô bên trái, ta tạo một ô trống có WidthF = 405F khớp với dòng trên và dưới
            rowH2.Cells.Add(new XRTableCell() { Text = " Tên hạng mục thông tin / Kết quả kiểm tra", Font = fontSubHeader, WidthF = 405F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft });
            rowH2.Cells.Add(new XRTableCell() { Text = "Giao Hàng (Assy)", Font = fontSubHeader, WidthF = 95F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            rowH2.Cells.Add(new XRTableCell() { Text = "Nhận Hàng (PC)", Font = fontSubHeader, WidthF = 95F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            rowH2.Cells.Add(new XRTableCell() { Text = "Ngày Giao Nhận", Font = fontSubHeader, WidthF = 130F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            tableMain.Rows.Add(rowH2);

            // --- DANH SÁCH 7 DÒNG THÔNG TIN SẢN PHẨM ---
            string[][] items = new string[][] {
                new string[] { " Số Thứ Tự Xe(Pallet)", "[SoThuTuXe]", "False" },
                new string[] { " Tên Sản Phẩm", "[TenSanPham]", "False" },
                new string[] { " Mã Sản Phẩm", "[MaSanPham]", "True" },
                new string[] { " Lot No", "[LotNo]", "False" },
                new string[] { " Số Lượng", "[SoLuong]", "False" },
                new string[] { " Check Tem", "[CheckTem]", "False" },
                new string[] { " Người Thực Hiện", "[NguoiThucHien]", "False" }
            };

            foreach (var item in items)
            {
                XRTableRow row = new XRTableRow() { HeightF = 28F };

                row.Cells.Add(new XRTableCell() { Text = item[0], Font = fontBody, WidthF = 175F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft });
                row.Cells.Add(new XRTableCell() { Text = "  " + item[1], Font = fontDetailVal, WidthF = 230F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft });
                row.Cells.Add(new XRTableCell() { Text = "☐", Font = new Font("MS Outlook", 12), WidthF = 95F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });

                string checkIcon = (item[2] == "True") ? "☑" : "☐";
                row.Cells.Add(new XRTableCell() { Text = checkIcon, Font = new Font("MS Outlook", 12), WidthF = 95F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
                row.Cells.Add(new XRTableCell() { Text = "", WidthF = 130F });

                tableMain.Rows.Add(row);
            }

            tableMain.EndInit();
            detail.Controls.Add(tableMain);

            // =========================================================================
            // BƯỚC 3: PHÂN KHU ĐÁY PHIẾU
            // =========================================================================
            int footerTop = 295;

            XRBarCode barCode = new XRBarCode()
            {
                LocationF = new PointF(0, footerTop),
                SizeF = new SizeF(120, 120),
                ShowText = false,
                AutoModule = true
            };
            QRCodeGenerator qrGen = new QRCodeGenerator() { Version = QRCodeVersion.AutoVersion, CompactionMode = QRCodeCompactionMode.Byte };
            barCode.Symbology = qrGen;
            barCode.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[QrData]"));
            detail.Controls.Add(barCode);

            XRLabel lblNoteHeader = new XRLabel()
            {
                Text = "* Chú Thích:\nGhi lại nội dung của vật phẩm đầu hoặc xử lý bất thường khi phát sinh",
                Font = fontNote,
                LocationF = new PointF(130, footerTop),
                SizeF = new SizeF(275, 30),
                Multiline = true,
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.TopLeft
            };
            XRLabel lblNoteBody = new XRLabel()
            {
                Text = "* Cách xác nhận hàng khi giao nhận :\n  - Kiểm tra hàng thực tế .\n  -So sánh với hạng mục ghi tại vị trí\n  phát hành phiếu và tích vào ô trống\n\n- Nếu thông tin và số liệu trùng khớp \"V\".\n-Nếu thông tin và số liệu có sự sai lệch \"X\".\n(Báo cáo ngay với trưởng ca khi có bất thường)",
                Font = fontNote,
                LocationF = new PointF(130, footerTop + 35),
                SizeF = new SizeF(275, 85),
                Multiline = true,
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.TopLeft
            };
            detail.Controls.AddRange(new XRControl[] { lblNoteHeader, lblNoteBody });

            XRLabel lblQLSX = new XRLabel()
            {
                Text = "QUẢN LÝ SẢN XUẤT",
                Font = fontHeader,
                LocationF = new PointF(415, footerTop - 3),
                SizeF = new SizeF(310, 20),
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter
            };
            detail.Controls.Add(lblQLSX);

            XRTable tableQLSX = new XRTable()
            {
                LocationF = new PointF(415, footerTop + 18),
                SizeF = new SizeF(310, 102),
                Borders = DevExpress.XtraPrinting.BorderSide.All
            };
            tableQLSX.BeginInit();

            XRTableRow rowQlHeader = new XRTableRow() { HeightF = 22F, Font = new Font("Times New Roman", 7.5f, FontStyle.Bold) };
            rowQlHeader.Cells.Add(new XRTableCell() { Text = "NGÀY", WidthF = 55F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            rowQlHeader.Cells.Add(new XRTableCell() { Text = "GIỜ", WidthF = 45F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            rowQlHeader.Cells.Add(new XRTableCell() { Text = "SỐ LƯỢNG", WidthF = 65F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            rowQlHeader.Cells.Add(new XRTableCell() { Text = "NGƯỜI XUẤT", WidthF = 75F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            rowQlHeader.Cells.Add(new XRTableCell() { Text = "SỐ LƯỢNG TỒN", WidthF = 70F, TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter });
            tableQLSX.Rows.Add(rowQlHeader);

            for (int k = 0; k < 4; k++)
            {
                XRTableRow rowEmpty = new XRTableRow() { HeightF = 20F };
                rowEmpty.Cells.Add(new XRTableCell() { Text = "", WidthF = 55F });
                rowEmpty.Cells.Add(new XRTableCell() { Text = "", WidthF = 45F });
                rowEmpty.Cells.Add(new XRTableCell() { Text = "", WidthF = 65F });
                rowEmpty.Cells.Add(new XRTableCell() { Text = "", WidthF = 75F });
                rowEmpty.Cells.Add(new XRTableCell() { Text = "", WidthF = 70F });
                tableQLSX.Rows.Add(rowEmpty);
            }
            tableQLSX.EndInit();
            detail.Controls.Add(tableQLSX);

            // =========================================================================
            // BƯỚC 4: THANH MÃ HÀNG NGANG CHẠY DÀI DƯỚI ĐÁY CÙNG
            // =========================================================================
            XRTable tableBottomBar = new XRTable()
            {
                LocationF = new PointF(0, footerTop + 125),
                SizeF = new SizeF(725, 25),
                Borders = DevExpress.XtraPrinting.BorderSide.All
            };
            tableBottomBar.BeginInit();
            XRTableRow rowBottom = new XRTableRow();
            rowBottom.Cells.Add(new XRTableCell()
            {
                Text = "  [LotNo]",
                Font = fontDetailVal,
                TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter
            });
            tableBottomBar.Rows.Add(rowBottom);
            tableBottomBar.EndInit();
            detail.Controls.Add(tableBottomBar);
        }
    }
}
