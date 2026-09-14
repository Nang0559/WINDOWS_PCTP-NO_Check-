using PCTP.ClassSQL;
using PCTP.Infrastructure.Stock;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Application.Contracts.Stock;
using PCTP.Modules.KhoCore.Application.Services;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoCore.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Modules.NhapKho.Application.Adapters;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Modules.XuatKho.Application.Adapters;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Repository;
using System;

namespace PCTP.Modules.KhoVatLy
{
    /// <summary>
    /// Nơi DUY NHẤT dựng dependency graph cho module "Nhập TP".
    /// Form chỉ gọi Build() và nhận về Service — không tự new bất kỳ Repository nào.
    /// </summary>
    public static class NhapTpModuleFactory
    {
        public sealed class Module
        {
            public INhapTpReceivingService NhapTpService { get; set; }
            public ISlotService SlotService { get; set; }
            public IWarehouseService WarehouseService { get; set; }
            public IInspectionService InspectionService { get; set; }
        }

        public static Module Build()
        {
            var dbExecutor = new PhieuSqlExecutor(new SQLPROVIDER());
            var uow = new UnitOfWork(dbExecutor.Sql);

            var stockTpRepo = new StockTpRepository(dbExecutor, uow);
            var phieuTrackRepo = new PhieuTrackingRepository(dbExecutor, uow);
            var caseRepo = new StockTpCaseRepository(dbExecutor, uow);
            var productionRepo = new StockTpProductionRepository(dbExecutor, uow);
            var slotRepo = new SlotRepository(dbExecutor, uow);
            var statusRepo = new StockTpStatusRepository(dbExecutor, uow);
            var historyRepo = new StockHistoryRepository(dbExecutor, uow);
            var warehouseRepo = new WarehouseRepository(dbExecutor, uow);
            var rackRepo = new RackRepository(dbExecutor, uow);
            var inspectionLogRepo = new InspectionLogRepository(dbExecutor, uow);

            var slotService = new SlotService(slotRepo);
            var warehouseService = new WarehouseService(warehouseRepo, rackRepo, uow);
            var inspectionService = new InspectionService(inspectionLogRepo);

            // Central stock mutation boundary for NhapKho.
            IStockSlotRepository stockSlotRepository =
                new LegacyStockSlotRepositoryAdapter(slotService);
            IStockBalanceRepository stockBalanceRepository =
                new StockExportRepositoryAdapter(
                    new PCTP.Modules.XuatKho.Repositories.StockExportRepository(dbExecutor, uow));
            IStockReceivingRepository stockReceivingRepository =
                new StockReceivingRepositoryAdapter(stockTpRepo);

            var stockMovement = new StockMovementService(
                stockBalanceRepository,
                stockSlotRepository,
                stockReceivingRepository);

            var nhapTpService = new NhapTpReceivingService(
                uow,
                stockTpRepo,
                phieuTrackRepo,
                caseRepo,
                productionRepo,
                slotService,
                historyRepo,
                statusRepo,
                stockMovement);

            return new Module
            {
                NhapTpService = nhapTpService,
                SlotService = slotService,
                WarehouseService = warehouseService,
                InspectionService = inspectionService
            };
        }
    }
}