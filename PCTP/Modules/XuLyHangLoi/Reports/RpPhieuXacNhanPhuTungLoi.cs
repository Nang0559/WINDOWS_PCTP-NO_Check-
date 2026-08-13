using DevExpress.Drawing.Printing;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraReports.UI;
using PCTP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.VIEWSTOCK.RpIn
{
    // PCTP/VIEWSTOCK/RpIn/RpPhieuXacNhanPhuTungLoi.cs — mẫu ảnh 3, in theo nhóm Model
    public partial class RpPhieuXacNhanPhuTungLoi : XtraReport
    {
        public RpPhieuXacNhanPhuTungLoi(PhieuLoiKhachTra header)
        {
            PaperKind = DXPaperKind.A4;
            Landscape = true;
            var detail = new DetailBand { HeightF = 500 };
            Bands.Add(detail);

            detail.Controls.Add(new XRLabel
            {
                Text = $"PHIẾU XÁC NHẬN PHỤ TÙNG LỖI TRẢ VỀ TỪ {header.Nguon}",
                Font = new Font("Times New Roman", 14, FontStyle.Bold),
                LocationF = new PointF(0, 0),
                SizeF = new SizeF(500, 30)
            });
            detail.Controls.Add(new XRLabel
            {
                Text = $"Ngày {header.NgayPhatHanh:dd} tháng {header.NgayPhatHanh:MM} năm {header.NgayPhatHanh:yyyy}   Ca: {header.Ca}",
                LocationF = new PointF(0, 30),
                SizeF = new SizeF(400, 20)
            });

            var table = new XRTable
            {
                LocationF = new PointF(0, 60),
                SizeF = new SizeF(1000, 300),
                Borders = DevExpress.XtraPrinting.BorderSide.All // 🌟 Sửa ở đây
            };
            table.BeginInit();
            var rowH = new XRTableRow { HeightF = 25F };
            foreach (var col in new[] { "STT", "Model", "Mã hàng", "Tên hàng", "Số lô", "Số lượng", "Nội dung lỗi", "Phiếu lỗi" })
                rowH.Cells.Add(new XRTableCell
                {
                    Text = col,
                    Font = new Font("Times New Roman", 9, FontStyle.Bold),
                    TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter // 🌟 Sửa ở đây
                });
            table.Rows.Add(rowH);

            foreach (var ct in header.ChiTiet)
            {
                var r = new XRTableRow { HeightF = 22F };
                r.Cells.Add(new XRTableCell { Text = ct.Stt.ToString() });
                r.Cells.Add(new XRTableCell { Text = ct.Model });
                r.Cells.Add(new XRTableCell { Text = ct.MaHang });
                r.Cells.Add(new XRTableCell { Text = ct.TenHang });
                r.Cells.Add(new XRTableCell { Text = ct.SoLo });
                r.Cells.Add(new XRTableCell { Text = ct.SoLuong.ToString() });
                r.Cells.Add(new XRTableCell { Text = ct.NoiDungLoi });
                r.Cells.Add(new XRTableCell { Text = ct.CoPhieuLoi ? "Có" : "Không" });
                table.Rows.Add(r);
            }
            table.EndInit();
            detail.Controls.Add(table);
        }
    }
}
