using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Common;
using PCTP.Shared.Enums;
using PCTP.Shared.Helpers;
using PCTP.Shared.UiMd;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    public sealed class QTChungService : IQTChungService
    {
        private const string ProcessCodeQTChung = "QT_CHUNG";
        private const string ProcessCodePhieuTraHang = "PHIEU_TRA_HANG";
        private readonly IPhieuXuLyBatThuongRepository _repo;
        private readonly IPhieuTraHangRepository _phieuTraHangRepo;
        private readonly IUnitOfWork _uow;
        private readonly IReworkStockService _reworkStockService;
        private readonly IGiaoBuNGService _giaoBuNGService;
        private readonly ITraHangQTChungRepository _traHangQTChungRepository;
        private readonly IWorkflowTransitionService _workflow;

        public QTChungService(
            IPhieuXuLyBatThuongRepository repo, IPhieuTraHangRepository phieuTraHangRepo, IReworkStockService reworkStockService,
            IGiaoBuNGService giaoBuNGService,
            IUnitOfWork uow, ITraHangQTChungRepository traHangQTChungRepository, IWorkflowTransitionService workflow)
        {
            _repo = repo
                ?? throw new ArgumentNullException(nameof(repo));
            _reworkStockService = reworkStockService ?? throw new ArgumentNullException(nameof(reworkStockService));
            _giaoBuNGService = giaoBuNGService ?? throw new ArgumentNullException(nameof(giaoBuNGService));
            _traHangQTChungRepository = traHangQTChungRepository ?? throw new ArgumentNullException(nameof(traHangQTChungRepository));
            _phieuTraHangRepo = phieuTraHangRepo
                ?? throw new ArgumentNullException(nameof(phieuTraHangRepo));
            _uow = uow
                ?? throw new ArgumentNullException(nameof(uow));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        }


        // ============================================================
        // PRIVATE
        // ============================================================

        private PhieuXuLyBatThuong GetRequired(
            int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentException(
                    "phieuXuLyId không hợp lệ.",
                    nameof(phieuXuLyId));

            var phieu = _repo.GetById(phieuXuLyId);

            if (phieu == null)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy PhieuXuLyBatThuong Id={phieuXuLyId}.");
            }

            return phieu;
        }


        private static void ValidateNguoiThucHien(
            string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(nguoiThucHien))
            {
                throw new ArgumentException(
                    "NguoiThucHien không được rỗng.",
                    nameof(nguoiThucHien));
            }
        }


        private void ValidateTransition(
            PhieuXuLyBatThuong phieu,
            QTChungStatus to)
        {
            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)to))
            {
                throw new InvalidOperationException(
                    $"Workflow không cho phép chuyển QT Chung {phieu.Status} → {to} cho PhieuXuLyBatThuong Id={phieu.Id}.");
            }
        }


        private void SafeRollback()
        {
            try
            {
                _uow.Rollback();
            }
            catch
            {
                // Không che lỗi nghiệp vụ/database ban đầu.
            }
        }


        // ============================================================
        // 1. TẠO PHIẾU XỬ LÝ BẤT THƯỜNG
        //
        // Moi
        //   ↓
        // DaTaoPhieuBatThuong
        // ============================================================

        public int TaoPhieuXuLyBatThuong(
            int phieuTraHangCTId,
            string model,
            string phanLoaiXuLy,
            string boPhanPhatHanh,
            string nguoiThucHien)
        {
            if (phieuTraHangCTId <= 0)
            {
                throw new ArgumentException(
                    "phieuTraHangCTId không hợp lệ.",
                    nameof(phieuTraHangCTId));
            }

            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException(
                    "Model không được rỗng.",
                    nameof(model));

            if (string.IsNullOrWhiteSpace(phanLoaiXuLy))
                throw new ArgumentException(
                    "PhanLoaiXuLy không được rỗng.",
                    nameof(phanLoaiXuLy));

            if (string.IsNullOrWhiteSpace(boPhanPhatHanh))
                throw new ArgumentException(
                    "BoPhanPhatHanh không được rỗng.",
                    nameof(boPhanPhatHanh));

            ValidateNguoiThucHien(nguoiThucHien);

            var p = new PhieuXuLyBatThuong
            {
                Model = model.Trim(),
                PhanLoaiXuLy = phanLoaiXuLy.Trim(),
                BoPhanPhatHanh = boPhanPhatHanh.Trim(),

                Status = QTChungStatus.Moi,

                CreatedBy = nguoiThucHien.Trim()
            };

            try
            {
                _uow.Begin();

                var id = _repo.Insert(
                    phieuTraHangCTId,
                    p);

                // Repository Insert tạo trạng thái Moi.
                //
                // Sau khi insert thành công:
                //
                // Moi
                //   ↓
                // DaTaoPhieuBatThuong

                _repo.UpdateStatus(
                    id,
                    QTChungStatus.DaTaoPhieuBatThuong,
                    nguoiThucHien);

                _uow.Commit();

                return id;
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }


        // ============================================================
        // 2. QC ĐỊNH HƯỚNG
        //
        // DaTaoPhieuBatThuong
        //          ↓
        //     DaDinhHuong
        //
        // Sau đó branch mới bắt đầu.
        // ============================================================

        public ScanResult QCDinhHuong(
            int phieuXuLyId,
            HuongXuLyBatThuong huong,
            string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);

            var phieu = GetRequired(phieuXuLyId);

            if (phieu.Status !=
                QTChungStatus.DaTaoPhieuBatThuong)
            {
                return ScanResult.Fail(
                    $"QT Chung hiện tại là {phieu.Status}. " +
                    "Chỉ được định hướng khi đang " +
                    "DaTaoPhieuBatThuong.");
            }

            switch (huong)
            {
                case HuongXuLyBatThuong.TuChoiGiaoBu:
                case HuongXuLyBatThuong.ChiGiaoBu:
                case HuongXuLyBatThuong.CanRework:
                    break;

                default:
                    return ScanResult.Fail(
                        $"Hướng xử lý {huong} không hợp lệ.");
            }

            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.DaDinhHuong))
            {
                return ScanResult.Fail(
                    $"Không thể chuyển {phieu.Status} → " +
                    $"{QTChungStatus.DaDinhHuong}.");
            }

            try
            {
                _uow.Begin();

                // Ghi hướng xử lý trước.
                _repo.UpdateDinhHuong(
                    phieuXuLyId,
                    huong,
                    nguoiThucHien);

                // Sau đó mới chuyển state.
                _repo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.DaDinhHuong,
                    nguoiThucHien);

                _uow.Commit();

                return ScanResult.OK(
                    $"Đã định hướng {huong}.");
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }


        // ============================================================
        // 3. TRA CỨU LOT REWORK
        //
        // Phần này KHÔNG thể implementation bằng
        // IPhieuXuLyBatThuongRepository hiện tại.
        //
        // Cần repository nghiệp vụ Lot/Stock riêng.
        // ============================================================

        public List<LotInfo> GetLotsCanRework(
            int phieuXuLyId)
        {
            var phieu = GetRequired(phieuXuLyId);

            if (phieu.HuongXuLy !=
                HuongXuLyBatThuong.CanRework)
            {
                throw new InvalidOperationException(
                    "Chỉ phiếu có hướng CanRework " +
                    "mới được tra cứu Lot rework.");
            }

            throw new NotImplementedException(
                "Cần repository Lot/Stock để lấy danh sách Lot rework.");
        }


        // ============================================================
        // 4. XUẤT KHO REWORK
        //
        // DaDinhHuong
        //      ↓
        // DaXuatKhoRework
        // ============================================================

        public ScanResult XuatKhoRework(int phieuXuLyId, int slotId, string lotNo, int soLuong, string nguoiXuat)
        {
            ValidateNguoiThucHien(nguoiXuat);
            if (slotId <= 0) return ScanResult.Fail("SlotId không hợp lệ.");
            if (string.IsNullOrWhiteSpace(lotNo)) return ScanResult.Fail("LotNo không được rỗng.");
            if (soLuong <= 0) return ScanResult.Fail("SoLuong phải lớn hơn 0.");

            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                return ScanResult.Fail("Chỉ phiếu có hướng CanRework mới được xuất kho rework.");

            ValidateTransition(phieu, QTChungStatus.DaXuatKhoRework);

            try
            {
                _uow.Begin(); // depth=1 (hoặc tăng nếu ReworkStockService cũng Begin bên trong — vẫn OK nhờ reentrant)

                // ReworkStockService.XuatKhoRework tự Begin/Commit nội bộ — với UOW reentrant,
                // Commit của nó chỉ giảm depth, KHÔNG commit thật cho tới khi ra khỏi method này.
                var result = _reworkStockService.XuatKhoRework(phieuXuLyId, slotId, lotNo, soLuong, nguoiXuat);
                if (!result.IsOK)
                {
                    SafeRollback();
                    return result;
                }

                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaXuatKhoRework, nguoiXuat);
                _uow.Commit();
                return result;
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi xuất kho rework: " + ex.Message);
            }
        }


        // ============================================================
        // 5. GIAO HÀNG REWORK
        //
        // DaXuatKhoRework
        //      ↓
        // DaGiaoSanXuat
        // ============================================================

        public ScanResult GiaoHangRework(
            int phieuXuLyId,
            List<LotInfo> lots,
            string ngayGiao,
            string nguoiNhan,
            string boPhanNhan)
        {
            ValidateNguoiThucHien(nguoiNhan);

            if (lots == null || lots.Count == 0)
                return ScanResult.Fail(
                    "Danh sách Lot giao rework không được rỗng.");

            if (string.IsNullOrWhiteSpace(ngayGiao))
                return ScanResult.Fail(
                    "NgayGiao không được rỗng.");

            if (string.IsNullOrWhiteSpace(boPhanNhan))
                return ScanResult.Fail(
                    "BoPhanNhan không được rỗng.");

            var phieu = GetRequired(phieuXuLyId);

            if (phieu.HuongXuLy !=
                HuongXuLyBatThuong.CanRework)
            {
                return ScanResult.Fail(
                    "Chỉ phiếu CanRework mới được giao sản xuất.");
            }

            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.DaGiaoSanXuat))
            {
                return ScanResult.Fail(
                    $"Không thể chuyển {phieu.Status} → " +
                    $"{QTChungStatus.DaGiaoSanXuat}.");
            }

            throw new NotImplementedException(
                "Cần repository giao hàng/rework hiện tại.");
        }


        // ============================================================
        // 6. GHI NHẬN ĐANG REWORK
        //
        // KHÔNG đổi QTChungStatus.
        // ============================================================

        public void GhiNhanDangRework(
            int phieuXuLyId,
            string ghiChu,
            string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);

            var phieu = GetRequired(phieuXuLyId);

            if (phieu.HuongXuLy !=
                HuongXuLyBatThuong.CanRework)
            {
                throw new InvalidOperationException(
                    "Chỉ phiếu CanRework mới được ghi nhận rework.");
            }

            if (phieu.Status !=
                QTChungStatus.DaGiaoSanXuat)
            {
                throw new InvalidOperationException(
                    $"Không thể ghi nhận đang rework " +
                    $"khi QT Chung đang {phieu.Status}.");
            }

            /*
             * Đây là nghiệp vụ thông tin.
             *
             * Không được:
             *
             * Status = DangRework
             *
             * Vì enum không có state này.
             *
             * Cần repository lưu ghi chú/log rework
             * nếu hệ thống có.
             */

            throw new NotImplementedException(
                "Cần repository ghi nhận thông tin rework.");
        }


        // ============================================================
        // 7. QC XÁC NHẬN CUỐI
        //
        // DaGiaoSanXuat
        //      ↓
        // DaQCXacNhanCuoi
        //
        // NG = 0:
        //      DaQCXacNhanCuoi → HoanTat
        //
        // NG > 0:
        //      DaQCXacNhanCuoi
        //          ↓
        //      DaNhapLaiKho
        // ============================================================

        public ScanResult QCXacNhanCuoi(
        int phieuXuLyId,
        int soLuongOK,
        int soLuongNG,
        string nguoiQC,
        int? slotIdOK = null,   // ✅ bắt buộc nếu soLuongOK > 0
        int? slotIdNG = null,   // ✅ bắt buộc nếu soLuongNG > 0
        string lotNo = null)   // ✅ LOT nhập lại
        {
            ValidateNguoiThucHien(nguoiQC);

            if (soLuongOK < 0) return ScanResult.Fail("SoLuongOK không hợp lệ.");
            if (soLuongNG < 0) return ScanResult.Fail("SoLuongNG không hợp lệ.");
            if (soLuongOK == 0 && soLuongNG == 0)
                return ScanResult.Fail("Kết quả QC phải có OK hoặc NG.");

            // ✅ Validate slot khi có OK/NG
            if (soLuongOK > 0 && (!slotIdOK.HasValue || slotIdOK <= 0))
                return ScanResult.Fail("Phải chỉ định SlotIdOK khi có hàng OK.");
            if (soLuongNG > 0 && (!slotIdNG.HasValue || slotIdNG <= 0))
                return ScanResult.Fail("Phải chỉ định SlotIdNG khi có hàng NG.");
            if ((soLuongOK > 0 || soLuongNG > 0) && string.IsNullOrWhiteSpace(lotNo))
                return ScanResult.Fail("LotNo không được rỗng.");

            var phieu = GetRequired(phieuXuLyId);

            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                return ScanResult.Fail(
                    "QC xác nhận cuối chỉ áp dụng cho nhánh CanRework.");

            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.DaQCXacNhanCuoi))
                return ScanResult.Fail(
                    $"Không thể chuyển {phieu.Status} → DaQCXacNhanCuoi.");

            try
            {
                _uow.Begin();

                // ── 1. Ghi kết quả QC ────────────────────────────────────────
                int qcId = _traHangQTChungRepository.InsertQC(new TraHangQTChungQC
                {
                    PhieuXuLyBatThuongId = phieuXuLyId,
                    SoLuongDaRework = soLuongOK + soLuongNG,
                    SoLuongOK = soLuongOK,
                    SoLuongNG = soLuongNG,
                    DaKiemTraTem = false,
                    NguoiQC = nguoiQC
                });

                _repo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.DaQCXacNhanCuoi,
                    nguoiQC);

                // ── 2. Nhập lại hàng OK (bất kể có NG hay không) ─────────────
                if (soLuongOK > 0)
                {
                    var okResult = _reworkStockService.NhapLaiHangOK(
                        phieuXuLyId, lotNo, soLuongOK,
                        slotIdOK.Value, nguoiQC);

                    if (!okResult.IsOK)
                    {
                        SafeRollback();
                        return ScanResult.Fail(
                            "Lỗi nhập lại hàng OK: " + okResult.Message);
                    }
                }

                // ── 3. Rẽ nhánh theo NG ──────────────────────────────────────
                if (soLuongNG == 0)
                {
                    // ✅ Không có NG → HoanTat luôn
                    _repo.UpdateStatus(
                        phieuXuLyId, QTChungStatus.HoanTat, nguoiQC);

                    TryHoanTatHeader(phieu.PhieuTraHangId, nguoiQC);

                    _uow.Commit();
                    return ScanResult.OK(
                        $"QC xác nhận (QcId={qcId}): " +
                        $"OK={soLuongOK}, NG=0. QT chung hoàn tất.");
                }
                else
                {
                    // ✅ Có NG → nhập lại NG, rồi mới HoanTat
                    var ngResult = _reworkStockService.NhapLaiHangNG(
                        phieuXuLyId, lotNo, soLuongNG,
                        slotIdOK, slotIdNG, nguoiQC);

                    if (!ngResult.IsOK)
                    {
                        SafeRollback();
                        return ScanResult.Fail(
                            "Lỗi nhập lại hàng NG: " + ngResult.Message);
                    }

                    _repo.UpdateStatus(
                        phieuXuLyId, QTChungStatus.DaNhapLaiKho, nguoiQC);
                    _repo.UpdateStatus(
                        phieuXuLyId, QTChungStatus.HoanTat, nguoiQC);

                    TryHoanTatHeader(phieu.PhieuTraHangId, nguoiQC);

                    _uow.Commit();
                    return ScanResult.OK(
                        $"QC xác nhận (QcId={qcId}): " +
                        $"OK={soLuongOK}, NG={soLuongNG}. " +
                        $"Đã nhập lại NG. QT chung hoàn tất.");
                }
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail(
                    "Lỗi QC xác nhận cuối: " + ex.Message);
            }
        }


        // ============================================================
        // 8. GHI NHẬN KIỂM TRA TEM
        // ============================================================

        public void GhiNhanKiemTraTem(
            int qcId,
            bool daKiemTra)
        {
            if (qcId <= 0)
                throw new ArgumentException(
                    "qcId không hợp lệ.",
                    nameof(qcId));

            /*
             * IPhieuXuLyBatThuongRepository hiện tại
             * không có method QC.
             */
            throw new NotImplementedException(
                "Cần repository QC/FormInspection hiện tại.");
        }


        // QTChungService.cs — thêm implementation
        public ScanResult XacNhanChoGiaoBu(int phieuXuLyId, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);

            if (phieu.HuongXuLy != HuongXuLyBatThuong.ChiGiaoBu)
                return ScanResult.Fail("Chỉ phiếu hướng ChiGiaoBu mới được xác nhận giao bù.");

            ValidateTransition(phieu, QTChungStatus.DaGiaoBu);

            try
            {
                _uow.Begin();

                var result = _giaoBuNGService.XacNhanHoanTatGiaoBu(
                    phieu.PhieuTraHangId ?? 0, nguoiThucHien);
                if (!result.IsOK)
                {
                    SafeRollback();
                    return result;
                }

                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaGiaoBu, nguoiThucHien);
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.HoanTat, nguoiThucHien);

                if (phieu.PhieuTraHangId.HasValue && !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header?.Status == PhieuTraHangStatus.DangXuLyQTChung)
                        _phieuTraHangRepo.UpdateStatus(header.Id, PhieuTraHangStatus.HoanTat, nguoiThucHien);
                }

                _uow.Commit();
                return result;
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi xác nhận giao bù: " + ex.Message);
            }
        }
        public ScanResult DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.ChiGiaoBu)
                return ScanResult.Fail("Chỉ phiếu hướng ChiGiaoBu mới được đánh dấu chờ giao bù.");
            ValidateTransition(phieu, QTChungStatus.ChoGiaoBu);

            try
            {
                _uow.Begin();
                UpdateStatusWithConcurrencyCheck(phieuXuLyId, phieu.Status, QTChungStatus.ChoGiaoBu, nguoiThucHien);
                _uow.Commit();
                return ScanResult.OK("Đã chuyển sang chờ giao bù.");
            }
            catch (Exception ex) { SafeRollback(); return ScanResult.Fail(ex.Message); }
        }
        // ============================================================
        // 9. NHẬP LẠI HÀNG NG
        //
        // DaQCXacNhanCuoi
        //      ↓
        // DaNhapLaiKho
        // ============================================================

        public ScanResult NhapLaiHangNG(int phieuXuLyId, string lotNo, int soLuongNG,
     int? slotIdOK, int? slotIdNG, string nguoiNhap)
        {
            ValidateNguoiThucHien(nguoiNhap);
            if (string.IsNullOrWhiteSpace(lotNo)) return ScanResult.Fail("LotNo không được rỗng.");
            if (soLuongNG <= 0) return ScanResult.Fail("SoLuongNG phải lớn hơn 0.");

            var phieu = GetRequired(phieuXuLyId);
            if (phieu.Status != QTChungStatus.DaQCXacNhanCuoi)
                return ScanResult.Fail($"Chỉ được nhập lại hàng NG khi QT Chung đang DaQCXacNhanCuoi. Hiện tại: {phieu.Status}.");

            ValidateTransition(phieu, QTChungStatus.DaNhapLaiKho);

            try
            {
                _uow.Begin();

                var result = _reworkStockService.NhapLaiHangNG(
                    phieuXuLyId, lotNo, soLuongNG, slotIdOK, slotIdNG, nguoiNhap);
                if (!result.IsOK)
                {
                    SafeRollback();
                    return result;
                }

                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaNhapLaiKho, nguoiNhap);

                // Sau DaNhapLaiKho: theo QTChungStatusTransition, bước kế tiếp luôn là HoanTat
                // (ReworkMap[DaNhapLaiKho] chỉ có 1 lối ra). Tự động hoàn tất luôn tại đây.
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.HoanTat, nguoiNhap);

                if (phieu.PhieuTraHangId.HasValue && !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header?.Status == PhieuTraHangStatus.DangXuLyQTChung)
                        _phieuTraHangRepo.UpdateStatus(header.Id, PhieuTraHangStatus.HoanTat, nguoiNhap);
                }

                _uow.Commit();
                return result;
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi nhập lại hàng NG: " + ex.Message);
            }
        }


        // ============================================================
        // 10. HOÀN TẤT
        //
        // TuChoiGiaoBu → HoanTat
        // DaGiaoBu      → HoanTat
        // DaQCXacNhanCuoi → HoanTat
        // DaNhapLaiKho  → HoanTat
        // ============================================================

        public ScanResult HoanTat(
            int phieuXuLyId,
            string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);

            var phieu = GetRequired(phieuXuLyId);

            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.HoanTat))
            {
                return ScanResult.Fail(
                    $"Không thể chuyển QT Chung " +
                    $"{phieu.Status} → {QTChungStatus.HoanTat}.");
            }

            try
            {
                _uow.Begin();

                _repo.UpdateStatus(
                    phieuXuLyId,
                    QTChungStatus.HoanTat,
                    nguoiThucHien);
                if (phieu.PhieuTraHangId.HasValue &&
                  !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header != null && header.Status == PhieuTraHangStatus.DangXuLyQTChung)
                    {
                        _phieuTraHangRepo.UpdateStatus(header.Id, PhieuTraHangStatus.HoanTat, nguoiThucHien);
                    }
                }
                _uow.Commit();

                return ScanResult.OK(
                    "QT Chung đã hoàn tất.");
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }


        // ============================================================
        // 11. GIAO LẠI BỘ PHẬN PHÁT HIỆN
        //
        // KHÔNG thuộc QTChungStatus.
        //
        // Không được UpdateStatus() ở đây.
        // ============================================================

        public ScanResult GiaoLaiBoPhanPhatHien(
            int phieuXuLyId,
            string boPhanNhan,
            int soLuongGiaoLai,
            string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);

            if (string.IsNullOrWhiteSpace(boPhanNhan))
                return ScanResult.Fail(
                    "BoPhanNhan không được rỗng.");

            if (soLuongGiaoLai <= 0)
                return ScanResult.Fail(
                    "SoLuongGiaoLai phải lớn hơn 0.");

            var phieu = GetRequired(phieuXuLyId);

            /*
             * Đây là nghiệp vụ cấp TraNoiBo / PhieuTraHang.
             *
             * QTChungService không có IPhieuTraHangRepository.
             *
             * Do đó không được ở đây làm:
             *
             * PhieuTraHangStatus.ChoGiaoLaiBoPhan
             * PhieuTraHangStatus.DaGiaoLaiBoPhan
             *
             * Việc đó thuộc TraNoiBoService/Base.
             */

            throw new InvalidOperationException(
                $"GiaoLaiBoPhanPhatHien không thuộc state machine " +
                $"QTChung của PhieuXuLyBatThuong Id={phieu.Id}. " +
                "Thao tác này phải được thực hiện ở TraNoiBoService.");
        }


        // ============================================================
        // 12. HUỶ
        //
        // Chỉ những state mà Transition cho phép Huy
        // mới được hủy.
        // ============================================================

        public ScanResult HuyQTChung(
  int phieuXuLyId,
  string lyDoHuy,
  string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);

            if (string.IsNullOrWhiteSpace(lyDoHuy))
                return ScanResult.Fail("LyDoHuy không được rỗng.");

            var phieu = GetRequired(phieuXuLyId);
            ValidateTransition(phieu, QTChungStatus.Huy);

            try
            {
                _uow.Begin();

                // UpdateLyDoHuy đã tự làm cả 2 việc trong 1 UPDATE duy nhất:
                // chuyển Status -> Huy (kèm optimistic concurrency check qua
                // ExpectedFrom) VÀ ghi LyDoHuy/NgayHuy/NguoiHuy cùng lúc.
                // KHÔNG gọi UpdateStatusIfCurrentIs riêng nữa — gọi trước nó sẽ
                // khiến ExpectedFrom ở đây không còn khớp Status thật trong DB
                // (vì Status đã bị đổi thành Huy ở lần gọi trước đó), làm bước
                // này luôn báo nhầm "đã bị thay đổi bởi người khác".
                bool okDetail = _repo.UpdateLyDoHuy(
                    phieuXuLyId,
                    phieu.Status, // Status cũ trước khi hủy (ExpectedFrom)
                    lyDoHuy,
                    nguoiThucHien);

                if (!okDetail)
                {
                    _uow.Rollback();
                    return ScanResult.Fail($"Trạng thái của phiếu xử lý {phieuXuLyId} đã bị thay đổi bởi người khác — Vui lòng tải lại.");
                }

                // Kiểm tra và hoàn tất Header nếu không còn phiếu nào chờ xử lý
                if (phieu.PhieuTraHangId.HasValue &&
                    !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header != null && header.Status == PhieuTraHangStatus.DangXuLyQTChung)
                    {
                        bool okHeader = _phieuTraHangRepo.UpdateStatusIfCurrentIs(
                            header.Id,
                            PhieuTraHangStatus.DangXuLyQTChung,
                            PhieuTraHangStatus.HoanTat,
                            nguoiThucHien);

                        if (!okHeader)
                        {
                            _uow.Rollback();
                            return ScanResult.Fail($"Trạng thái của phiếu Header {header.Id} đã bị thay đổi bởi người khác trong lúc cập nhật — Vui lòng tải lại.");
                        }
                    }
                }

                _uow.Commit();

                return ScanResult.OK($"Đã hủy QT Chung. Lý do: {lyDoHuy}");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi khi thực hiện hủy QT Chung: " + ex.Message);
            }
        }


        // ============================================================
        // 13. GET BY ID
        // ============================================================

        public PhieuXuLyBatThuong GetById(
            int phieuXuLyId)
        {
            return GetRequired(phieuXuLyId);
        }


        // ============================================================
        // 14. GET STATUS
        // ============================================================

        //public QTChungStatus GetTrangThai(
        //    int phieuXuLyId)
        //{
        //    GetRequired(phieuXuLyId);

        //    return _repo.GetStatus(phieuXuLyId);
        //}

        private void UpdateStatusWithConcurrencyCheck(
        int phieuXuLyId,
        QTChungStatus expectedFrom,
        QTChungStatus newStatus,
        string nguoiThucHien)
        {
            bool success = _repo.UpdateStatusIfCurrentIs(phieuXuLyId, expectedFrom, newStatus, nguoiThucHien);

            if (!success)
            {
                throw new InvalidOperationException(
                    $"Trạng thái phiếu {phieuXuLyId} đã bị thay đổi bởi người khác (Kỳ vọng: {expectedFrom}) — Vui lòng tải lại.");
            }
        }
        // ============================================================
        // 15. GET ALLOWED NEXT
        // ============================================================

        public IReadOnlyList<QTChungStatus> GetAllowedNext(
        int phieuXuLyId)
        {
            var phieu = GetRequired(phieuXuLyId);

            var result = new List<QTChungStatus>();

            foreach (QTChungStatus candidate in Enum.GetValues(typeof(QTChungStatus)))
            {
                if (candidate == phieu.Status)
                    continue;

                if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)candidate))
                    continue;

                if (!QTChungBranchMap.IsReachableForHuong(candidate, phieu.HuongXuLy))
                    continue;

                result.Add(candidate);
            }

            return result;
        }


        // ============================================================
        // 16. TIMELINE
        // ============================================================

        public List<QTChungTimelineItem> GetTimeline(
            int phieuXuLyId)
        {
            GetRequired(phieuXuLyId);

            /*
             * IPhieuXuLyBatThuongRepository hiện tại
             * CHƯA có GetTimeline().
             *
             * Không được tự gọi một method repository chưa tồn tại.
             */
            throw new NotImplementedException(
                "IPhieuXuLyBatThuongRepository hiện tại chưa cung cấp GetTimeline().");
        }

        // ✅ Thêm vào QTChungService — dùng chung cho QCXacNhanCuoi, NhapLaiHangNG, XacNhanChoGiaoBu, HuyQTChung
        private void TryHoanTatHeader(int? phieuTraHangId, string nguoiThucHien)
        {
            if (!phieuTraHangId.HasValue) return;
            if (_phieuTraHangRepo.ConChoXuLy(phieuTraHangId.Value)) return;

            var header = _phieuTraHangRepo.GetById(phieuTraHangId.Value);
            if (header?.Status == PhieuTraHangStatus.DangXuLyQTChung)
                _phieuTraHangRepo.UpdateStatus(
                    header.Id, PhieuTraHangStatus.HoanTat, nguoiThucHien);
        }
        /// <summary>
        /// Thay thế phần "biết trạng thái nào thuộc nhánh xử lý nào" của
        /// QTChungStatusTransition.cs cũ (ChungMap / TuChoiGiaoBuMap / ChiGiaoBuMap /
        /// ReworkMap) — đây là phần logic KHÔNG thể chuyển vào bảng
        /// sys_WorkflowTransitions, vì bảng chỉ biết "trạng thái A -> B có hợp lệ
        /// về hình thức", không biết B thuộc nhánh HuongXuLy nào.
        ///
        /// Suy ra từ comment "NHÁNH 1/2/3" trong file SQL sys_WorkflowTransitions —
        /// ĐỐI CHIẾU LẠI với QTChungStatusTransition.cs gốc trước khi xoá file đó,
        /// để đảm bảo danh sách dưới đây khớp 100% logic cũ.
        /// </summary>
        internal static class QTChungBranchMap
        {
            private static readonly Dictionary<QTChungStatus, HuongXuLyBatThuong> _owner =
                new Dictionary<QTChungStatus, HuongXuLyBatThuong>
                {
            { QTChungStatus.TuChoiGiaoBu,    HuongXuLyBatThuong.TuChoiGiaoBu },
            { QTChungStatus.ChoGiaoBu,       HuongXuLyBatThuong.ChiGiaoBu },
            { QTChungStatus.DaGiaoBu,        HuongXuLyBatThuong.ChiGiaoBu },
            { QTChungStatus.DaXuatKhoRework, HuongXuLyBatThuong.CanRework },
            { QTChungStatus.DaGiaoSanXuat,   HuongXuLyBatThuong.CanRework },
            { QTChungStatus.DaQCXacNhanCuoi, HuongXuLyBatThuong.CanRework },
            { QTChungStatus.DaNhapLaiKho,    HuongXuLyBatThuong.CanRework },
                    // Moi, DaTaoPhieuBatThuong, DaDinhHuong, HoanTat, Huy:
                    // dùng chung cho mọi nhánh -> không có mặt trong dictionary này.
                };

            public static bool IsReachableForHuong(QTChungStatus status, HuongXuLyBatThuong huong)
            {
                if (!_owner.TryGetValue(status, out HuongXuLyBatThuong requiredHuong))
                    return true; // trạng thái dùng chung, không phân biệt nhánh

                return huong == requiredHuong;
            }
        }
    }
}
