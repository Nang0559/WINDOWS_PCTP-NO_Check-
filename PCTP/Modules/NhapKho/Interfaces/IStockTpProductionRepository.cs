using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
