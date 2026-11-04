using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.IFS
{
    public class IfsOrderLoadStrategy
    {
        private readonly IIFSRepository _ifsRepo;
        private readonly IPhieuLuuTruRepository _luuTruRepo;
        private readonly IPhieuTmpRepository _tmpRepo;

        public IfsOrderLoadStrategy(IIFSRepository ifsRepo, IPhieuLuuTruRepository luuTruRepo,
            IPhieuTmpRepository tmpRepo)
        {
            _ifsRepo = ifsRepo;
            _luuTruRepo = luuTruRepo;
            _tmpRepo = tmpRepo;
        }

        public DataTable LoadDonHangGoc(OrderLoadContext ctx)
        {
            // SP của khách 100001 được xác định bằng DOCK_CODE = HVN,
            // không phải bằng giờ giao. Chuẩn hóa context ngay tại source
            // để toàn bộ pipeline phía sau (SaveFromSource/LuuVaLoad/QR)
            // cũng không còn mang giờ MP cũ vào truy vấn SP.
            if (ctx.Category == OrderCategory.SP)
            {
                ctx.GioFcc = string.Empty;
                ctx.GioFccMoTa = "Tất cả ca";
            }

            // MP vẫn giữ nguyên luồng cũ: nhà máy + giờ giao.
            // SP: nhà máy + toàn bộ giờ trong ngày, sau đó mới lọc DOCK_CODE = HVN.
            int hinhThucIn = ctx.Category == OrderCategory.SP ? 2 : 1;

            return _ifsRepo.GetCustomerOrderJoin(
                ctx.NgayGiao.ToString("ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture),
                ctx.GioFcc,
                ctx.GioFccMoTa,
                ctx.NhaMay,
                ctx.AddNm,
                hinhThucIn,
                ctx.Cfg);
        }

        public void MergeLotDaLuu(DataTable donHang, OrderLoadContext ctx)
        {
            var daLuu = _luuTruRepo.LoadLuuPhieu(ctx.NhaMay,
                ctx.NgayGiao.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ctx.GioFccMoTa);

            // Một dòng đã lưu chỉ thuộc về ĐÚNG MỘT dòng đơn hàng. Khóa khớp giống
            // Usp_Qrcode_Update_Stock2405 (kiểm tra "đã lưu"): MAHANG + CUA + TRUYEN +
            // SOLUONG + GIOGIAO. Trước đây chỉ khớp MAHANG và lấy dòng đầu tiên nên dòng
            // cùng mã nhưng khác số lượng/cửa bị gán nhầm LOT và trạng thái.
            var used = new HashSet<DataRow>();

            foreach (DataRow rDaLuu in daLuu.Rows)
            {
                DataRow match = donHang.AsEnumerable().FirstOrDefault(r =>
                    !used.Contains(r) && SameOrderLine(r, rDaLuu));
                if (match == null)
                    continue;

                used.Add(match);
                match["LOT"] = rDaLuu["LOT"];
                if (donHang.Columns.Contains("STATUS"))
                    match["STATUS"] = rDaLuu["STATUS"];
            }
        }

        private static bool SameOrderLine(DataRow order, DataRow saved)
        {
            return SameText(order, saved, "MAHANG")
                && SameText(order, saved, "CUA")
                && SameText(order, saved, "TRUYEN")
                && SameQuantity(order, saved)
                && SameHour(order, saved);
        }

        private static string Read(DataRow row, string column)
        {
            if (row == null || !row.Table.Columns.Contains(column) || row[column] == DBNull.Value)
                return string.Empty;
            return Convert.ToString(row[column], System.Globalization.CultureInfo.InvariantCulture).Trim();
        }

        private static bool SameText(DataRow a, DataRow b, string column)
        {
            return string.Equals(Read(a, column), Read(b, column), StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameQuantity(DataRow a, DataRow b)
        {
            decimal x, y;
            bool okX = decimal.TryParse(Read(a, "SOLUONG"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out x);
            bool okY = decimal.TryParse(Read(b, "SOLUONG"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out y);
            return okX && okY && x == y;
        }

        private static bool SameHour(DataRow a, DataRow b)
        {
            return NormalizeHour(Read(a, "GIOGIAO")) == NormalizeHour(Read(b, "GIOGIAO"));
        }

        // "7", "07", "07H", "07:00:00" -> "07"
        private static string NormalizeHour(string value)
        {
            string s = (value ?? string.Empty).Replace("H", string.Empty).Replace("h", string.Empty).Trim();
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s.Substring(0, colon);
            int h;
            return int.TryParse(s, out h) ? h.ToString("00") : s;
        }

        public void SyncChoDocQR(DataTable donHang, OrderLoadContext ctx)
        {
            bool isSP = ctx.Category == OrderCategory.SP;
            var d = ctx.Cfg.Delivery;

            _tmpRepo.LuuVaLoad(d.GetIfsTable(isSP), "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                donHang, ctx.NgayGiao.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ctx.NhaMay,
                ctx.GioFccMoTa, ctx.AddNm, d.GetTmpTable(isSP), d.GetDocQRTable(isSP));
        }

        public DataTable SoSanhVoiIFS(DataTable donHang, OrderLoadContext ctx)
        {
            return new DataTable();
        }
    }
}