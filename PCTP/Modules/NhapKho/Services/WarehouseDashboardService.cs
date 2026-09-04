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
            => _repo = repo ?? throw new ArgumentNullException(nameof(repo));

        public int GetTongTonStockTp() => _repo.GetTongTonStockTp();
        public int GetTongTonRackThat() => _repo.GetTongTonRackThat();
        public int GetTongTonKhoTam() => _repo.GetTongTonKhoTam();
        public int DemLechDoiChieu() => _repo.DemLechDoiChieu();
        public DataTable GetGridLechDoiChieu() => _repo.GetGridLechDoiChieu();
    }
}
