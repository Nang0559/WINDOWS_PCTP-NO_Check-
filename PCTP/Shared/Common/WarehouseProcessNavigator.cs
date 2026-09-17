using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.HVN;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoVatLy;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.NhapKho;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuatKho.Services;
using PCTP.Modules.XuLyHangLoi;
using PCTP.Modules.XuLyHangLoi.Application;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.Shared.Common;
using PCTP.Shared.Models;
using PCTP.Shared.UiMd;
using PCTP.YMN;
using System;
using System.Windows.Forms;

namespace PCTP.Common
{
    public static class WarehouseProcessNavigator
    {
        private static IPhieuLotRepository CreatePhieuLoiRepo()
        {
            var provider = new SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);
            return new PhieuLotRepository(sql, uow);
        }

        private static IPhieuTraHangRepository CreatePhieuTraHangRepo()
        {
            var provider = new SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);
            return new PhieuTraHangRepository(sql, uow);
        }

        public static void OpenBanDoKho(IWin32Window owner)
        {
            var f = new MainStockSV();
            f.Show();
        }

        public static void OpenNhapKhoTienTrinh(IWin32Window owner, MainStockSV mainStock)
        {
            if (mainStock == null) throw new ArgumentNullException(nameof(mainStock));
            var module = MainStockModuleFactory.Build();
            using (var f = new FormNhapKhoTienTrinh(mainStock, module.DashboardService))
                f.ShowDialog(owner);
        }

        public static void OpenQCDinhHuong(IWin32Window owner, int? preselectId = null)
        {
            OpenQuanLyTienTrinhHangLoi(owner, preselectId);
        }

        public static void OpenQCXacNhanCuoi(IWin32Window owner, int? preselectId = null)
        {
            OpenQuanLyTienTrinhHangLoi(owner, preselectId);
        }

        private static IGiaoBuNGService CreateGiaoBuNGService(
            PhieuSqlExecutor sql,
            IUnitOfWork uow,
            ISlotService slotService,
            IStockMovementService stockMovement)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));
            if (uow == null) throw new ArgumentNullException(nameof(uow));
            if (slotService == null) throw new ArgumentNullException(nameof(slotService));
            if (stockMovement == null) throw new ArgumentNullException(nameof(stockMovement));

            var coreHistoryRepo = new StockHistoryRepository(sql, uow);
            var stockExportRepo = new StockExportRepository(sql, uow);
            var stockHistoryRepo = new StockHistoryRepository(sql, uow);
            var stockExportHistoryRepo = new StockExportHistoryRepository(sql, uow, coreHistoryRepo);
            var choGiaoRepo = new HangChoGiaoRepository(sql, uow);
            var validationService = new StockExportValidationService(stockExportRepo, stockExportHistoryRepo);
            var stockExportService = new StockExportService(
                uow,
                slotService,
                stockHistoryRepo,
                choGiaoRepo,
                validationService,
                stockMovement,
                stockExportHistoryRepo);

            return new GiaoBuNGService(stockExportService, choGiaoRepo, slotService);
        }

        public static void OpenQuanLyTienTrinhHangLoi(IWin32Window owner, int? preselectPhieuXuLyId = null)
        {
            using (var f = CreateFormQuanLyTienTrinhHangLoi(preselectPhieuXuLyId))
                f.ShowDialog(owner);
        }

        private static Modules.XuLyHangLoi.FormQuanLyTienTrinhHangLoi CreateFormQuanLyTienTrinhHangLoi(int? preselectPhieuXuLyId = null)
        {
            var provider = new SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);

            var phieuTraHangRepo = new PhieuTraHangRepository(sql, uow);
            var phieuGiaoRepo = new PhieuGiaoRepository(sql, uow);
            var workflowRepo = new WorkflowRepository(sql, uow);
            var workflow = new WorkflowTransitionService(workflowRepo);
            var workflowEngine = new WorkflowEngine(workflowRepo);

            var module = XuLyHangLoiModuleFactory.Build(sql, uow);
            var phieuXuLyRepo = module.PhieuXuLyRepo;
            var qtChungRepo = module.QTChungRepo;
            var slotService = module.SlotService;
            var reworkStockService = module.ReworkStockService;

            var giaoBuNGService = CreateGiaoBuNGService(sql, uow, slotService, module.StockMovement);

            var qtChungService = new QTChungService(
                phieuXuLyRepo,
                phieuTraHangRepo,
                reworkStockService,
                giaoBuNGService,
                uow,
                qtChungRepo,
                workflow,
                module.AffectedLotTraceService);

            var khachTraHangService = new KhachTraHangService(
                qtChungService,
                phieuTraHangRepo,
                phieuGiaoRepo,
                phieuXuLyRepo,
                uow,
                workflow);

            var traNoiBoService = new TraNoiBoService(
                phieuTraHangRepo,
                workflowEngine,
                uow,
                workflow);

            return new FormQuanLyTienTrinhHangLoi(
                khachTraHangService,
                traNoiBoService,
                qtChungService,
                reworkStockService,
                giaoBuNGService,
                phieuTraHangRepo,
                phieuXuLyRepo,
                qtChungRepo,
                phieuGiaoRepo,
                slotService,
                preselectPhieuXuLyId,
                module.AffectedLotTraceService,
                module.InitialQCService);
        }

        public static void OpenGiaoHangHVN(string customerNo)
        {
            if (string.IsNullOrWhiteSpace(customerNo))
                throw new ArgumentException("CustomerNo không được để trống.", nameof(customerNo));

            CustomerTableConfig.GetForDelivery(customerNo);
            var frm = new HVN_PGH(customerNo);
            frm.Show();
        }
    }
}