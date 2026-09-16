using PCTP.ClassSQL;
using PCTP.Infrastructure.Stock;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoCore.Application.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuLyHangLoi.Application.Adapters;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.Shared.Common;
using PCTP.Shared.UiMd;

namespace PCTP.Modules.XuLyHangLoi.Application
{
    public static class XuLyHangLoiModuleFactory
    {
        public sealed class Module
        {
            public IUnitOfWork UnitOfWork { get; internal set; }
            public ISlotService SlotService { get; internal set; }
            public IStockMovementService StockMovement { get; internal set; }
            public IReworkStockService ReworkStockService { get; internal set; }
            public IReworkPhase4Service ReworkPhase4Service { get; internal set; }
            public IAffectedLotTraceService AffectedLotTraceService { get; internal set; }
            public IProductionLotTraceProvider ProductionLotTraceProvider { get; internal set; }
            public IInitialQCService InitialQCService { get; internal set; }
            public IHangLoiPhase5To9Service Phase5To9Service { get; internal set; }
            public IWorkflowTransitionService Workflow { get; internal set; }
            public IStockExportRepository StockExportRepo { get; internal set; }
            public IStockHistoryRepository StockHistoryRepo { get; internal set; }
            public IPhieuXuLyBatThuongRepository PhieuXuLyRepo { get; internal set; }
            public ITraHangQTChungRepository QTChungRepo { get; internal set; }
            public IPhieuTraHangRepository PhieuTraHangRepo { get; internal set; }
        }

        public static Module Build()
        {
            var dbExecutor = new PhieuSqlExecutor(new SQLPROVIDER());
            var uow = new UnitOfWork(dbExecutor.Sql);
            return Build(dbExecutor, uow);
        }

        public static Module Build(PhieuSqlExecutor dbExecutor, IUnitOfWork uow)
        {
            if (dbExecutor == null) throw new System.ArgumentNullException(nameof(dbExecutor));
            if (uow == null) throw new System.ArgumentNullException(nameof(uow));

            var slotRepo = new SlotRepository(dbExecutor, uow);
            var slotService = new SlotService(slotRepo);
            var stockTpRepo = new StockExportRepository(dbExecutor, uow);
            var historyRepo = new StockHistoryRepository(dbExecutor, uow);
            var phieuXuLyRepo = new PhieuXuLyBatThuongRepository(dbExecutor, uow);
            var phieuTraHangRepo = new PhieuTraHangRepository(dbExecutor, uow);
            var qtChungRepo = new TraHangQTChungRepository(dbExecutor, uow);

            IStockBalanceRepository stockBalance = new ReworkStockBalanceAdapter(stockTpRepo);
            IStockSlotRepository stockSlot = new ReworkStockSlotAdapter(slotService);
            var stockMovement = new StockMovementService(stockBalance, stockSlot);

            var reworkStockService = new ReworkStockService(
                uow, slotService, stockTpRepo, stockMovement, historyRepo, qtChungRepo, phieuXuLyRepo);

            var productionProvider = new ProductionLotTraceProvider(dbExecutor, uow);
            var customerReturnProvider = new CustomerReturnLotTraceProvider(phieuTraHangRepo);
            var affectedLotTraceService = new AffectedLotTraceService(
                reworkStockService, productionProvider, customerReturnProvider,
                phieuXuLyRepo, dbExecutor, uow);

            var initialQcService = new InitialQCService(
                dbExecutor, uow, phieuXuLyRepo, affectedLotTraceService);

            var reworkPhase4Service = new ReworkPhase4Service(
                phieuXuLyRepo, qtChungRepo, initialQcService);

            var workflowRepository = new WorkflowRepository(dbExecutor, uow);
            var workflowService = new WorkflowTransitionService(workflowRepository);
            var phase5To9Service = new HangLoiPhase5To9Service(
                dbExecutor, uow, phieuXuLyRepo, initialQcService,
                reworkPhase4Service, workflowService);

            return new Module
            {
                UnitOfWork = uow,
                SlotService = slotService,
                StockMovement = stockMovement,
                ReworkStockService = reworkStockService,
                ReworkPhase4Service = reworkPhase4Service,
                AffectedLotTraceService = affectedLotTraceService,
                ProductionLotTraceProvider = productionProvider,
                InitialQCService = initialQcService,
                Phase5To9Service = phase5To9Service,
                Workflow = workflowService,
                StockExportRepo = stockTpRepo,
                StockHistoryRepo = historyRepo,
                PhieuXuLyRepo = phieuXuLyRepo,
                QTChungRepo = qtChungRepo,
                PhieuTraHangRepo = phieuTraHangRepo
            };
        }
    }
}
