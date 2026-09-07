using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
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
    using System;
    using System.Drawing;
    using DevExpress.XtraReports.UI;
    using DevExpress.XtraPrinting;
    using PCTP.Modules.XuLyHangLoi.Models;
    using PCTP.Shared.Enums;

    /// <summary>
    /// Mẫu in "Phiếu xử lý bất thường" (BM-04/QĐ-QC-02).
    ///
    /// ⚠️ ĐÃ SỬA theo WORKFLOW_HANGLOI.md: bản trước bind vào một loạt field
    /// không còn tồn tại trên <see cref="PhieuXuLyBatThuong"/> (LoaiSanPham,
    /// CapDoQuanTrong, CapDoPhienBan, NguoiThucHien, PhuongPhapKiemTra,
    /// SoLuongKiemTra, PhuongPhapSua, SoLuongSua, XacNhanCuoiKetQua,
    /// NguoiDanhGia, NguoiThucHienQC, GhiChuQC, BoPhanChiuTrachNhiem,
    /// Ngay/HoTen* của 4 bên ký) — hậu quả của việc audit từng bước đã được
    /// tách sang các bảng con riêng (TraHangQTChungXuat/Giao/QC/NhapNG,
    /// xem mục 1.2 tài liệu) thay vì dồn hết vào 1 bảng phẳng như thiết kế
    /// cũ. File không hề có nơi nào gọi tới (0 call site trong toàn bộ
    /// solution) nên lỗi build của nó không bị phát hiện cho tới giờ.
    ///
    /// Fix: report giờ nhận thêm <see cref="TraHangQTChungQC"/> (kết quả QC
    /// xác nhận cuối — bảng audit duy nhất chứa "kết luận") làm tham số tuỳ
    /// chọn, và MỌI ô dữ liệu đều trỏ về field có thật. Những mục vốn dành
    /// cho chữ ký tay (không có nguồn dữ liệu số trong toàn bộ schema) được
    /// để trống thay vì tham chiếu field ảo — đúng bản chất phiếu giấy cần
    /// ký tay sau khi in, không phải lỗi thiếu dữ liệu.
    /// </summary>
    public partial class RpPhieuXuLyBatThuong : XtraReport
    {
        // Font dùng chung
        private static readonly Font FBold = new Font("Times New Roman", 7.5F, FontStyle.Bold);
        private static readonly Font FReg = new Font("Times New Roman", 7.5F, FontStyle.Regular);
        private static readonly Font FSmall = new Font("Times New Roman", 6.5F, FontStyle.Regular);
        private static readonly Font FSmallBold = new Font("Times New Roman", 6.5F, FontStyle.Bold);

        private TopMarginBand topMarginBand1;
        private DetailBand detailBand1;
        private BottomMarginBand bottomMarginBand1;

        /// <param name="data">Bảng chính FVN_PhieuXuLyBatThuong — bắt buộc.</param>
        /// <param name="qc">
        /// Kết quả QC xác nhận cuối (FVN_TraHangQTChung_QC) — tuỳ chọn, chỉ có
        /// khi phiếu đã qua bước IQTChungService.QCXacNhanCuoi (Status ≥
        /// DaQCXacNhanCuoi). Truyền null nếu in phiếu ở giai đoạn sớm hơn —
        /// các ô liên quan sẽ để trống cho điền tay.
        /// </param>
        public RpPhieuXuLyBatThuong(PhieuXuLyBatThuong data, TraHangQTChungQC qc = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            PaperKind = DXPaperKind.A5;
            Landscape = false;
            // Lề siêu nhỏ giúp tối ưu tối đa không gian in trong khổ A5
            Margins = new Margins(10, 10, 8, 8);

            var detail = new DetailBand { HeightF = 735 };
            Bands.Add(detail);

            // ── 1. TIÊU ĐỀ & HỘP A ────────────────────────────────────────
            detail.Controls.Add(new XRLabel
            {
                Text = "Phiếu xử lý bất thường" + (string.IsNullOrEmpty(data.SoPhieu) ? "" : $"  —  {data.SoPhieu}"),
                Font = new Font("Times New Roman", 15, FontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter,
                LocationF = new PointF(0, 2),
                SizeF = new SizeF(475, 26)
            });

            var boxA = CreateTable(new PointF(485, 2), new SizeF(75, 26), BorderSide.All);
            var rA1 = new XRTableRow { HeightF = 13F };
            rA1.Cells.Add(CreateCell("— A", 75, FontStyle.Bold, TextAlignment.MiddleCenter));
            var rA2 = new XRTableRow { HeightF = 13F };
            rA2.Cells.Add(CreateCell("Lưu tại QC", 75, FontStyle.Italic, TextAlignment.MiddleCenter));
            rA2.Cells[0].Font = new Font("Times New Roman", 6.5F, FontStyle.Italic);
            boxA.Rows.Add(rA1);
            boxA.Rows.Add(rA2);
            boxA.EndInit();
            detail.Controls.Add(boxA);

            // ── 2. HEADER: Model / Tên SP-Mã SP / Số lô / Số lượng lô lỗi ─
            var tblHeader = CreateTable(new PointF(0, 32), new SizeF(560, 105), BorderSide.All);

            var row1 = new XRTableRow { HeightF = 20F };
            row1.Cells.Add(CreateCell("Model:", 60, FontStyle.Bold, TextAlignment.MiddleLeft));
            row1.Cells.Add(CreateCell(data.Model, 160, FontStyle.Regular, TextAlignment.MiddleLeft));
            row1.Cells.Add(CreateCell("Tên sản phẩm / Mã sản phẩm", 180, FontStyle.Bold, TextAlignment.MiddleCenter));
            row1.Cells.Add(CreateCell("Số lô", 75, FontStyle.Bold, TextAlignment.MiddleCenter));
            row1.Cells.Add(CreateCell("Số lượng lô lỗi", 85, FontStyle.Bold, TextAlignment.MiddleCenter));
            tblHeader.Rows.Add(row1);

            var row2 = new XRTableRow { HeightF = 22F };
            row2.Cells.Add(CreateCell("Nơi phát sinh", 60, FontStyle.Bold, TextAlignment.MiddleLeft));
            row2.Cells.Add(CreateCell(data.BoPhanPhatHanh, 160, FontStyle.Regular, TextAlignment.MiddleLeft));
            row2.Cells.Add(CreateCell(data.MaSanPham, 180, FontStyle.Regular, TextAlignment.MiddleLeft));
            row2.Cells.Add(CreateCell(data.SoLo, 75, FontStyle.Regular, TextAlignment.MiddleCenter));
            row2.Cells.Add(CreateCell(data.SoLuongLoi.ToString(), 85, FontStyle.Bold, TextAlignment.MiddleCenter));
            tblHeader.Rows.Add(row2);

            // ── Hàng 3: Nguồn/Lô lỗi | Nội dung bất thường | P/P xử lý ────
            var row3 = new XRTableRow { HeightF = 63F };

            // ✅ Trước đây bind vào data.LoaiSanPham (không tồn tại). Thay bằng
            // các field có thật, vốn chưa hề được in ra trước đó: Nguồn phát
            // sinh, Số lô lỗi (SoLoLoi), và LOT nguồn khi Nguon = TraNoiBo.
            string nguonText = data.Nguon == NguonXuLyBatThuong.KhachTra ? "Khách trả" : "Trả nội bộ";
            var noiDungLoai = new StringBuilder();
            noiDungLoai.AppendLine($"Nguồn: {nguonText}");
            noiDungLoai.AppendLine($"Số lô lỗi: {data.SoLoLoi}");
            if (data.Nguon == NguonXuLyBatThuong.TraNoiBo)
                noiDungLoai.Append($"LOT nguồn: {data.LotNguon}");
            var cellLoai = CreateCell(noiDungLoai.ToString().TrimEnd(),
                120, FontStyle.Regular, TextAlignment.TopLeft);
            cellLoai.Font = FSmall;
            cellLoai.Multiline = true;

            var cellND = CreateCell($"Nội dung bất thường:\n{data.NoiDungBatThuong}",
                255, FontStyle.Regular, TextAlignment.TopLeft);
            cellND.Font = FReg;
            cellND.Multiline = true;

            string ppStr = data.PhanLoaiXuLy ?? "";
            var cellPP = CreateCell(
                "P/P xử lý\n" +
                $"{(ppStr.Contains("Phân loại") ? "( X ) Phân loại xử lý" : "[   ] Phân loại xử lý")}\n" +
                $"{(ppStr.Contains("Gia công") ? "( X ) Gia công riêng" : "[   ] Gia công riêng")}\n" +
                $"{(ppStr.Contains("Gửi trả") ? "( X ) Gửi trả hàng" : "[   ] Gửi trả hàng")}\n" +
                $"{(ppStr.Contains("Hủy") ? "( X ) Hủy hàng" : "[   ] Hủy hàng")}\n" +
                $"{(ppStr.Contains("Tách") ? "( X ) Kiểm tra tách hàng" : "[   ] Kiểm tra tách hàng")}",
                185, FontStyle.Regular, TextAlignment.TopLeft);
            cellPP.Font = FSmall;
            cellPP.Multiline = true;

            row3.Cells.Add(cellLoai);
            row3.Cells.Add(cellND);
            row3.Cells.Add(cellPP);
            tblHeader.Rows.Add(row3);
            tblHeader.EndInit();
            detail.Controls.Add(tblHeader);

            // ── 3. Trạng thái quy trình & hướng xử lý ─────────────────────
            // ✅ Trước đây bind CapDoQuanTrong/CapDoPhienBan (không tồn tại).
            // Thay bằng Status/HuongXuLy — 2 field state machine có thật và
            // hữu ích hơn nhiều để tra cứu nhanh trên bản in (xem mục 0 và 4
            // của WORKFLOW_HANGLOI.md để tra nghĩa từng giá trị).
            var tblMeta = CreateTable(new PointF(0, 139), new SizeF(560, 20), BorderSide.All);
            var rowMeta = new XRTableRow { HeightF = 20F };
            rowMeta.Cells.Add(CreateCell("Quy trình xử lý và kết quả xử lý:", 230, FontStyle.Bold, TextAlignment.MiddleLeft));
            rowMeta.Cells.Add(CreateCell($"Trạng thái: {data.Status}", 165, FontStyle.Regular, TextAlignment.MiddleLeft));
            rowMeta.Cells.Add(CreateCell($"Hướng xử lý: {data.HuongXuLy}", 165, FontStyle.Regular, TextAlignment.MiddleLeft));
            tblMeta.Rows.Add(rowMeta);
            tblMeta.EndInit();
            detail.Controls.Add(tblMeta);

            // ── 4. Người thực hiện | Xác nhận lần cuối ───────────────────
            // ✅ data.NguoiThucHien không tồn tại — dùng CreatedBy (người lập
            // phiếu, field audit có thật).
            var tblUser = CreateTable(new PointF(0, 160), new SizeF(560, 20), BorderSide.All);
            var rowUser = new XRTableRow { HeightF = 20F };
            rowUser.Cells.Add(CreateCell($"Người lập phiếu: {data.CreatedBy}", 260, FontStyle.Regular, TextAlignment.MiddleLeft));
            rowUser.Cells.Add(CreateCell("Xác nhận lần cuối (phòng chất lượng)", 300, FontStyle.Bold, TextAlignment.MiddleCenter));
            tblUser.Rows.Add(rowUser);
            tblUser.EndInit();
            detail.Controls.Add(tblUser);

            // ── 5. KHUNG SƠ ĐỒ: Kiểm tra ➔ Sửa ➔ Kết luận ─────────────────
            float diagTop = 181;
            float diagHeight = 108;

            var boxKT = CreateTable(new PointF(0, diagTop), new SizeF(180, diagHeight), BorderSide.All);
            var rKT1 = new XRTableRow { HeightF = 14F };
            rKT1.Cells.Add(CreateCell("Phương pháp kiểm tra", 180, FontStyle.Bold, TextAlignment.MiddleCenter));
            boxKT.Rows.Add(rKT1);
            var rKT2 = new XRTableRow { HeightF = 40F };
            // ✅ PhuongPhapKiemTra không được lưu số hoá ở đâu trong hệ thống
            // — để trống dòng "Nội dung:" cho ghi tay thay vì bind field ảo.
            var cellKTNoiDung = CreateCell("Nội dung:", 110, FontStyle.Regular, TextAlignment.TopLeft);
            cellKTNoiDung.Font = FSmall; cellKTNoiDung.Multiline = true;
            var cellKTNoiPhatSinh = CreateCell($"Nơi phát sinh\n{data.BoPhanPhatHanh}", 70, FontStyle.Regular, TextAlignment.TopCenter);
            cellKTNoiPhatSinh.Font = FSmall; cellKTNoiPhatSinh.Multiline = true;
            rKT2.Cells.Add(cellKTNoiDung);
            rKT2.Cells.Add(cellKTNoiPhatSinh);
            boxKT.Rows.Add(rKT2);
            var rKT3 = new XRTableRow { HeightF = 14F };
            // ✅ SoLuongKiemTra không tồn tại — SL OK/NG thật sự chỉ có sau
            // bước QCXacNhanCuoi, lấy từ TraHangQTChungQC (tham số qc); nếu
            // phiếu chưa tới bước đó (qc == null) thì để trống cho điền tay.
            rKT3.Cells.Add(CreateCell($"OK  SL: {(qc != null ? qc.SoLuongOK.ToString() : "")}", 90, FontStyle.Regular, TextAlignment.MiddleLeft));
            rKT3.Cells.Add(CreateCell($"NG  SL: {(qc != null ? qc.SoLuongNG.ToString() : "")}", 90, FontStyle.Regular, TextAlignment.MiddleLeft));
            boxKT.Rows.Add(rKT3);
            boxKT.EndInit();
            detail.Controls.Add(boxKT);

            // Mũi tên 1
            detail.Controls.Add(new XRLabel
            {
                Text = "➔",
                Font = new Font("Times New Roman", 12F, FontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter,
                LocationF = new PointF(180, diagTop + diagHeight / 2 - 8),
                SizeF = new SizeF(14, 16)
            });

            var boxSua = CreateTable(new PointF(194, diagTop), new SizeF(160, diagHeight), BorderSide.All);
            var rSua1 = new XRTableRow { HeightF = 14F };
            rSua1.Cells.Add(CreateCell("Phương pháp sửa", 160, FontStyle.Bold, TextAlignment.MiddleCenter));
            boxSua.Rows.Add(rSua1);
            var rSua2 = new XRTableRow { HeightF = 40F };
            // ✅ PhuongPhapSua không được lưu số hoá — để trống cho ghi tay.
            var cellSuaND = CreateCell("Nội dung:", 160, FontStyle.Regular, TextAlignment.TopLeft);
            cellSuaND.Font = FSmall; cellSuaND.Multiline = true;
            rSua2.Cells.Add(cellSuaND);
            boxSua.Rows.Add(rSua2);
            var rSua3 = new XRTableRow { HeightF = 14F };
            // ✅ SoLuongSua không tồn tại — dùng SoLuongDaRework (field thật,
            // ghi nhận số lượng đã đưa đi rework) làm số liệu gần đúng nhất.
            rSua3.Cells.Add(CreateCell($"SL đã rework: {(qc != null ? qc.SoLuongDaRework.ToString() : "")}", 160, FontStyle.Regular, TextAlignment.MiddleLeft));
            boxSua.Rows.Add(rSua3);
            boxSua.EndInit();
            detail.Controls.Add(boxSua);

            // Mũi tên 2
            detail.Controls.Add(new XRLabel
            {
                Text = "➔",
                Font = new Font("Times New Roman", 12F, FontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter,
                LocationF = new PointF(354, diagTop + diagHeight / 2 - 8),
                SizeF = new SizeF(14, 16)
            });

            var boxKL = CreateTable(new PointF(368, diagTop), new SizeF(192, diagHeight), BorderSide.All);
            // ✅ data.XacNhanCuoiKetQua không tồn tại — suy ra OK/NG từ
            // SoLuongNG của TraHangQTChungQC (0 => OK). Nếu chưa có qc, để
            // cả 2 ô trống (chưa có kết luận) thay vì mặc định sai lệch.
            string ketLuanText = qc == null
                ? "[   ] OK   [   ] NG"
                : (qc.SoLuongNG == 0 ? "( X ) OK   [   ] NG" : "[   ] OK   ( X ) NG");
            var rKL1 = new XRTableRow { HeightF = 14F };
            rKL1.Cells.Add(CreateCell($"Kết luận: {ketLuanText}", 192, FontStyle.Bold, TextAlignment.MiddleLeft));
            boxKL.Rows.Add(rKL1);
            var rKL2 = new XRTableRow { HeightF = 14F };
            rKL2.Cells.Add(CreateCell("OK", 30, FontStyle.Regular, TextAlignment.MiddleCenter));
            // ✅ data.NguoiDanhGia không tồn tại — dùng qc.NguoiQC (người QC
            // xác nhận cuối, field thật duy nhất tương ứng vai trò này).
            rKL2.Cells.Add(CreateCell($"Người đánh giá: {qc?.NguoiQC}", 162, FontStyle.Regular, TextAlignment.MiddleLeft));
            boxKL.Rows.Add(rKL2);
            var rKL3 = new XRTableRow { HeightF = 14F };
            rKL3.Cells.Add(CreateCell("NG", 30, FontStyle.Regular, TextAlignment.MiddleCenter));
            // ✅ data.NguoiThucHienQC không tồn tại — dùng NguoiDinhHuong
            // (người QC định hướng ban đầu, field thật, tránh trùng lặp giá
            // trị với "Người đánh giá" ở trên).
            rKL3.Cells.Add(CreateCell($"Người định hướng: {data.NguoiDinhHuong}", 162, FontStyle.Regular, TextAlignment.MiddleLeft));
            boxKL.Rows.Add(rKL3);
            var rKL4 = new XRTableRow { HeightF = 14F };
            // ✅ data.GhiChuQC không tồn tại — dùng qc.Note (field thật).
            var cellGhiChu = CreateCell($"Ghi chú: {qc?.Note}", 192, FontStyle.Regular, TextAlignment.TopLeft);
            cellGhiChu.Font = FSmall; cellGhiChu.Multiline = true;
            rKL4.Cells.Add(cellGhiChu);
            boxKL.Rows.Add(rKL4);
            var rKL5 = new XRTableRow { HeightF = 14F };
            // ✅ data.BoPhanChiuTrachNhiem không tồn tại — dùng lại
            // BoPhanPhatHanh (bộ phận nơi phát sinh = bộ phận chịu trách
            // nhiệm xử lý, hợp lý về nghiệp vụ và là field thật).
            var cellBoPhanTN = CreateCell($"Bộ phận chịu trách nhiệm: {data.BoPhanPhatHanh}", 192, FontStyle.Bold, TextAlignment.MiddleLeft);
            cellBoPhanTN.Font = FSmall;
            rKL5.Cells.Add(cellBoPhanTN);
            boxKL.Rows.Add(rKL5);
            boxKL.EndInit();
            detail.Controls.Add(boxKL);

            // ── 6. BẢNG CHỮ KÝ 4 BÊN ──────────────────────────────────────
            // ✅ Trước đây bind vào 8 field (Ngay*/HoTen*) không tồn tại ở
            // BẤT KỲ đâu trong schema — đây là các cột ký tay trên phiếu
            // giấy, không có nguồn dữ liệu số. 3/4 cột được ánh xạ sang field
            // thật có ý nghĩa nghiệp vụ tương đương (phát sinh=CreatedAt/By,
            // QC tiếp nhận=NgayDinhHuong/NguoiDinhHuong, QC duyệt=qc.ThoiGian/
            // NguoiQC); cột "Bộ phận phát hành xác nhận" không có nguồn số
            // hoá tương ứng nên để trống cho ký tay — đúng bản chất, không
            // phải thiếu sót.
            float signTop = diagTop + diagHeight + 5;
            var tblSign = CreateTable(new PointF(0, signTop), new SizeF(560, 70), BorderSide.All);

            var rowSignH = new XRTableRow { HeightF = 14F };
            rowSignH.Cells.Add(CreateCell("Chữ ký", 45, FontStyle.Bold, TextAlignment.MiddleCenter));
            rowSignH.Cells.Add(CreateCell("Bộ phận phát hành", 110, FontStyle.Bold, TextAlignment.MiddleCenter));
            rowSignH.Cells.Add(CreateCell("QC tiếp nhận", 90, FontStyle.Bold, TextAlignment.MiddleCenter));
            rowSignH.Cells.Add(CreateCell("Bộ phận phát hành xác nhận", 165, FontStyle.Bold, TextAlignment.MiddleCenter));
            rowSignH.Cells.Add(CreateCell("QC duyệt", 150, FontStyle.Bold, TextAlignment.MiddleCenter));
            tblSign.Rows.Add(rowSignH);

            var rowSignSub = new XRTableRow { HeightF = 14F };
            rowSignSub.Cells.Add(CreateCell("", 45, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignSub.Cells.Add(CreateCell("Phát sinh", 55, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignSub.Cells.Add(CreateCell("Sub Leader", 55, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignSub.Cells.Add(CreateCell("Leader/Chief", 90, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignSub.Cells.Add(CreateCell("Leader/Chief", 82, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignSub.Cells.Add(CreateCell("MG", 83, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignSub.Cells.Add(CreateCell("MG", 75, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignSub.Cells.Add(CreateCell("GM", 75, FontStyle.Regular, TextAlignment.MiddleCenter));
            foreach (XRTableCell c in rowSignSub.Cells) c.Font = FSmall;
            tblSign.Rows.Add(rowSignSub);

            var rowSignNgay = new XRTableRow { HeightF = 14F };
            rowSignNgay.Cells.Add(CreateCell("Ngày", 45, FontStyle.Bold, TextAlignment.MiddleCenter));
            rowSignNgay.Cells.Add(CreateCell(data.CreatedAt.ToString("dd/MM/yyyy"), 55, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignNgay.Cells.Add(CreateCell("", 55, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignNgay.Cells.Add(CreateCell(data.NgayDinhHuong?.ToString("dd/MM/yyyy"), 90, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignNgay.Cells.Add(CreateCell("", 82, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignNgay.Cells.Add(CreateCell("", 83, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignNgay.Cells.Add(CreateCell(qc?.ThoiGian.ToString("dd/MM/yyyy"), 75, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignNgay.Cells.Add(CreateCell("", 75, FontStyle.Regular, TextAlignment.MiddleCenter));
            foreach (XRTableCell c in rowSignNgay.Cells) c.Font = FSmall;
            tblSign.Rows.Add(rowSignNgay);

            var rowSignHoTen = new XRTableRow { HeightF = 22F };
            rowSignHoTen.Cells.Add(CreateCell("Họ tên", 45, FontStyle.Bold, TextAlignment.MiddleCenter));
            rowSignHoTen.Cells.Add(CreateCell(data.CreatedBy, 55, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignHoTen.Cells.Add(CreateCell("", 55, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignHoTen.Cells.Add(CreateCell(data.NguoiDinhHuong, 90, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignHoTen.Cells.Add(CreateCell("", 82, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignHoTen.Cells.Add(CreateCell("", 83, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignHoTen.Cells.Add(CreateCell(qc?.NguoiQC, 75, FontStyle.Regular, TextAlignment.MiddleCenter));
            rowSignHoTen.Cells.Add(CreateCell("", 75, FontStyle.Regular, TextAlignment.MiddleCenter));
            foreach (XRTableCell c in rowSignHoTen.Cells) c.Font = FSmall;
            tblSign.Rows.Add(rowSignHoTen);

            tblSign.EndInit();
            detail.Controls.Add(tblSign);

            // ── 7. SƠ ĐỒ LƯU TRÌNH — 2 hàng hộp tối ưu chiều cao (boxH = 32) ─
            // (Thuần mô tả tĩnh quy trình giấy — không cần dữ liệu, giữ nguyên.)
            float flowTop = signTop + 72;

            detail.Controls.Add(new XRLabel
            {
                Text = "• Lưu trình:",
                Font = FBold,
                LocationF = new PointF(0, flowTop),
                SizeF = new SizeF(100, 11)
            });
            flowTop += 12;

            string[] flowRow1 =
            {
            "Phát sinh\nbất thường",
            "Bộ phận tách riêng sản phẩm,\ntreo phiếu vàng hiển thị",
            "Sub Leader phát hành\nphiếu XLBT",
            "Chuyển phiếu sang bộ phận QC\n(Leader/Chief) ký tiếp nhận",
            "QC xác nhận và\nđưa ra phương pháp xử lý"
        };
            string[] flowRow2 =
            {
            "Bộ phận phát hành tiến hành\nxử lý theo kết quả QC",
            "QC: Leader/Chief xác nhận\nMG/GM duyệt và trả kết quả",
            "Nếu có trách nhiệm tách lọc,\ntreo phiếu hiển thị",
            "Trả lại Bộ phận phát hành\n(A.Chief/Chief/MG) ký duyệt",
            ""
        };

            float boxW = 108, boxH = 32, arrowW = 4, x0 = 0;

            // Hàng 1
            for (int i = 0; i < flowRow1.Length; i++)
            {
                var box = CreateTable(new PointF(x0 + i * (boxW + arrowW), flowTop), new SizeF(boxW, boxH), BorderSide.All);
                var r = new XRTableRow { HeightF = boxH };
                var c = CreateCell(flowRow1[i], boxW, FontStyle.Regular, TextAlignment.MiddleCenter);
                c.Font = new Font("Times New Roman", 5.2F, FontStyle.Regular); c.Multiline = true;
                r.Cells.Add(c);
                box.Rows.Add(r);
                box.EndInit();
                detail.Controls.Add(box);

                if (i < flowRow1.Length - 1)
                {
                    detail.Controls.Add(new XRLabel
                    {
                        Text = "➔",
                        Font = new Font("Times New Roman", 7.5F, FontStyle.Bold),
                        TextAlignment = TextAlignment.MiddleCenter,
                        LocationF = new PointF(x0 + i * (boxW + arrowW) + boxW, flowTop + boxH / 2 - 6),
                        SizeF = new SizeF(arrowW, 12)
                    });
                }
            }

            // Mũi tên xuống dưới ở cột cuối
            float lastColX = x0 + (flowRow1.Length - 1) * (boxW + arrowW);
            detail.Controls.Add(new XRLabel
            {
                Text = "↓",
                Font = new Font("Times New Roman", 7.5F, FontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter,
                LocationF = new PointF(lastColX + boxW / 2 - 6, flowTop + boxH),
                SizeF = new SizeF(12, 10)
            });

            float flowTop2 = flowTop + boxH + 9;

            // Hàng 2 (phải qua trái)
            for (int i = 0; i < flowRow2.Length; i++)
            {
                float colX = x0 + (flowRow1.Length - 1 - i) * (boxW + arrowW);

                var box = CreateTable(new PointF(colX, flowTop2), new SizeF(boxW, boxH), BorderSide.All);
                var r = new XRTableRow { HeightF = boxH };
                var c = CreateCell(flowRow2[i], boxW, FontStyle.Regular, TextAlignment.MiddleCenter);
                c.Font = new Font("Times New Roman", 5.2F, FontStyle.Regular); c.Multiline = true;
                r.Cells.Add(c);
                box.Rows.Add(r);
                box.EndInit();
                detail.Controls.Add(box);

                if (i < flowRow2.Length - 1)
                {
                    detail.Controls.Add(new XRLabel
                    {
                        Text = "⬅",
                        Font = new Font("Times New Roman", 7.5F, FontStyle.Bold),
                        TextAlignment = TextAlignment.MiddleCenter,
                        LocationF = new PointF(colX - arrowW, flowTop2 + boxH / 2 - 6),
                        SizeF = new SizeF(arrowW, 12)
                    });
                }
            }

            // Mũi tên lên khép vòng
            detail.Controls.Add(new XRLabel
            {
                Text = "↑",
                Font = new Font("Times New Roman", 7.5F, FontStyle.Bold),
                TextAlignment = TextAlignment.MiddleCenter,
                LocationF = new PointF(x0 + boxW / 2 - 6, flowTop2 - 9),
                SizeF = new SizeF(12, 10)
            });

            // ── 8. CHÚ Ý & MÃ BIỂU MẪU ────────────────────────────────────
            float noteTop = flowTop2 + boxH + 4;
            detail.Controls.Add(new XRLabel
            {
                Text = "• Chú ý: 1. Nhanh chóng liên lạc QC khi lỗi. 2. Phiếu B trả bộ phận phát hành, phiếu A lưu QC. 3. Hàng chỉ luân chuyển khi có xác nhận QC.",
                Font = new Font("Times New Roman", 5.2F, FontStyle.Regular),
                LocationF = new PointF(0, noteTop),
                SizeF = new SizeF(440, 16),
                Multiline = true
            });

            detail.Controls.Add(new XRLabel
            {
                Text = "BM-04/QĐ-QC-02",
                Font = FBold,
                TextAlignment = TextAlignment.MiddleRight,
                LocationF = new PointF(445, noteTop),
                SizeF = new SizeF(115, 16)
            });

            // Cố định cứng chiều cao detail band để hoàn toàn không bị sang trang 2
            detail.HeightF = 730;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private XRTable CreateTable(PointF location, SizeF size, BorderSide borders)
        {
            var table = new XRTable
            {
                LocationF = location,
                SizeF = size,
                Borders = borders
            };
            table.BeginInit();
            return table;
        }

        private XRTableCell CreateCell(string text, float width, FontStyle style, TextAlignment align)
        {
            return new XRTableCell
            {
                Text = text ?? "",
                WidthF = width,
                Font = new Font("Times New Roman", 7.5F, style),
                TextAlignment = align,
                Padding = new PaddingInfo(2, 2, 1, 1),
                Borders = BorderSide.All
            };
        }

        private void InitializeComponent()
        {
            this.topMarginBand1 = new DevExpress.XtraReports.UI.TopMarginBand();
            this.detailBand1 = new DevExpress.XtraReports.UI.DetailBand();
            this.bottomMarginBand1 = new DevExpress.XtraReports.UI.BottomMarginBand();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // topMarginBand1
            // 
            this.topMarginBand1.Name = "topMarginBand1";
            // 
            // detailBand1
            // 
            this.detailBand1.Name = "detailBand1";
            // 
            // bottomMarginBand1
            // 
            this.bottomMarginBand1.Name = "bottomMarginBand1";
            // 
            // RpPhieuXuLyBatThuong
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
        this.topMarginBand1,
        this.detailBand1,
        this.bottomMarginBand1});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();
        }
    }
}
