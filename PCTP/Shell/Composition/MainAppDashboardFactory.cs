using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.NhapKho;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shell.Widgets;
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
        private readonly IHangChoGiaoRepository _hangChoGiaoRepository;

        internal MainAppDashboardFactory()
        {
            ClassSQL.SQLPROVIDER provider = new ClassSQL.SQLPROVIDER();
            PhieuSqlExecutor sql = new PhieuSqlExecutor(provider);
            UnitOfWork uow = new UnitOfWork(provider);

            _phieuXuLyRepository = new PhieuXuLyBatThuongRepository(sql, uow);
            _dashboardRepository = new NhapKhoDashboardRepository(sql, uow);
            _hangChoGiaoRepository = new HangChoGiaoRepository(sql, uow);
        }

        internal WarehouseDashboardBar Create(Action openNhapKho, Action openHangChoGiao)
        {
            if (openNhapKho == null)
                throw new ArgumentNullException("openNhapKho");
            if (openHangChoGiao == null)
                throw new ArgumentNullException("openHangChoGiao");

            return new WarehouseDashboardBar(
                _phieuXuLyRepository,
                _dashboardRepository,
                _hangChoGiaoRepository,
                openNhapKho,
                openHangChoGiao);
        }

        internal WmsFgHomePanel CreateFgHome(Action openNhapKho, Action openHangChoGiao, Action openTonKho)
        {
            if (openNhapKho == null)
                throw new ArgumentNullException("openNhapKho");
            if (openHangChoGiao == null)
                throw new ArgumentNullException("openHangChoGiao");
            if (openTonKho == null)
                throw new ArgumentNullException("openTonKho");

            return new WmsFgHomePanel(
                _phieuXuLyRepository,
                _dashboardRepository,
                _hangChoGiaoRepository,
                openNhapKho,
                openHangChoGiao,
                openTonKho);
        }

        internal WmsWorklistBar CreateWorklist(Action openNhapKho, Action openHangChoGiao)
        {
            if (openNhapKho == null)
                throw new ArgumentNullException("openNhapKho");
            if (openHangChoGiao == null)
                throw new ArgumentNullException("openHangChoGiao");

            return new WmsWorklistBar(
                _phieuXuLyRepository,
                _dashboardRepository,
                _hangChoGiaoRepository,
                openNhapKho,
                openHangChoGiao);
        }
    }
}
