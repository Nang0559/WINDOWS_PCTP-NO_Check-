using PCTP.ClassSQL;
using PCTP.Infrastructure.Stock;
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
    /// <summary>
    /// Composition root cho luồng XuLyHangLoi.
    ///
    /// Quy tắc:
    /// - Một UnitOfWork duy nhất cho toàn bộ graph.
    /// - IStockMovementService là boundary duy nhất cho mutation stock.
    /// - Adapter legacy nằm ngoài KhoCore.
    /// - UI nhận service đã dựng sẵn, không tự new repository/SQL.
    /// </summary>
    public static class XuLyHangLoiModuleFactory
    {
        public sealed class Module
        {
            public IUnitOfWork UnitOfWork { get; internal set; }
            public ISlotService SlotService { get; internal set; }
            public IStockMovementService StockMovement { get; internal set; }
            public IReworkStockService ReworkStockService { get; internal set; }
            public IPhieuXuLyBatThuongRepository PhieuXuLyRepo { get; internal set; }
            public ITraHangQTChungRepository QTChungRepo { get; internal set; }
        }

        public static Module Build()
        {
            var dbExecutor = new PhieuSqlExecutor(new SQLPROVIDER());
            var uow = new UnitOfWork(dbExecutor.Sql);

            // Legacy storage adapters remain transitional infrastructure.
            var slotRepo = new SlotRepository(dbExecutor, uow);
            var slotService = new SlotService(slotRepo);
            var stockTpRepo = new StockExportRepository(dbExecutor, uow);
            var historyRepo = new StockHistoryRepository(dbExecutor, uow);

            var phieuXuLyRepo = new PhieuXuLyBatThuongRepository(dbExecutor, uow);
            var qtChungRepo = new TraHangQTChungRepository(dbExecutor, uow);

            IStockBalanceRepository stockBalance =
                new ReworkStockBalanceAdapter(stockTpRepo);
            IStockSlotRepository stockSlot =
                new ReworkStockSlotAdapter(slotService);

            var stockMovement = new StockMovementService(
                stockBalance,
                stockSlot,
                null);

            var reworkStockService = new ReworkStockService(
                uow,
                slotService,
                stockTpRepo,
                stockMovement,
                historyRepo,
                qtChungRepo,
                phieuXuLyRepo);

            return new Module
            {
                UnitOfWork = uow,
                SlotService = slotService,
                StockMovement = stockMovement,
                ReworkStockService = reworkStockService,
                PhieuXuLyRepo = phieuXuLyRepo,
                QTChungRepo = qtChungRepo
            };
        }
    }
}
