using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Interfaces
{
    public interface IWarehouseDashboardService
    {
        // ============================================================
        // DASHBOARD TỔNG QUAN
        // ============================================================

        int GetTongTonStockTp();
        int GetTongTonRackThat();
        int GetTongTonKhoTam();

        // ============================================================
        // TIẾN TRÌNH NHẬP KHO
        // ============================================================

        int DemPhieuChoNhap();
        DataTable GetGridChoNhap();

        int DemDaNhapHomNay();
        DataTable GetGridDaNhapHomNay();

        // ============================================================
        // ĐỐI CHIẾU
        // ============================================================

        int DemLechDoiChieu();
        DataTable GetGridLechDoiChieu();
    }
}
