using PCTP.Shared.Models;
using System.Collections.Generic;


namespace PCTP.Modules.NhapKho.Repository
{
    public interface IStockTpProductionRepository
    {
        // ── Phiếu sản xuất ──────────────────────────────────────

        PhieuNhapInfo GetPhieuByFind(string find);

        List<PhieuNhapInfo> GetPhieuTong();

        List<PhieuNhapInfo> GetPhieuDangSanXuat(
            int soNgayGanDay = 30);

        // ── Tìm phiếu từ QR ─────────────────────────────────────

        PhieuNhapInfo TimPhieuTheoLotQR(
            string rawLotNoSL,
            string maHang);
    }
}
