using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Shared.Models;
using System;
using System.Data;
using System.Linq;

namespace PCTP.Applications.Services
{
    /// <summary>
    /// Phase 7: business orchestration cho scenario GiaoDB.
    /// Repository chịu trách nhiệm persistence/data access; service điều phối nghiệp vụ.
    /// </summary>
    public class PhieuGiaoDbService
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
                "TMPPHIEUGIAOHANGDB",
                "TMPPHIEUGIAOHANGDB_IFS");
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
            DataTable donHang = _phieuRepo.BuildDonHangTuUpload();
            if (donHang == null || donHang.Rows.Count == 0)
                return;

            var nhomTheoNhaMay = donHang.AsEnumerable()
                .GroupBy(r => DbValueHelper.SafeInt(r["ADDNM"]));

            foreach (var nhom in nhomTheoNhaMay)
            {
                DataTable phanNhom = donHang.Clone();
                foreach (DataRow r in nhom)
                    phanNhom.ImportRow(r);

                _phieuRepo.LuuGiaoDB(
                    phanNhom,
                    "(GIAO DB)",
                    addNm: nhom.Key,
                    tmpTable: "TMPPHIEUGIAOHANGDB",
                    ifsTable: "TMPPHIEUGIAOHANGDB_IFS");
            }
        }
    }
}
