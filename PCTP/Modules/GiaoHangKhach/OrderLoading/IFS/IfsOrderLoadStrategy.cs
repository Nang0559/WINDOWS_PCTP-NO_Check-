using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.IFSORDER
{
    /// <summary>
    /// Customer dùng IFS Oracle (HVN 100001, và mọi customer không có OrderTable riêng).
    /// Đơn hàng gốc load trực tiếp từ IFS qua linked server/OleDb — không có bảng
    /// trung gian nào lưu đơn hàng, nên "load lại" luôn nghĩa là query lại IFS.
    /// </summary>
    public class IfsOrderLoadStrategy : IOrderLoadStrategy
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
            return _ifsRepo.GetCustomerOrderJoin(
                ctx.NgayGiao.ToString("ddMMyyyy"),
                ctx.GioFcc, ctx.GioFccMoTa,
                ctx.NhaMay, ctx.AddNm, ctx.Cfg);
        }

        public void MergeLotDaLuu(DataTable donHang, OrderLoadContext ctx)
        {
            // PhieuRepository đã có sẵn LoadLuuPhieu — dùng lại, không viết trùng.
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
            // Đây chính là LuuVaLoad hiện có — Drop/Create IFS table + BulkInsert + CallSP.
            _tmpRepo.LuuVaLoad(ctx.Cfg.IfsTable, "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                donHang, ctx.NgayGiao.ToString("yyyy-MM-dd"), ctx.NhaMay,
                ctx.GioFccMoTa, ctx.AddNm, ctx.Cfg.TmpTable, ctx.Cfg.DocQRTable);
        }

        public DataTable SoSanhVoiIFS(DataTable donHang, OrderLoadContext ctx)
        {
            // Nguồn ĐÃ LÀ IFS — không có gì để so sánh với chính nó.
            return new DataTable();
        }
    }
}
