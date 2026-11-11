using PCTP.Domain.Entities;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using System;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Phase 7: business orchestration cho scenario GiaoDB.
    /// Repository chịu trách nhiệm persistence/data access; service điều phối nghiệp vụ.
    /// </summary>
    public class PhieuGiaoDbService: IPhieuGiaoDbService
    {
        private readonly IPhieuRepository _phieuRepo;
        private readonly IPhieuGiaoDBRepository _giaoDbRepo;
        private readonly IOrderSourceFactory _orderSourceFactory;
        private readonly CustomerConfig _cfg;
        private readonly Func<bool> _isLoaiSP;

        public PhieuGiaoDbService(
            IPhieuRepository phieuRepo,
            IPhieuGiaoDBRepository giaoDbRepo,
            IOrderSourceFactory orderSourceFactory,
            CustomerConfig cfg,
            Func<bool> isLoaiSP)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _giaoDbRepo = giaoDbRepo ?? throw new ArgumentNullException(nameof(giaoDbRepo));
            _orderSourceFactory = orderSourceFactory ?? throw new ArgumentNullException(nameof(orderSourceFactory));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _isLoaiSP = isLoaiSP ?? throw new ArgumentNullException(nameof(isLoaiSP));
        }

        // Bộ bảng Giao DB lấy từ config khách (HVN: TMPPHIEUGIAOHANGDB/_IFS/DOCQRCODE_DB).
        private string TmpTableGiaoDB => _cfg.Delivery.CoConfigGiaoDB ? _cfg.Delivery.TmpTableGiaoDB : "TMPPHIEUGIAOHANGDB";
        private string IfsTableGiaoDB => !string.IsNullOrWhiteSpace(_cfg.Delivery.IfsTableGiaoDB) ? _cfg.Delivery.IfsTableGiaoDB : "TMPPHIEUGIAOHANGDB_IFS";
        private string DocQrTableGiaoDB => _cfg.Delivery.CoConfigGiaoDB ? _cfg.Delivery.DocQRTableGiaoDB : "DOCQRCODE_DB";

        public DataTable GetDanhSachMaHang() => _phieuRepo.GetDanhSachMaHang();

        public int TaoPhieuVaChiTiet(
            string ten,
            DateTime ngayLap,
            int nhaMay,
            string nhaMayName,
            string note,
            DataTable chiTiet)
        {
            return _phieuRepo.TaoPhieuVaChiTietGiaoDB(
                ten, ngayLap, nhaMay, nhaMayName, note, chiTiet);
        }

        public void LuuGiaoDB(DataTable donHang, GioXuat gioXuat, int addNm)
        {
            _giaoDbRepo.LuuGiaoDB(
                donHang,
                gioXuat.MoTa,
                addNm,
                TmpTableGiaoDB,
                IfsTableGiaoDB,
                docQrTable: DocQrTableGiaoDB);
        }

        public DataTable LoadTmpPhieuGiaoDB(DateTime ngayGiao, int addNm)
        {
            var ctx = new OrderLoadContext
            {
                Cfg = _cfg,
                NgayGiao = ngayGiao,
                AddNm = addNm,
                Source = OrderSourceKind.GiaoDB,
                Category = _isLoaiSP() ? OrderCategory.SP : OrderCategory.MP
            };

            var source = _orderSourceFactory.GetSource(ctx);
            return source.Load(ctx).Orders;
        }

        public void XuLySauUpload()
        {
            // Đơn chờ = mọi dòng STATUS='NG' của bảng nghiệp vụ FVN_Purchase_Order_DB_CT.
            DataTable donHang = _phieuRepo.BuildDonHangTuUpload();
            if (donHang == null || donHang.Rows.Count == 0)
                return;

            // NẠP MỘT LẦN cho TẤT CẢ nhà máy. Trước đây lặp theo từng nhà máy, nhưng nhánh
            // (GIAO DB) của Usp_Qrcode_LOAD_PHIEU_DOCQR2405 DELETE toàn bộ TMPPHIEUGIAOHANGDB
            // rồi chép nguyên _IFS => nhóm xử lý sau xóa mất nhóm trước, lưới của nhà máy kia trống.
            // Mỗi dòng đã mang ADDNM riêng nên SP chép đúng về từng nhà máy; tham số addNm chỉ
            // phục vụ bước chuẩn hóa đầu SP và không ảnh hưởng nhánh (GIAO DB).
            int addNmThamChieu = DbValueHelper.SafeInt(donHang.Rows[0]["ADDNM"]);

            _phieuRepo.LuuGiaoDB(
                donHang,
                "(GIAO DB)",
                addNm: addNmThamChieu,
                tmpTable: TmpTableGiaoDB,
                ifsTable: IfsTableGiaoDB,
                docQrTable: DocQrTableGiaoDB);
        }

        public string CanhBaoLuoiRong(DateTime ngayGiao, int addNm)
        {
            DataTable thongKe = _giaoDbRepo.ThongKeDonChoXuLy();
            if (thongKe == null || thongKe.Rows.Count == 0)
                return null;

            string TenNm(int nm) => nm == 2 ? "Hà Nam" : "Vĩnh Phúc";
            string FmtNgay(DateTime d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

            var dong = thongKe.AsEnumerable()
                .Select(r => new
                {
                    Ngay = Convert.ToDateTime(r["NGAYGIAO"]).Date,
                    Nm = DbValueHelper.SafeInt(r["ADDNM"]),
                    So = DbValueHelper.SafeInt(r["SODONG"])
                })
                .ToList();

            var khop = dong.FirstOrDefault(x => x.Ngay == ngayGiao.Date && x.Nm == addNm);
            if (khop != null)
                return $"Có {khop.So} dòng Giao DB đang chờ ở ngày {FmtNgay(ngayGiao)} - {TenNm(addNm)} " +
                       "nhưng chưa nạp lên lưới. Hãy thử Upload lại hoặc báo bộ phận IT kiểm tra.";

            string ds = string.Join("\n", dong.Select(x => $"  • {FmtNgay(x.Ngay)} - {TenNm(x.Nm)}: {x.So} dòng"));
            return $"Lưới trống vì không có đơn Giao DB nào ở ngày {FmtNgay(ngayGiao)} - {TenNm(addNm)}.\n" +
                   "Đơn Giao DB đang chờ ở:\n" + ds + "\nHãy đổi Ngày xuất hàng / Nhà máy cho khớp.";
        }
    }
}
