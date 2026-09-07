using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoCore.Interfaces;
using PCTP.Modules.KhoCore.Repositories;
using PCTP.Modules.KhoCore.Services;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Modules.NhapKho.Interfaces;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuatKho.Services;
using PCTP.Shared.Common;
using PCTP.Shared.Services;
using PCTP.VIEWSTOCK.Repository;

namespace PCTP.Modules.KhoVatLy
{
    /// <summary>
    /// Nơi DUY NHẤT dựng dependency graph cho MainStockSV (bản đồ Canvas kho).
    /// Form chỉ gọi Build() và nhận về Service — không tự new SQLPROVIDER/Repository nào.
    /// Toàn bộ Service dùng CHUNG 1 UnitOfWork để đảm bảo transaction nhất quán
    /// khi 1 thao tác (vd xuất kho) đụng tới nhiều Repository.
    /// </summary>
    public static class MainStockModuleFactory
    {
        public sealed class Module
        {
            public ISlotService SlotService { get; set; }
            public IWarehouseService WarehouseService { get; set; }
            public IRackService RackService { get; set; }
            public IStockExportService ExportService { get; set; }
            public IPrintService PrintService { get; set; }
            public IWarehouseDashboardService DashboardService { get; set; }
            public IInspectionConfigService InspectionConfigService { get; set; }
            public IInspectionLogRepository InspectionLogRepo { get; set; }

            public IStockTpLookupService StockTpLookupService { get; set; }
        }

        public static Module Build()
        {
            var dbExecutor = new PhieuSqlExecutor(new SQLPROVIDER());
            var uow = new UnitOfWork(dbExecutor.Sql);

            var slotRepo = new SlotRepository(dbExecutor, uow);
            var warehouseRepo = new WarehouseRepository(dbExecutor, uow);
            var rackRepo = new RackRepository(dbExecutor, uow);
            var historyRepo = new StockHistoryRepository(dbExecutor, uow);
            var stockExportRepo = new StockExportRepository(dbExecutor, uow);
            var hangChoGiaoRepo = new HangChoGiaoRepository(dbExecutor, uow);
            var exportHistoryRepo = new StockExportHistoryRepository(dbExecutor, uow, historyRepo);
            var dashRepo = new NhapKhoDashboardRepository(dbExecutor, uow);
            var inspectionConfigRepo = new InspectionConfigRepository(dbExecutor, uow);
            var inspectionLogRepo = new InspectionLogRepository(dbExecutor, uow);
            var stockTpRepo = new StockTpRepository(dbExecutor, uow);
            var stockTpLookupService = new StockTpLookupService(stockTpRepo);
            var slotService = new SlotService(slotRepo);
            var warehouseService = new WarehouseService(warehouseRepo, rackRepo, uow);
            var rackService = new RackService(rackRepo);
            var exportValidationService = new StockExportValidationService(stockExportRepo, exportHistoryRepo);
            var exportService = new StockExportService(
                uow, slotService, stockExportRepo, historyRepo, hangChoGiaoRepo, exportValidationService);
            var printService = new PrintService(slotService, warehouseService);
            var dashboardService = new WarehouseDashboardService(dashRepo);
            var inspectionConfigService = new InspectionConfigService(inspectionConfigRepo);

            return new Module
            {
                SlotService = slotService,
                WarehouseService = warehouseService,
                RackService = rackService,
                ExportService = exportService,
                PrintService = printService,
                DashboardService = dashboardService,
                InspectionConfigService = inspectionConfigService,
                InspectionLogRepo = inspectionLogRepo,
                StockTpLookupService = stockTpLookupService
            };
        }
    }
}