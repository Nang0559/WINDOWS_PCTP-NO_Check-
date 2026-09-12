using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading
{
    /// <summary>
    /// Customer dùng bảng riêng (YMVN, HTN) — đơn hàng thực tế (MilkRun/TableOrder)
    /// được đối chiếu với IFS baseline (roadmap mục 2.2). KHÔNG xử lý GiaoDB —
    /// đã tách sang <see cref="GiaoDB.GiaoDbOrderLoadStrategy"/> (roadmap mục 2.4:
    /// "GiaoDB không phải MilkRun").
    /// </summary>
    public class OrderTableLoadStrategy : IOrderLoadStrategy
    {
        private readonly ITableOrderRepository _phieuRepo;
        private readonly IPhieuTmpRepository _phieuTmpRepo;

        public OrderTableLoadStrategy(
            ITableOrderRepository phieuRepo,
            IPhieuTmpRepository phieuTmpRepo)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _phieuTmpRepo = phieuTmpRepo ?? throw new ArgumentNullException(nameof(phieuTmpRepo));
        }

        public DataTable LoadDonHangGoc(OrderLoadContext ctx)
        {
            var d = ctx.Cfg.Delivery;
            bool isSP = ctx.Category == OrderCategory.SP;

            // LoadPhieuTuBangRieng tự MergeLotTuBangRieng nội bộ — MergeLotDaLuu bên dưới
            // không cần làm gì thêm. Không truyền tenBangOverride — luôn dùng đúng
            // d.OrderTable của customer, GiaoDacBiet đã tách sang strategy khác.
            return _phieuRepo.LoadPhieuTuBangRieng(
                ctx.NgayGiao.ToString("yyyy-MM-dd"),
                string.Join(",", ctx.CheckedGios ?? new List<string>()),
                isSP, d.DockCodeSP, ctx.Cfg);
        }

        public void MergeLotDaLuu(DataTable donHang, OrderLoadContext ctx)
        {
            // KHÔNG LÀM GÌ — LoadPhieuTuBangRieng đã tự gọi MergeLotTuBangRieng() nội bộ.
        }

        public void SyncChoDocQR(DataTable donHang, OrderLoadContext ctx)
        {
            var d = ctx.Cfg.Delivery;
            var tables = new PhieuTableSet(
                tmpTable: d.TmpTable,
                sourceTable: d.OrderTable,
                docQRTable: d.DocQRTable);

            _phieuTmpRepo.LuuVaLoad(tables, "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                donHang, ctx.NgayGiao.ToString("yyyy-MM-dd"), ctx.NhaMay,
                ctx.GioFccMoTa, ctx.AddNm);
        }

        /// <summary>
        /// BẮT BUỘC theo roadmap mục 2.2: TableOrder luôn phải đối chiếu với IFS baseline
        /// để phát hiện thiếu/thừa/lệch — khác GiaoDB (không cần IFS, mục 2.4).
        /// </summary>
        public DataTable SoSanhVoiIFS(DataTable donHang, OrderLoadContext ctx)
        {
            return _phieuRepo.SoSanhDonHangVoiIFS(
                donHang, ctx.NgayGiao.ToString("yyyy-MM-dd"), ctx.Cfg);
        }
    }
}