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
            public IAffectedLotTraceService AffectedLotTraceService { get; internal set; }
            public IProductionLotTraceProvider ProductionLotTraceProvider { get; internal set; }
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
            if (dbExecutor == null)
                throw new System.ArgumentNullException(nameof(dbExecutor));
            if (uow == null)
                throw new System.ArgumentNullException(nameof(uow));

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
                uow,
                slotService,
                stockTpRepo,
                stockMovement,
                historyRepo,
                qtChungRepo,
                phieuXuLyRepo);

            // Phase 2: cả 3 nguồn trace đều phải có contract thật.
            // Production/WIP dùng trực tiếp view vNhapTP hiện đang là nguồn của
            // StockTpProductionRepository; không giả định WIP = 0.
            var productionProvider = new ProductionLotTraceProvider(dbExecutor, uow);
            var customerReturnProvider = new CustomerReturnLotTraceProvider(phieuTraHangRepo);

            var affectedLotTraceService = new AffectedLotTraceService(
                reworkStockService,
                productionProvider,
                customerReturnProvider,
                phieuXuLyRepo,
                dbExecutor,
                uow);

            return new Module
            {
                UnitOfWork = uow,
                SlotService = slotService,
                StockMovement = stockMovement,
                ReworkStockService = reworkStockService,
                AffectedLotTraceService = affectedLotTraceService,
                ProductionLotTraceProvider = productionProvider,
                StockExportRepo = stockTpRepo,
                StockHistoryRepo = historyRepo,
                PhieuXuLyRepo = phieuXuLyRepo,
                QTChungRepo = qtChungRepo,
                PhieuTraHangRepo = phieuTraHangRepo
            };
        }
    }
}
