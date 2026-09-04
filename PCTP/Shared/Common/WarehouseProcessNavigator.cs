using PCTP.ClassSQL;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Modules.GiaoHangKhach.Intefaces.PhieuGiao;
using PCTP.Modules.GiaoHangKhach.Repositories;
using PCTP.Modules.KhoVatLy.Application.Services;
using PCTP.Modules.KhoVatLy.Repositories;
using PCTP.Modules.XuatKho.Repositories;
using PCTP.Modules.XuatKho.Services;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Modules.XuLyHangLoi.Services;
using PCTP.QRCODE_HVN.PGH;
using PCTP.QRCODE_HVN.YMN;
using PCTP.Shared.Common;
using PCTP.Shared.UiMd;
using PCTP.VIEWSTOCK;
using PCTP.VIEWSTOCK.Repository;
using PCTP.VIEWSTOCK.ViewForm;
using PCTP.YMN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PCTP.Common
{
    /// <summary>
    /// Điểm điều phối DUY NHẤT để mở các form quy trình nghiệp vụ kho.
    /// Main_APP (accordion) và MainStockSV (process bar) đều gọi qua đây —
    /// tránh code trùng lặp "using (var f = new FormXxx()) f.ShowDialog()"
    /// rải rác nhiều nơi, và đảm bảo mọi nơi mở form đều đồng nhất hành vi.
    ///
    /// Navigator KHÔNG chứa logic nghiệp vụ — chỉ new đúng dependency (Tầng 3:
    /// SqlProvider/Repository) và mở đúng Form (Tầng 2: Module).
    /// </summary>
    public static class WarehouseProcessNavigator
    {
        // ── Repository dùng chung — new 1 lần theo lời gọi, không giữ static instance
        // để tránh cache dữ liệu cũ giữa các lần mở form (mỗi lần mở là 1 phiên làm việc mới).
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
            f.Show(); // Show (không ShowDialog) — bản đồ kho dùng như cửa sổ làm việc song song
        }

        public static void OpenNhapKhoTienTrinh(IWin32Window owner, MainStockSV mainStock = null)
        {
            using (var f = new FormNhapKhoTienTrinh(mainStock))
                f.ShowDialog(owner);
        }

        /// <summary>
        /// Mốc 3a — QC Định hướng ban đầu: phiếu vừa được ban hành (TrangThai=ChoQC),
        /// QC nhập phương pháp định hướng xử lý (Nắn/Vặn/Cắt gọt...) trước khi
        /// chuyển bộ phận sản xuất thực hiện.
        /// </summary>
        public static void OpenQCDinhHuong(IWin32Window owner, int? preselectId = null)
        {
            using (var f = new FormXuLyBatThuong(CreatePhieuTraHangRepo(), XuLyBatThuongMode.DinhHuong, preselectId))
                f.ShowDialog(owner);
        }

        /// <summary>
        /// Mốc 3b — QC Xác nhận lần cuối: sau khi SX đã sửa/xử lý xong, QC vào
        /// chốt kết luận OK/NG cuối cùng (TrangThai chuyển sang QCDaDuyet).
        /// Đây là điều kiện KHOÁ bắt buộc trước khi FormTraHangNGNew cho trả về SX.
        /// </summary>
        public static void OpenQCXacNhanCuoi(IWin32Window owner, int? preselectId = null)
        {
            using (var f = new FormXuLyBatThuong(CreatePhieuTraHangRepo(), XuLyBatThuongMode.XacNhanCuoi, preselectId))
                f.ShowDialog(owner);
        }
        private static IGiaoBuNGService CreateGiaoBuNGService()
        {
            var provider = new SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);

            var slotRepo = new SlotRepository(sql, uow);
            var slotService = new SlotService(slotRepo);
            var coreHistoryRepo = new StockHistoryRepository(sql, uow); // đã có ở bước trước
          
            var stockExportRepo = new StockExportRepository(sql, uow);
            var stockHistoryRepo = new StockHistoryRepository(sql, uow);
            var stockExportHistoryRepo = new StockExportHistoryRepository(sql, uow, coreHistoryRepo);
            var choGiaoRepo = new HangChoGiaoRepository(sql, uow);

            var validationService = new StockExportValidationService(stockExportRepo, stockExportHistoryRepo);

            var stockExportService = new StockExportService(
                uow, slotService, stockExportRepo, stockHistoryRepo, choGiaoRepo, validationService);

            return new GiaoBuNGService(stockExportService, choGiaoRepo, slotService);
        }
        // ★ MỚI — mở màn hình quản lý tiến trình hàng lỗi (thay cho OpenTraHangNG/OpenGiaoBuNG cũ).
        // Người dùng tự chọn phiếu trong lưới, form tự xử lý mở FormGiaoBuNG/FormXuLyBatThuong theo bước tương ứng.
        public static void OpenQuanLyTienTrinhHangLoi(IWin32Window owner)
        {
            using (var f = CreateFormQuanLyTienTrinhHangLoi())
                f.ShowDialog(owner);
        }

        private static FormQuanLyTienTrinhHangLoi CreateFormQuanLyTienTrinhHangLoi()
        {
            var provider = new SQLPROVIDER();
            var sql = new PhieuSqlExecutor(provider);
            var uow = new UnitOfWork(provider);

            // ── Repository tầng dưới ─────────────────────────────────────
            var phieuTraHangRepo = new PhieuTraHangRepository(sql, uow);
            var phieuXuLyRepo = new PhieuXuLyBatThuongRepository(sql, uow);
            var qtChungRepo = new TraHangQTChungRepository(sql, uow);
            var phieuGiaoRepo = new PhieuGiaoRepository(sql, uow);

            var slotRepo = new SlotRepository(sql, uow);
            var slotService = new SlotService(slotRepo);
            var stockExportRepo = new StockExportRepository(sql, uow);
            var stockHistoryRepo = new StockHistoryRepository(sql, uow);

            // ── Workflow — dùng CHUNG 1 IWorkflowRepository ─────────────
            var workflowRepo = new WorkflowRepository(sql, uow);
            var workflow = new WorkflowTransitionService(workflowRepo);
            var workflowEngine = new WorkflowEngine(workflowRepo);

            // ── GiaoBuNGService (đã ghép hoàn chỉnh từ trước) ────────────
            var giaoBuNGService = CreateGiaoBuNGService();

            // ── ReworkStockService ────────────────────────────────────────
            var reworkStockService = new ReworkStockService(
                uow, slotService, stockExportRepo, stockHistoryRepo, qtChungRepo, phieuXuLyRepo);

            // ── QTChungService ────────────────────────────────────────────
            var qtChungService = new QTChungService(
                phieuXuLyRepo, phieuTraHangRepo, reworkStockService,
                giaoBuNGService, uow, qtChungRepo, workflow);

            // ── KhachTraHangService ───────────────────────────────────────
            var khachTraHangService = new KhachTraHangService(
                qtChungService, phieuTraHangRepo, phieuGiaoRepo, phieuXuLyRepo, uow);

            // ── TraNoiBoService ───────────────────────────────────────────
            var traNoiBoService = new TraNoiBoService(
                phieuTraHangRepo, workflowEngine, uow);

            return new FormQuanLyTienTrinhHangLoi(
                khachTraHangService,
                traNoiBoService,
                qtChungService,
                reworkStockService,
                giaoBuNGService,
                phieuTraHangRepo,
                phieuXuLyRepo,
                qtChungRepo,
                phieuGiaoRepo);
        }
        public static void OpenGiaoHangHVN(string customerNo)
        {
            var frm = new HVN_PGH(customerNo);
            frm.Show();
        }

        public static void OpenGiaoHangYMVN(string mpSp)
        {
            YMVN_CHONGIAO.MP_SP = mpSp;
            var frm = new GIAOHANGYMN();
            frm.Show();
        }
    }
}
