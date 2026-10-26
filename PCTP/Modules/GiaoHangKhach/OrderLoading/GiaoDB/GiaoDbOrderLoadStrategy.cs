using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Models;
using System;
using System.Data;

namespace PCTP.Modules.GiaoHangKhach.OrderLoading.GiaoDB
{
    /// <summary>
    /// Business scenario "Giao đặc biệt" (roadmap mục 2.3) — đơn hàng KHÔNG đến từ IFS,
    /// mà từ Upload Excel/nhập tay (FRM_UploadGiaoDB) → TMPPHIEUGIAOHANGDBCT → xử lý qua
    /// Usp_Qrcode_LOAD_PHIEU_DOCQR2405 → TMPPHIEUGIAOHANGDB (bảng output đã có LOT/STATUS).
    /// Độc lập với TableOrder (mục 2.4): không có bước so sánh IFS baseline.
    /// </summary>
    public class GiaoDbOrderLoadStrategy 
    {
        private readonly IPhieuGiaoDBRepository _giaoDbRepo;

        public GiaoDbOrderLoadStrategy(IPhieuGiaoDBRepository giaoDbRepo)
        {
            _giaoDbRepo = giaoDbRepo ?? throw new ArgumentNullException(nameof(giaoDbRepo));
        }

        public DataTable LoadDonHangGoc(OrderLoadContext ctx)
        {
            // TMPPHIEUGIAOHANGDB đã là bảng OUTPUT sau SP — đã có LOT/STATUS thật,
            // lọc đúng nhà máy/ngày đang chọn (xem PhieuGiaoDBRepository.LoadTmpPhieuGiaoDB).
            return _giaoDbRepo.LoadTmpPhieuGiaoDB("TMPPHIEUGIAOHANGDB", ctx.NgayGiao, ctx.AddNm);
        }

        public void MergeLotDaLuu(DataTable donHang, OrderLoadContext ctx)
        {
            // KHÔNG LÀM GÌ — LOT đã được SP ghi thẳng vào TMPPHIEUGIAOHANGDB tại thời điểm
            // Upload (LuuGiaoDB), không phải tại thời điểm Load lại như IFS/TableOrder.
        }

        public void SyncChoDocQR(DataTable donHang, OrderLoadContext ctx)
        {
            // KHÔNG LÀM GÌ — TMPPHIEUGIAOHANGDB CHÍNH LÀ bảng làm việc cho QR rồi.
            // Việc "sync" đã xảy ra 1 lần tại Upload (PhieuService.XuLySauUploadGiaoDB
            // → LuuGiaoDB → SP), không lặp lại mỗi lần Load như IFS/TableOrder.
        }

        public DataTable SoSanhVoiIFS(DataTable donHang, OrderLoadContext ctx)
        {
            // Roadmap mục 2.4: "GiaoDB: không cần IFS" — không có gì để so sánh.
            return new DataTable();
        }
    }
}