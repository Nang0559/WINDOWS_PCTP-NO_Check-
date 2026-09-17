using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using System;
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
            // không phải bằng giờ giao. Vì vậy SP phải lấy toàn bộ đơn trong ngày
            // theo nhà máy rồi mới lọc category ở tầng IRowCategoryFilter.
            // MP vẫn giữ nguyên luồng cũ: nhà máy + giờ giao.
            int hinhThucIn = ctx.Category == OrderCategory.SP ? 2 : 1;

            return _ifsRepo.GetCustomerOrderJoin(
                ctx.NgayGiao.ToString("ddMMyyyy"),
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
                ctx.NgayGiao.ToString("yyyy-MM-dd"), ctx.GioFccMoTa);

            foreach (DataRow rDaLuu in daLuu.Rows)
            {
                var match = donHang.AsEnumerable().FirstOrDefault(r =>
                    string.Equals(r["MAHANG"]?.ToString(), rDaLuu["MAHANG"]?.ToString(),
                        StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match["LOT"] = rDaLuu["LOT"];
                    if (donHang.Columns.Contains("STATUS"))
                        match["STATUS"] = rDaLuu["STATUS"];
                }
            }
        }

        public void SyncChoDocQR(DataTable donHang, OrderLoadContext ctx)
        {
            bool isSP = ctx.Category == OrderCategory.SP;
            var d = ctx.Cfg.Delivery;

            _tmpRepo.LuuVaLoad(d.GetIfsTable(isSP), "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                donHang, ctx.NgayGiao.ToString("yyyy-MM-dd"), ctx.NhaMay,
                ctx.GioFccMoTa, ctx.AddNm, d.GetTmpTable(isSP), d.GetDocQRTable(isSP));
        }

        public DataTable SoSanhVoiIFS(DataTable donHang, OrderLoadContext ctx)
        {
            return new DataTable();
        }
    }
}