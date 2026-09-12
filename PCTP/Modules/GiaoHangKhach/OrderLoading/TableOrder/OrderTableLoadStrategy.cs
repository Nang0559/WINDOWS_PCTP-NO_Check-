using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
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
    public class OrderTableLoadStrategy 
    {
        private readonly ITableOrderRepository _phieuRepo;
        private readonly IPhieuTmpRepository _phieuTmpRepo;
        private readonly IIFSRepository _ifsRepo;                 // ✅ mới
        private readonly IRowCategoryFilter _rowCategoryFilter;
        public OrderTableLoadStrategy(
            ITableOrderRepository phieuRepo,
            IPhieuTmpRepository phieuTmpRepo,
            IIFSRepository ifsRepo,
            IRowCategoryFilter rowCategoryFilter)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _phieuTmpRepo = phieuTmpRepo ?? throw new ArgumentNullException(nameof(phieuTmpRepo));
            _ifsRepo = ifsRepo ?? throw new ArgumentNullException(nameof(ifsRepo));
            _rowCategoryFilter = rowCategoryFilter ?? throw new ArgumentNullException(nameof(rowCategoryFilter));
        }

        public DataTable LoadDonHangGoc(OrderLoadContext ctx)
        {
            var d = ctx.Cfg.Delivery;
            bool isSP = ctx.Category == OrderCategory.SP;

            var donHang = _phieuRepo.LoadPhieuTuBangRieng(
                ctx.NgayGiao.ToString("yyyy-MM-dd"),
                string.Join(",", ctx.CheckedGios ?? new List<string>()),
                isSP, d.DockCodeSP, ctx.Cfg);

            // ✅ MỚI — build sẵn ifsDataDaLoc ngay tại bước load, để SoSanhVoiIFS
            // chỉ việc đọc lại, không tự query/lọc lần 2 (tránh trùng lặp + lệch
            // quy tắc như bản SoSanhDonHangVoiIFS cũ đã gặp).
            try
            {
                string ngayXuatIFS = ctx.NgayGiao.ToString("ddMMyyyy");
                DataTable ifsData = _ifsRepo.GetFullCustomerOrder(ngayXuatIFS, ctx.Cfg);

                if (d.CoGear)
                    ifsData = GioRowFilter.Filter(ifsData, ctx.CheckedGios);

                if (d.CoLoaiSP)
                    ifsData = _rowCategoryFilter.Filter(ifsData, ctx.Category, ctx.Cfg);

                ctx.IfsDataDaLoc = ifsData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OrderTableLoadStrategy.LoadDonHangGoc] Lỗi lấy IFS để so sánh: {ex.Message}");
                ctx.IfsDataDaLoc = null;
                ctx.IfsLoadError = "⚠ Không kết nối được IFS để so sánh lệch. " + ex.Message;
            }

            return donHang;
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
            return _phieuRepo.SoSanhDonHangVoiIFS(donHang, ctx.IfsDataDaLoc, ctx.Cfg);
        }
    }
}