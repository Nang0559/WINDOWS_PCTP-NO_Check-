using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.NhapKho;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shell.Widgets;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Windows.Forms;

namespace PCTP.Shell.Composition
{
    /// <summary>
    /// Composition root for the WMS Shell dashboard/worklist data sources.
    /// Repository instances are shared so both widgets read through the same
    /// provider/unit-of-work boundary instead of creating duplicate data sources.
    /// </summary>
    internal sealed class MainAppDashboardFactory
    {
        private readonly IPhieuXuLyBatThuongRepository _phieuXuLyRepository;
        private readonly INhapKhoDashboardRepository _dashboardRepository;

        internal MainAppDashboardFactory()
        {
            ClassSQL.SQLPROVIDER provider = new ClassSQL.SQLPROVIDER();
            PhieuSqlExecutor sql = new PhieuSqlExecutor(provider);
            UnitOfWork uow = new UnitOfWork(provider);

            _phieuXuLyRepository = new PhieuXuLyBatThuongRepository(sql, uow);
            _dashboardRepository = new NhapKhoDashboardRepository(sql, uow);
        }

        internal WarehouseDashboardBar Create(Action openNhapKho)
        {
            if (openNhapKho == null)
                throw new ArgumentNullException("openNhapKho");

            return new WarehouseDashboardBar(
                _phieuXuLyRepository,
                _dashboardRepository,
                openNhapKho);
        }

        internal WmsWorklistBar CreateWorklist(Action openNhapKho)
        {
            if (openNhapKho == null)
                throw new ArgumentNullException("openNhapKho");

            return new WmsWorklistBar(
                _phieuXuLyRepository,
                _dashboardRepository,
                openNhapKho);
        }
    }
}
