
using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.KhoVatLy.Application.Interfaces;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.KhoVatLy.Repository;
using PCTP.Modules.NhapKho.Repository;
using PCTP.Modules.NhapKho.Services;
using PCTP.Shared.Common;
using PCTP.VIEWSTOCK.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                public IStockService StockService { get; set; }
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
                var slotService = new SlotService(slotRepo);
                var warehouseService = new WarehouseService(warehouseRepo);
            var stockService = new StockService(
           slotService, warehouseService, historyRepo, uow);

            var nhapTpService = new NhapTpReceivingService(
                    uow,
                    stockTpRepo,
                    phieuTrackRepo,
                    caseRepo,
                    productionRepo,
                    slotService,
                    historyRepo,
                    statusRepo);

                return new Module
                {
                    NhapTpService = nhapTpService,
                    SlotService = slotService,
                    StockService = stockService
                };
            }
        }
    
}
