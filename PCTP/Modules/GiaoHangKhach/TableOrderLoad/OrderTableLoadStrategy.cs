using PCTP.Domain.Interfaces;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.TableOrderLoad
{
    /// <summary>
    /// Customer dùng bảng riêng (YMVN, HTN, GiaoDB, và mọi customer upload PO
    /// trong tương lai). Đơn hàng gốc nằm trong 1 bảng SQL cụ thể theo Cfg —
    /// KHÔNG cho phép nhập tay trên grid nữa (theo yêu cầu), chỉ upload file.
    /// </summary>
    public class OrderTableLoadStrategy : IOrderLoadStrategy
    {
        private readonly ITableOrderRepository _phieuRepo;

        public OrderTableLoadStrategy(ITableOrderRepository phieuRepo)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
        }

        public DataTable LoadDonHangGoc(OrderLoadContext ctx)
        {
            // Đúng bản đã có sẵn — LoadPhieuTuBangRieng tự MergeLotTuBangRieng bên trong rồi,
            // nên MergeLotDaLuu ở dưới sẽ KHÔNG cần làm gì thêm cho nhánh này (xem ghi chú mục 5).
            string tenBang = ctx.CheDoGiaoDacBiet && ctx.Cfg.CoGiaoDacBiet
                ? ctx.Cfg.OrderTableGiaoDacBiet
                : ctx.Cfg.OrderTable;

            // LoadPhieuTuBangRieng hiện đọc cứng từ ctx.Cfg.OrderTable — cần overload
            // nhận tên bảng tường minh để hỗ trợ OrderTableGiaoDacBiet (xem mục 6).
            return _phieuRepo.LoadPhieuTuBangRieng(
                ctx.NgayGiao.ToString("yyyy-MM-dd"),
                string.Join(",", ctx.CheckedGios ?? new List<string>()),
                ctx.IsLoaiSP, ctx.Cfg.DockCodeSP, ctx.Cfg, tenBang);
        }

        public void MergeLotDaLuu(DataTable donHang, OrderLoadContext ctx)
        {
            // KHÔNG LÀM GÌ — LoadPhieuTuBangRieng đã tự gọi MergeLotTuBangRieng() nội bộ.
            // Giữ method rỗng ở đây để interface nhất quán, nhưng tránh merge trùng lặp 2 lần.
        }

        public void SyncChoDocQR(DataTable donHang, OrderLoadContext ctx)
        {
            _phieuRepo.LuuVaLoad(ctx.Cfg.OrderTable, "Usp_Qrcode_LOAD_PHIEU_DOCQR2405",
                donHang, ctx.NgayGiao.ToString("yyyy-MM-dd"), ctx.NhaMay,
                ctx.GioFccMoTa, ctx.AddNm, ctx.Cfg.TmpTable, ctx.Cfg.DocQRTable);
        }

        /// <summary>
        /// BẮT BUỘC theo yêu cầu: bảng riêng luôn phải đối chiếu với IFS thật
        /// để phát hiện đơn hàng bị thiếu/lệch số lượng giữa 2 nguồn.
        /// </summary>
        public DataTable SoSanhVoiIFS(DataTable donHang, OrderLoadContext ctx)
        {
            return _phieuRepo.SoSanhDonHangVoiIFS(
                donHang, ctx.NgayGiao.ToString("yyyy-MM-dd"), ctx.Cfg);
        }
    }
}
