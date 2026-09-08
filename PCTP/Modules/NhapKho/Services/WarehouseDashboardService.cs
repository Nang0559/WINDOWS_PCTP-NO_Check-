using PCTP.Modules.NhapKho.Interfaces;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.NhapKho.Services
{
    public sealed class WarehouseDashboardService : IWarehouseDashboardService
    {
        private readonly INhapKhoDashboardRepository _repo;

        public WarehouseDashboardService(INhapKhoDashboardRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        // ============================================================
        // DASHBOARD TỔNG QUAN
        // ============================================================

        public int GetTongTonStockTp()
        {
            return _repo.GetTongTonStockTp();
        }

        public int GetTongTonRackThat()
        {
            return _repo.GetTongTonRackThat();
        }

        public int GetTongTonKhoTam()
        {
            return _repo.GetTongTonKhoTam();
        }

        // ============================================================
        // TIẾN TRÌNH NHẬP KHO
        // ============================================================

        public int DemPhieuChoNhap()
        {
            return _repo.DemPhieuChoNhap();
        }

        public DataTable GetGridChoNhap()
        {
            return _repo.GetGridChoNhap();
        }

        public int DemDaNhapHomNay()
        {
            return _repo.DemDaNhapHomNay();
        }

        public DataTable GetGridDaNhapHomNay()
        {
            return _repo.GetGridDaNhapHomNay();
        }

        // ============================================================
        // ĐỐI CHIẾU
        // ============================================================

        public int DemLechDoiChieu()
        {
            return _repo.DemLechDoiChieu();
        }

        public DataTable GetGridLechDoiChieu()
        {
            return _repo.GetGridLechDoiChieu();
        }
    }
}
