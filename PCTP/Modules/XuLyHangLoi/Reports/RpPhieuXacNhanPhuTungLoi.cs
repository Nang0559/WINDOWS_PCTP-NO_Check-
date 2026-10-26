using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using PCTP.Modules.XuLyHangLoi.Models;
using System;
using System.Drawing;

namespace PCTP.VIEWSTOCK.RpIn
{
    // Mẫu "PHIẾU ĐỔI PHỤ TÙNG LỖI" — bám theo layout phiếu giấy gốc.
    public partial class RpPhieuXacNhanPhuTungLoi : XtraReport
    {
        public RpPhieuXacNhanPhuTungLoi(PhieuTraHang header)
        {
            PaperKind = DXPaperKind.A5;   // khổ nhỏ, giống phiếu giấy gốc
            Landscape = false;

            var detail = new DetailBand { HeightF = 700 };
            Bands.Add(detail);

            var titleFont = new Font("Times New Roman", 13, FontStyle.Bold);
            var labelFont = new Font("Times New Roman", 9, FontStyle.Regular);
            var boldFont = new Font("Times New Roman", 9, FontStyle.Bold);

            // ── Số phiếu (góc phải, in đậm màu đỏ) ──────────────────────
            detail.Controls.Add(new XRLabel
            {
                Text = header.SoPhieu,
                Font = new Font("Times New Roman", 14, FontStyle.Bold),
                ForeColor = Color.Red,
                LocationF = new PointF(350, 0),
                SizeF = new SizeF(150, 30),
                TextAlignment = TextAlignment.MiddleRight
            });

            // ── Tiêu đề ──────────────────────────────────────────────
            detail.Controls.Add(new XRLabel
            {
                Text = "PHIẾU ĐỔI PHỤ TÙNG LỖI",
                Font = titleFont,
                LocationF = new PointF(0, 0),
                SizeF = new SizeF(350, 30),
                TextAlignment = TextAlignment.MiddleCenter
            });

            int y = 40;

            AddFieldRow(detail, "Ngày phát hành", header.NgayPhatHanh?.ToString("dd/MM/yy") ?? "", y, labelFont, boldFont);
            AddFieldRow(detail, "Slip no", header.SlipNo, y += 25, labelFont, boldFont);

            // ── Bảng xác nhận (BP phát hiện lỗi / QC HVN) ───────────────
            var confirmTable = new XRTable
            {
                LocationF = new PointF(0, y += 30),
                SizeF = new SizeF(500, 45),
                Borders = BorderSide.All
            };
            confirmTable.BeginInit();
            var rHeader = new XRTableRow { HeightF = 20F };
            foreach (var col in new[] { "BP phát hiện lỗi", "Xác nhận của BPPHL", "Xác nhận của QC(HVN)" })
                rHeader.Cells.Add(new XRTableCell { Text = col, Font = boldFont, TextAlignment = TextAlignment.MiddleCenter });
            confirmTable.Rows.Add(rHeader);

            var rValue = new XRTableRow { HeightF = 25F };
            rValue.Cells.Add(new XRTableCell { Text = header.BoPhanPhatHienLoi, Font = labelFont, TextAlignment = TextAlignment.MiddleCenter });
            rValue.Cells.Add(new XRTableCell { Text = header.XacNhanBPPhatHienLoi, Font = labelFont, TextAlignment = TextAlignment.MiddleCenter });
            rValue.Cells.Add(new XRTableCell { Text = header.XacNhanQCKhach, Font = labelFont, TextAlignment = TextAlignment.MiddleCenter });
            confirmTable.Rows.Add(rValue);
            confirmTable.EndInit();
            detail.Controls.Add(confirmTable);

            y += 55;
            AddFieldRow(detail, "Tên nhà cung cấp", header.TenKhachHang, y, labelFont, boldFont);

            // ── Bảng chi tiết phụ tùng lỗi ───────────────────────────
            y += 30;
            var table = new XRTable
            {
                LocationF = new PointF(0, y),
                SizeF = new SizeF(500, 200),
                Borders = BorderSide.All
            };
            table.BeginInit();

            var rowH = new XRTableRow { HeightF = 22F };
            foreach (var col in new[] { "Mã số phụ tùng", "Tên phụ tùng", "Nội dung hỏng", "Số lượng" })
                rowH.Cells.Add(new XRTableCell
                {
                    Text = col,
                    Font = boldFont,
                    TextAlignment = TextAlignment.MiddleCenter
                });
            table.Rows.Add(rowH);

            foreach (var ct in header.ChiTiet)
            {
                var r = new XRTableRow { HeightF = 22F };
                r.Cells.Add(new XRTableCell { Text = ct.MaHang, Font = labelFont });
                r.Cells.Add(new XRTableCell { Text = ct.TenHang, Font = labelFont });
                r.Cells.Add(new XRTableCell { Text = ct.LyDoNg, Font = labelFont });
                r.Cells.Add(new XRTableCell { Text = ct.SoLuong.ToString(), Font = labelFont, TextAlignment = TextAlignment.MiddleCenter });
                table.Rows.Add(r);
            }

            // Dòng "Tổng số"
            var rTotal = new XRTableRow { HeightF = 22F };
            rTotal.Cells.Add(new XRTableCell { Text = "Tổng số", Font = boldFont });
            rTotal.Cells.Add(new XRTableCell { Text = "" });
            rTotal.Cells.Add(new XRTableCell { Text = "" });
            rTotal.Cells.Add(new XRTableCell { Text = header.TongSoLuongNhan.ToString(), Font = boldFont, TextAlignment = TextAlignment.MiddleCenter });
            table.Rows.Add(rTotal);

            table.EndInit();
            detail.Controls.Add(table);

            y += 210;
            AddFieldRow(detail, "Ngày nhập PT đổi", header.NgayNhanKho?.ToString("dd/MM/yy") ?? "", y, labelFont, boldFont);

            // ── Chữ ký (để trống, ký tay khi in) ─────────────────────
            y += 40;
            var signTable = new XRTable
            {
                LocationF = new PointF(0, y),
                SizeF = new SizeF(500, 60),
                Borders = BorderSide.All
            };
            signTable.BeginInit();
            var rSignHeader = new XRTableRow { HeightF = 20F };
            foreach (var col in new[] { "Chữ ký nhà cung cấp", "Marker ký", "MS (HVN) ký" })
                rSignHeader.Cells.Add(new XRTableCell { Text = col, Font = boldFont, TextAlignment = TextAlignment.MiddleCenter });
            signTable.Rows.Add(rSignHeader);
            signTable.Rows.Add(new XRTableRow { HeightF = 40F }); // ô trống để ký tay
            signTable.EndInit();
            detail.Controls.Add(signTable);

            // ── Ghi chú quy trình + số tờ (cố định, không lấy từ model) ──
            y += 65;
            detail.Controls.Add(new XRLabel
            {
                Text = "Trình tự xử lý phiếu: QC(HVN) → MS(HVN) lưu → Marker → MS(HVN) → Marker",
                Font = new Font("Times New Roman", 8, FontStyle.Italic),
                LocationF = new PointF(0, y),
                SizeF = new SizeF(500, 20)
            });
        }

        private void AddFieldRow(DetailBand detail, string label, string value, float y, Font labelFont, Font valueFont)
        {
            detail.Controls.Add(new XRLabel
            {
                Text = label + ":",
                Font = labelFont,
                LocationF = new PointF(0, y),
                SizeF = new SizeF(150, 20)
            });
            detail.Controls.Add(new XRLabel
            {
                Text = value ?? "",
                Font = valueFont,
                LocationF = new PointF(150, y),
                SizeF = new SizeF(350, 20)
            });
        }
    }
}