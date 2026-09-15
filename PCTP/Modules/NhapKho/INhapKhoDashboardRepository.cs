
using System.Data;


namespace PCTP.Modules.NhapKho
{
    public interface INhapKhoDashboardRepository
    {
        int DemPhieuChoNhap();       // vNhapTP: SlDaNhap < SlSanXuat, chưa KetThucLot
        int DemDaNhapHomNay();       // SlotLot.CreatedDate >= hôm nay
        int DemLechDoiChieu();       // STOCKTP.SLCONLAI != SUM(SlotLot Active theo LOT)

        DataTable GetGridChoNhap();
        DataTable GetGridDaNhapHomNay();
        DataTable GetGridLechDoiChieu();
        // ── THÊM cho Dashboard bar của MainStockSV ──
        int GetTongTonStockTp();
        int GetTongTonRackThat();
        int GetTongTonKhoTam();
    }
}
