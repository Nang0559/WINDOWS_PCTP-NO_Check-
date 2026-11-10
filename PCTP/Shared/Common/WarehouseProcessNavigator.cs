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
using PCTP.Shared.Helpers;
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
            FormWindowManager.ShowSingle(owner, () => new MainStockSV());
        }

        /// <summary>
        /// Mở màn hình Tiến trình nhập kho. <paramref name="mainStock"/> là TUỲ CHỌN: chỉ truyền khi bản đồ kho
        /// đang mở để nó được làm mới sau khi nhập (OnSlotUpdated). Mở từ Main_APP thì để null — không cần
        /// dựng MainStockSV; form quét tự dựng service riêng qua NhapTpModuleFactory.
        /// </summary>
        public static void OpenNhapKhoTienTrinh(IWin32Window owner, MainStockSV mainStock = null)
        {
            FormWindowManager.ShowSingle(owner, () =>
            {
                FormNhapKhoTienTrinh form = null;
                Action build = () =>
                {
                    var module = MainStockModuleFactory.Build();
                    form = new FormNhapKhoTienTrinh(mainStock, module.DashboardService);
                };

                // Chỉ dựng service khi chưa có cửa sổ nhập kho đang mở.
                var ownerForm = owner as Form;
                if (ownerForm != null && ownerForm.Visible && !ownerForm.IsDisposed)
                    new WaitFormService(ownerForm).Run(build, "Đang mở màn hình tiến trình nhập kho...");
                else
                    build();

                return form;
            });
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
            FormWindowManager.ShowSingle(owner,
                () => CreateFormQuanLyTienTrinhHangLoi(preselectPhieuXuLyId));
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

            // Gác tồn kho + số lượng đang bị phiếu chưa hoàn tất giữ (dùng chung cho nội bộ và khách trả).
            var lotClaimGuard = new LotClaimGuard(phieuTraHangRepo, slotService, qtChungRepo);
            khachTraHangService.LotGuard = lotClaimGuard;
            traNoiBoService.LotGuard = lotClaimGuard;

            var noiBoLookupRepo = new NoiBoLookupRepository(sql, uow);

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
                module.InitialQCService,
                noiBoLookupRepo,
                module.Phase5To9Service);
        }

        public static void OpenGiaoHangHVN(string customerNo)
        {
            if (string.IsNullOrWhiteSpace(customerNo))
                throw new ArgumentException("CustomerNo không được để trống.", nameof(customerNo));

            CustomerTableConfig.GetForDelivery(customerNo);
            string normalizedCustomerNo = CustomerTableConfig.NormalizeCustomerNo(customerNo);
            string instanceKey = typeof(HVN_PGH).FullName + ":" + normalizedCustomerNo;

            // Mỗi khách hàng có một cửa sổ Phiếu giao hàng riêng; cùng khách không mở trùng.
            FormWindowManager.ShowSingle<HVN_PGH>(
                null,
                () => new HVN_PGH(normalizedCustomerNo),
                instanceKey);
        }
    }
}