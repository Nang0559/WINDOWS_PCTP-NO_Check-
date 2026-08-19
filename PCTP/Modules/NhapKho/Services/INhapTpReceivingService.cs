using PCTP.Shared.Helpers;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Services
{
    /// <summary>
    /// Toàn bộ nghiệp vụ "Nhập TP từ sản xuất vào Slot" — Form CHỈ được biết
    /// interface này. Mọi Repository liên quan (StockTp, PhieuTracking, Case,
    /// Production, Status, Slot, History) đều bị che sau tầng này.
    /// </summary>
    public interface INhapTpReceivingService
    {
        // ── Nhập kho ─────────────────────────────────────────────────────
        ScanResult KiemTraTruocKhiNhap(QRCodeInfo qr);

        ScanResult NhapTpVaoSlot(
            QRCodeInfo qr,
            int slotId,
            PhieuNhapInfo matchedPhieu = null);

        // ── Trạng thái LOT ───────────────────────────────────────────────
        void MoLaiLot(string lot, string find = null);

        bool KiemTraKhopTonKho(
            string lotNo,
            out int slActive,
            out int slConLaiStockTp);

        // ── Tra cứu phiếu sản xuất (trước là gọi thẳng IStockTpProductionRepository) ──
        /// <summary>Danh sách phiếu đang sản xuất trong N ngày gần đây — dùng bind grid.</summary>
        List<PhieuNhapInfo> GetPhieuDangSanXuat(int soNgayGanDay = 30);

        /// <summary>Lấy lại 1 phiếu theo Find — dùng để refresh dòng grid sau khi có sự kiện đổi trạng thái.</summary>
        PhieuNhapInfo GetPhieuByFind(string find);

        /// <summary>Đối chiếu LOT quét từ QR với phiếu sản xuất — trả null nếu không khớp/không rõ ràng.</summary>
        PhieuNhapInfo TimPhieuTheoLotQR(string rawLotNoSL, string maHang);
    }
}
