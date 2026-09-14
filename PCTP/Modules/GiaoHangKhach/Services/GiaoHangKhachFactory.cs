using PCTP.Applications.Services;
using PCTP.ClassSQL;
using PCTP.Domain.Interfaces;
using PCTP.Infrastructure;
using PCTP.Infrastructure.Repositories;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.OrderLoading;
using PCTP.Modules.GiaoHangKhach.OrderLoading.Category;
using PCTP.Modules.GiaoHangKhach.OrderLoading.GiaoDB;
using PCTP.Modules.GiaoHangKhach.OrderLoading.IFS;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Interfaces;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using System;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Composition root cho module Giao Hàng Khách — tách phần "dựng dependency"
    /// ra khỏi HVN_PGH để tái dùng được (unit test / form khác) và để mọi
    /// consumer chỉ phụ thuộc interface, không phụ thuộc concrete class.
    ///
    /// Mirror 1:1 logic của HVN_PGH.BuildPresenter() cũ — KHÔNG bớt dependency,
    /// chỉ đổi kiểu field/param sang interface.
    /// </summary>
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

            /// <summary>Máy hiện tại có đang được cấp quyền bắn QR không.</summary>
            public bool IsMayBanQR { get; set; }

            /// <summary>Tên bảng TMP/VIEW dùng cho phiên làm việc hiện tại.</summary>
            public string TenBan { get; set; }
        }

        public static Module Build(string customerNo)
        {
            var cfg = CustomerTableConfig.Get(customerNo);

            // ── Hạ tầng SQL — 1 SQLPROVIDER duy nhất cho cả session ─────────────
            // ❌ Bản gốc: new PhieuSqlExecutor(new SQLPROVIDER()) rồi dùng dbExecutor.Sql
            //    cho UnitOfWork — vẫn ra 1 instance vì Sql là property trỏ lại _sql,
            //    nhưng để rõ ràng và khớp 100% với HVN_PGH, dựng SQLPROVIDER trước.
            var sql = new SQLPROVIDER();
            var bus = new InProcessEventBus();
            var phieuDb = new PhieuSqlExecutor(sql);
            var phieuUow = new UnitOfWork(sql);

            // ── Repos con mà PhieuRepository cần (constructor "tiện dụng" của nó
            //    bắt buộc 2 tham số này, không có default) ────────────────────────
            var bulkStockSlotRepo = new BulkStockSlotRepository(phieuDb, phieuUow);
            var historyRepo = new StockHistoryRepository(phieuDb, phieuUow);
            var hangChoGiaoRepo = new HangChoGiaoRepository(phieuDb, phieuUow);
            var phieugiaDBRepo = new PhieuGiaoDBRepository(phieuDb, phieuUow);

            var phieuRepo = new PhieuRepository(
                phieuDb, phieuUow, cfg, bulkStockSlotRepo, historyRepo, hangChoGiaoRepo);

            var phieuTmpRepo = new PhieuTmpRepository(phieuDb, phieuUow);
            var tableOrderRepo = new TableOrderRepo(phieuDb, phieuTmpRepo);

            // ❌ Bản gốc: new GioXuatRepository(dbExecutor) — thiếu uow (bắt buộc).
            var gioRepo = new GioXuatRepository(phieuDb, phieuUow);

            // ❌ Bản gốc: new DocQRRepository(dbExecutor, cfg) — DocQRRepository nhận
            //    SQLPROVIDER, không nhận PhieuSqlExecutor.
            var qrRepo = new DocQRRepository(sql, cfg);

            // ❌ Bản gốc: new SqlRepository(dbExecutor) — thiếu uow (bắt buộc).
            var sqlRepo = new SqlRepository(phieuDb, phieuUow);

            var luuTruRepo = new PhieuLuuTruRepository(phieuDb, phieuUow);

            // ❌ Bản gốc: new MachinePermissionService(dbExecutor) — service này
            //    nhận SQLPROVIDER, và KHÔNG có method IsMayBanQR()/GetTenBan(cfg).
            //    Logic đúng (giống HVN_PGH) là đọc trực tiếp tbl_QR_MAY_DOCQR.
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

            // ❌ Bản gốc: new PhieuService(phieuRepo, ifsRepo, bus, gioRepo, tenBan, cfg, isMayBanQR)
            //    — thiếu 4 tham số bắt buộc: tableOrderRepo, giaoDbRepo, orderSourceFactory, rowCategoryFilter.
            var phieuSvc = new PhieuService(
                phieuRepo, ifsRepo, bus, gioRepo, tenBan, cfg, isMayBanQR,
                tableOrderRepo, phieugiaDBRepo, orderSourceFactory, rowCategoryFilter);

            var lotSvc = new PhieuLotService(phieuRepo, phieuRepo, bus);
            var hangThieuCaNgaySvc = new HangThieuCaNgayService(ifsRepo, luuTruRepo, phieuDb);

            // ❌ Bản gốc: new DocQRService(qrRepo, bus, cfg) — thiếu categoryResolver (bắt buộc).
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
