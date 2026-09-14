using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.NhapKho;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shell.Widgets;
using System;
using System.Windows.Forms;

namespace PCTP.Shell.Composition
{
    /// <summary>
    /// Composition root for the legacy dashboard hosted by Main_APP.
    ///
    /// Main_APP must not know how dashboard repositories are constructed.
    /// This factory is the temporary migration boundary until the dashboard
    /// data source is fully moved behind application contracts.
    /// </summary>
    internal sealed class MainAppDashboardFactory
    {
        private readonly ClassSQL.SQLPROVIDER _provider;

        internal MainAppDashboardFactory()
        {
            _provider = new ClassSQL.SQLPROVIDER();
        }

        internal WarehouseDashboardBar Create(Action openNhapKho)
        {
            if (openNhapKho == null)
                throw new ArgumentNullException("openNhapKho");

            PhieuSqlExecutor sql = new PhieuSqlExecutor(_provider);
            UnitOfWork uow = new UnitOfWork(_provider);

            IPhieuXuLyBatThuongRepository phieuXuLyRepository =
                new PhieuXuLyBatThuongRepository(sql, uow);

            INhapKhoDashboardRepository dashboardRepository =
                new NhapKhoDashboardRepository(sql, uow);

            return new WarehouseDashboardBar(
                phieuXuLyRepository,
                dashboardRepository,
                openNhapKho)
            {
                Dock = DockStyle.Top
            };
        }
    }
}
