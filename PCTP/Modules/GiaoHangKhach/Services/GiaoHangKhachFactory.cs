using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure;
using PCTP.Infrastructure.Repositories;
using PCTP.Infrastructure.Stock;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.OrderLoading.GiaoDB;
using PCTP.Modules.GiaoHangKhach.OrderLoading.IFS;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.KhoCore.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using System;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public static class GiaoHangKhachModuleFactory
    {
        public sealed class Module
        {
            public CustomerConfig Cfg { get; set; }
            public IPhieuService PhieuService { get; set; }
            public IPhieuLotService PhieuLotService { get; set; }
            public IDocQRService DocQRService { get; set; }
            public IInPhieuService InPhieuService { get; set; }
            public IHangThieuCaNgayService HangThieuCaNgayService { get; set; }
            public IGioXuatRepository GioXuatRepo { get; set; }
            public IIFSRepository IfsRepo { get; set; }
            public IMachinePermissionService MachinePermissionService { get; set; }
            public IOrderCategoryResolver CategoryResolver { get; set; }
            public IEventBus Bus { get; set; }
            public bool IsMayBanQR { get; set; }
            public string TenBan { get; set; }
        }

        public static Module Build(string customerNo)
        {
            var cfg = CustomerTableConfig.Get(customerNo);
            var sql = new SQLPROVIDER();
            var bus = new InProcessEventBus();
            var phieuDb = new PhieuSqlExecutor(sql);
            var phieuUow = new UnitOfWork(sql);

            var bulkStockSlotRepo = new BulkStockSlotRepository(phieuDb, phieuUow);
            var historyRepo = new StockHistoryRepository(phieuDb, phieuUow);
            var hangChoGiaoRepo = new HangChoGiaoRepository(phieuDb, phieuUow);
            var phieugiaDBRepo = new PhieuGiaoDBRepository(phieuDb, phieuUow);

            // KhoCore mutation boundary: STOCKTP balance + Slot/SlotLot share the
            // same PhieuSqlExecutor/UnitOfWork transaction as the delivery workflow.
            var stockBalanceRepo = new LegacyStockBalanceRepositoryAdapter(phieuDb, phieuUow);
            var stockMovement = new StockMovementService(stockBalanceRepo, bulkStockSlotRepo);

            var phieuRepo = new PhieuRepository(
                phieuDb, phieuUow, cfg, bulkStockSlotRepo, historyRepo, hangChoGiaoRepo,
                ifsRepo: null, stockMovement: stockMovement);

            var phieuTmpRepo = new PhieuTmpRepository(phieuDb, phieuUow);
            var tableOrderRepo = new TableOrderRepo(phieuDb, phieuTmpRepo);
            var gioRepo = new GioXuatRepository(phieuDb, phieuUow);
            var qrRepo = new DocQRRepository(sql, cfg);
            var sqlRepo = new SqlRepository(phieuDb, phieuUow);
            var luuTruRepo = new PhieuLuuTruRepository(phieuDb, phieuUow);

            var machinePermissionService = new MachinePermissionService(sql);
            bool isMayBanQR = machinePermissionService.GetCurrentRole() == MachineRole.DuocBanQR;
            string tenBan = isMayBanQR
                ? cfg.Delivery.TmpTable
                : cfg.Delivery.GetTmpViewTable(Environment.MachineName);

            var ifsRepo = IFSRepository.Create();
            var rowCategoryFilter = new DockCodeRowCategoryFilter();
            var categoryResolver = new GioMoTaCategoryResolver();
            var ifsStrategy = new IfsOrderLoadStrategy(ifsRepo, luuTruRepo, phieuTmpRepo);
            var tableOrderStrategy = new OrderTableLoadStrategy(tableOrderRepo, phieuTmpRepo, ifsRepo, rowCategoryFilter);
            var giaoDbStrategy = new GiaoDbOrderLoadStrategy(phieugiaDBRepo);
            var ifsSource = new IfsOrderSource(ifsStrategy);
            var tableOrderSource = new TableOrderSource(tableOrderStrategy);
            var giaoDbSource = new GiaoDbOrderSource(giaoDbStrategy);
            var orderSourceFactory = new OrderSourceFactory(ifsSource, tableOrderSource, giaoDbSource);

            // Rebuild PhieuRepository after IFS is available only if future code
            // needs to pass it; the current constructor keeps ifsRepo optional.
            // (The repository itself does not consume IFS directly.)
            var phieuSvc = new PhieuService(
                phieuRepo, ifsRepo, bus, gioRepo, tenBan, cfg, isMayBanQR,
                tableOrderRepo, phieugiaDBRepo, orderSourceFactory, rowCategoryFilter);

            var lotSvc = new PhieuLotService(phieuRepo, phieuRepo, bus);
            var hangThieuCaNgaySvc = new HangThieuCaNgayService(ifsRepo, luuTruRepo, phieuDb, orderSourceFactory);
            var qrSvc = new DocQRService(qrRepo, bus, cfg, categoryResolver);
            var gioVP = gioRepo.GetDictGioVP();
            var gioHN = gioRepo.GetDictGioHN();
            var inPhieuSvc = new InPhieuService(ifsRepo, phieuRepo, sqlRepo, gioVP, gioHN, cfg);

            return new Module
            {
                Cfg = cfg,
                PhieuService = phieuSvc,
                PhieuLotService = lotSvc,
                DocQRService = qrSvc,
                InPhieuService = inPhieuSvc,
                HangThieuCaNgayService = hangThieuCaNgaySvc,
                GioXuatRepo = gioRepo,
                IfsRepo = ifsRepo,
                MachinePermissionService = machinePermissionService,
                CategoryResolver = categoryResolver,
                Bus = bus,
                IsMayBanQR = isMayBanQR,
                TenBan = tenBan,
            };
        }
    }
}
