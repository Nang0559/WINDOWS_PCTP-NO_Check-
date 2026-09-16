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
        private readonly IAffectedLotTraceService _affectedLotTraceService;

        public QTChungService(
            IPhieuXuLyBatThuongRepository repo,
            IPhieuTraHangRepository phieuTraHangRepo,
            IReworkStockService reworkStockService,
            IGiaoBuNGService giaoBuNGService,
            IUnitOfWork uow,
            ITraHangQTChungRepository traHangQTChungRepository,
            IWorkflowTransitionService workflow,
            IAffectedLotTraceService affectedLotTraceService = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _reworkStockService = reworkStockService ?? throw new ArgumentNullException(nameof(reworkStockService));
            _giaoBuNGService = giaoBuNGService ?? throw new ArgumentNullException(nameof(giaoBuNGService));
            _traHangQTChungRepository = traHangQTChungRepository ?? throw new ArgumentNullException(nameof(traHangQTChungRepository));
            _phieuTraHangRepo = phieuTraHangRepo ?? throw new ArgumentNullException(nameof(phieuTraHangRepo));
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _affectedLotTraceService = affectedLotTraceService;
        }

        private PhieuXuLyBatThuong GetRequired(int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentException("phieuXuLyId không hợp lệ.", nameof(phieuXuLyId));

            var phieu = _repo.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException($"Không tìm thấy PhieuXuLyBatThuong Id={phieuXuLyId}.");

            return phieu;
        }

        private static void ValidateNguoiThucHien(string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(nguoiThucHien))
                throw new ArgumentException("NguoiThucHien không được rỗng.", nameof(nguoiThucHien));
        }

        private void ValidateTransition(PhieuXuLyBatThuong phieu, QTChungStatus to)
        {
            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)to))
                throw new InvalidOperationException($"Workflow không cho phép chuyển QT Chung {phieu.Status} → {to} cho PhieuXuLyBatThuong Id={phieu.Id}.");
        }

        private void SafeRollback()
        {
            try { _uow.Rollback(); } catch { }
        }

        public int TaoPhieuXuLyBatThuong(int phieuTraHangCTId, string model, string phanLoaiXuLy, string boPhanPhatHanh, string nguoiThucHien)
        {
            if (phieuTraHangCTId <= 0) throw new ArgumentException("phieuTraHangCTId không hợp lệ.", nameof(phieuTraHangCTId));
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model không được rỗng.", nameof(model));
            if (string.IsNullOrWhiteSpace(phanLoaiXuLy)) throw new ArgumentException("PhanLoaiXuLy không được rỗng.", nameof(phanLoaiXuLy));
            if (string.IsNullOrWhiteSpace(boPhanPhatHanh)) throw new ArgumentException("BoPhanPhatHanh không được rỗng.", nameof(boPhanPhatHanh));
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
                var id = _repo.Insert(phieuTraHangCTId, p);
                _repo.UpdateStatus(id, QTChungStatus.DaTaoPhieuBatThuong, nguoiThucHien);
                _uow.Commit();
                return id;
            }
            catch { SafeRollback(); throw; }
        }

        /// <summary>
        /// Phase 2 boundary: truy vết LOT, lưu snapshot và trả về TotalAffectedQuantity.
        /// QC không được tự dùng SoLuongLoi làm source-of-truth sau khi Phase 2 đã chạy.
        /// </summary>
        public AffectedLotTraceResult TruyVetLOT(int phieuXuLyId, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            if (_affectedLotTraceService == null)
                throw new InvalidOperationException("QT Chung chưa được cấu hình IAffectedLotTraceService.");

            return _affectedLotTraceService.TruyVetLOT(phieuXuLyId, nguoiThucHien);
        }

        public ScanResult QCDinhHuong(int phieuXuLyId, HuongXuLyBatThuong huong, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);

            if (phieu.Status != QTChungStatus.DaTaoPhieuBatThuong)
                return ScanResult.Fail($"QT Chung hiện tại là {phieu.Status}. Chỉ được định hướng khi đang DaTaoPhieuBatThuong.");

            switch (huong)
            {
                case HuongXuLyBatThuong.TuChoiGiaoBu:
                case HuongXuLyBatThuong.ChiGiaoBu:
                case HuongXuLyBatThuong.CanRework:
                    break;
                default:
                    return ScanResult.Fail($"Hướng xử lý {huong} không hợp lệ.");
            }

            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.DaDinhHuong))
                return ScanResult.Fail($"Không thể chuyển {phieu.Status} → {QTChungStatus.DaDinhHuong}.");

            // Phase 2: trước khi QC định hướng, bắt buộc snapshot toàn bộ LOT bị ảnh hưởng.
            // Nếu thiếu Production/WIP hoặc Customer Return thì trace fail-closed.
            AffectedLotTraceResult trace;
            try
            {
                trace = TruyVetLOT(phieuXuLyId, nguoiThucHien);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail("Không thể hoàn tất truy vết LOT trước khi QC định hướng: " + ex.Message);
            }

            if (trace == null || !trace.IsComplete || trace.TotalAffectedQuantity <= 0)
                return ScanResult.Fail("Không có snapshot LOT hợp lệ để QC định hướng.");

            try
            {
                _uow.Begin();
                _repo.UpdateDinhHuong(phieuXuLyId, huong, nguoiThucHien);
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaDinhHuong, nguoiThucHien);
                _uow.Commit();

                return ScanResult.OK($"Đã định hướng {huong}. TotalAffectedQuantity={trace.TotalAffectedQuantity:n0}.");
            }
            catch
            {
                SafeRollback();
                throw;
            }
        }

        public List<LotInfo> GetLotsCanRework(int phieuXuLyId)
        {
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                throw new InvalidOperationException("Chỉ phiếu có hướng CanRework mới được tra cứu Lot rework.");
            throw new NotImplementedException("Cần repository Lot/Stock để lấy danh sách Lot rework.");
        }

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
                _uow.Begin();
                var result = _reworkStockService.XuatKhoRework(phieuXuLyId, slotId, lotNo, soLuong, nguoiXuat);
                if (!result.IsOK) { SafeRollback(); return result; }
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaXuatKhoRework, nguoiXuat);
                _uow.Commit();
                return result;
            }
            catch (Exception ex) { SafeRollback(); return ScanResult.Fail("Lỗi xuất kho rework: " + ex.Message); }
        }

        public ScanResult GiaoHangRework(int phieuXuLyId, List<LotInfo> lots, string ngayGiao, string nguoiNhan, string boPhanNhan)
        {
            ValidateNguoiThucHien(nguoiNhan);
            if (lots == null || lots.Count == 0) return ScanResult.Fail("Danh sách Lot giao rework không được rỗng.");
            if (string.IsNullOrWhiteSpace(ngayGiao)) return ScanResult.Fail("NgayGiao không được rỗng.");
            if (string.IsNullOrWhiteSpace(boPhanNhan)) return ScanResult.Fail("BoPhanNhan không được rỗng.");
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework) return ScanResult.Fail("Chỉ phiếu CanRework mới được giao sản xuất.");
            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.DaGiaoSanXuat))
                return ScanResult.Fail($"Không thể chuyển {phieu.Status} → {QTChungStatus.DaGiaoSanXuat}.");
            throw new NotImplementedException("Cần repository giao hàng/rework hiện tại.");
        }

        public void GhiNhanDangRework(int phieuXuLyId, string ghiChu, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                throw new InvalidOperationException("Chỉ phiếu CanRework mới được ghi nhận rework.");
            if (phieu.Status != QTChungStatus.DaGiaoSanXuat)
                throw new InvalidOperationException($"Không thể ghi nhận đang rework khi QT Chung đang {phieu.Status}.");
            throw new NotImplementedException("Cần repository ghi nhận thông tin rework.");
        }

        public ScanResult QCXacNhanCuoi(int phieuXuLyId, int soLuongOK, int soLuongNG, string nguoiQC, int? slotIdOK = null, int? slotIdNG = null, string lotNo = null)
        {
            ValidateNguoiThucHien(nguoiQC);
            if (soLuongOK < 0) return ScanResult.Fail("SoLuongOK không hợp lệ.");
            if (soLuongNG < 0) return ScanResult.Fail("SoLuongNG không hợp lệ.");
            if (soLuongOK == 0 && soLuongNG == 0) return ScanResult.Fail("Kết quả QC phải có OK hoặc NG.");
            if (soLuongOK > 0 && (!slotIdOK.HasValue || slotIdOK <= 0)) return ScanResult.Fail("Phải chỉ định SlotIdOK khi có hàng OK.");
            if (soLuongNG > 0 && (!slotIdNG.HasValue || slotIdNG <= 0)) return ScanResult.Fail("Phải chỉ định SlotIdNG khi có hàng NG.");
            if ((soLuongOK > 0 || soLuongNG > 0) && string.IsNullOrWhiteSpace(lotNo)) return ScanResult.Fail("LotNo không được rỗng.");

            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework) return ScanResult.Fail("QC xác nhận cuối chỉ áp dụng cho nhánh CanRework.");
            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.DaQCXacNhanCuoi)) return ScanResult.Fail($"Không thể chuyển {phieu.Status} → DaQCXacNhanCuoi.");

            try
            {
                _uow.Begin();
                int qcId = _traHangQTChungRepository.InsertQC(new TraHangQTChungQC
                {
                    PhieuXuLyBatThuongId = phieuXuLyId,
                    SoLuongDaRework = soLuongOK + soLuongNG,
                    SoLuongOK = soLuongOK,
                    SoLuongNG = soLuongNG,
                    DaKiemTraTem = false,
                    NguoiQC = nguoiQC
                });
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaQCXacNhanCuoi, nguoiQC);

                if (soLuongOK > 0)
                {
                    var okResult = _reworkStockService.NhapLaiHangOK(phieuXuLyId, lotNo, soLuongOK, slotIdOK.Value, nguoiQC);
                    if (!okResult.IsOK) { SafeRollback(); return ScanResult.Fail("Lỗi nhập lại hàng OK: " + okResult.Message); }
                }

                if (soLuongNG == 0)
                {
                    _repo.UpdateStatus(phieuXuLyId, QTChungStatus.HoanTat, nguoiQC);
                    TryHoanTatHeader(phieu.PhieuTraHangId, nguoiQC);
                    _uow.Commit();
                    return ScanResult.OK($"QC xác nhận (QcId={qcId}): OK={soLuongOK}, NG=0. QT chung hoàn tất.");
                }

                var ngResult = _reworkStockService.NhapLaiHangNG(phieuXuLyId, lotNo, soLuongNG, slotIdOK, slotIdNG, nguoiQC);
                if (!ngResult.IsOK) { SafeRollback(); return ScanResult.Fail("Lỗi nhập lại hàng NG: " + ngResult.Message); }
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaNhapLaiKho, nguoiQC);
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.HoanTat, nguoiQC);
                TryHoanTatHeader(phieu.PhieuTraHangId, nguoiQC);
                _uow.Commit();
                return ScanResult.OK($"QC xác nhận (QcId={qcId}): OK={soLuongOK}, NG={soLuongNG}. Đã nhập lại NG. QT chung hoàn tất.");
            }
            catch (Exception ex) { SafeRollback(); return ScanResult.Fail("Lỗi QC xác nhận cuối: " + ex.Message); }
        }

        public void GhiNhanKiemTraTem(int qcId, bool daKiemTra)
        {
            if (qcId <= 0) throw new ArgumentException("qcId không hợp lệ.", nameof(qcId));
            throw new NotImplementedException("Cần repository QC/FormInspection hiện tại.");
        }

        public ScanResult XacNhanChoGiaoBu(int phieuXuLyId, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.ChiGiaoBu) return ScanResult.Fail("Chỉ phiếu hướng ChiGiaoBu mới được xác nhận giao bù.");
            ValidateTransition(phieu, QTChungStatus.DaGiaoBu);
            try
            {
                _uow.Begin();
                var result = _giaoBuNGService.XacNhanHoanTatGiaoBu(phieu.PhieuTraHangId ?? 0, nguoiThucHien);
                if (!result.IsOK) { SafeRollback(); return result; }
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaGiaoBu, nguoiThucHien);
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.HoanTat, nguoiThucHien);
                if (phieu.PhieuTraHangId.HasValue && !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header?.Status == PhieuTraHangStatus.DangXuLyQTChung) _phieuTraHangRepo.UpdateStatus(header.Id, PhieuTraHangStatus.HoanTat, nguoiThucHien);
                }
                _uow.Commit();
                return result;
            }
            catch (Exception ex) { SafeRollback(); return ScanResult.Fail("Lỗi xác nhận giao bù: " + ex.Message); }
        }

        public ScanResult DanhDauChoGiaoBu(int phieuXuLyId, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.ChiGiaoBu) return ScanResult.Fail("Chỉ phiếu hướng ChiGiaoBu mới được đánh dấu chờ giao bù.");
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

        public ScanResult NhapLaiHangNG(int phieuXuLyId, string lotNo, int soLuongNG, int? slotIdOK, int? slotIdNG, string nguoiNhap)
        {
            ValidateNguoiThucHien(nguoiNhap);
            if (string.IsNullOrWhiteSpace(lotNo)) return ScanResult.Fail("LotNo không được rỗng.");
            if (soLuongNG <= 0) return ScanResult.Fail("SoLuongNG phải lớn hơn 0.");
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.Status != QTChungStatus.DaQCXacNhanCuoi) return ScanResult.Fail($"Chỉ được nhập lại hàng NG khi QT Chung đang DaQCXacNhanCuoi. Hiện tại: {phieu.Status}.");
            ValidateTransition(phieu, QTChungStatus.DaNhapLaiKho);
            try
            {
                _uow.Begin();
                var result = _reworkStockService.NhapLaiHangNG(phieuXuLyId, lotNo, soLuongNG, slotIdOK, slotIdNG, nguoiNhap);
                if (!result.IsOK) { SafeRollback(); return result; }
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.DaNhapLaiKho, nguoiNhap);
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.HoanTat, nguoiNhap);
                if (phieu.PhieuTraHangId.HasValue && !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header?.Status == PhieuTraHangStatus.DangXuLyQTChung) _phieuTraHangRepo.UpdateStatus(header.Id, PhieuTraHangStatus.HoanTat, nguoiNhap);
                }
                _uow.Commit();
                return result;
            }
            catch (Exception ex) { SafeRollback(); return ScanResult.Fail("Lỗi nhập lại hàng NG: " + ex.Message); }
        }

        public ScanResult HoanTat(int phieuXuLyId, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);
            if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.HoanTat)) return ScanResult.Fail($"Không thể chuyển QT Chung {phieu.Status} → {QTChungStatus.HoanTat}.");
            try
            {
                _uow.Begin();
                _repo.UpdateStatus(phieuXuLyId, QTChungStatus.HoanTat, nguoiThucHien);
                if (phieu.PhieuTraHangId.HasValue && !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header != null && header.Status == PhieuTraHangStatus.DangXuLyQTChung) _phieuTraHangRepo.UpdateStatus(header.Id, PhieuTraHangStatus.HoanTat, nguoiThucHien);
                }
                _uow.Commit();
                return ScanResult.OK("QT Chung đã hoàn tất.");
            }
            catch { SafeRollback(); throw; }
        }

        public ScanResult GiaoLaiBoPhanPhatHien(int phieuXuLyId, string boPhanNhan, int soLuongGiaoLai, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            if (string.IsNullOrWhiteSpace(boPhanNhan)) return ScanResult.Fail("BoPhanNhan không được rỗng.");
            if (soLuongGiaoLai <= 0) return ScanResult.Fail("SoLuongGiaoLai phải lớn hơn 0.");
            var phieu = GetRequired(phieuXuLyId);
            throw new InvalidOperationException($"GiaoLaiBoPhanPhatHien không thuộc state machine QT Chung của PhieuXuLyBatThuong Id={phieu.Id}. Thao tác này phải được thực hiện ở TraNoiBoService.");
        }

        public ScanResult HuyQTChung(int phieuXuLyId, string lyDoHuy, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            if (string.IsNullOrWhiteSpace(lyDoHuy)) return ScanResult.Fail("LyDoHuy không được rỗng.");
            var phieu = GetRequired(phieuXuLyId);
            ValidateTransition(phieu, QTChungStatus.Huy);
            try
            {
                _uow.Begin();
                bool okDetail = _repo.UpdateLyDoHuy(phieuXuLyId, phieu.Status, lyDoHuy, nguoiThucHien);
                if (!okDetail)
                {
                    _uow.Rollback();
                    return ScanResult.Fail($"Trạng thái của phiếu xử lý {phieuXuLyId} đã bị thay đổi bởi người khác — Vui lòng tải lại.");
                }
                if (phieu.PhieuTraHangId.HasValue && !_phieuTraHangRepo.ConChoXuLy(phieu.PhieuTraHangId.Value))
                {
                    var header = _phieuTraHangRepo.GetById(phieu.PhieuTraHangId.Value);
                    if (header != null && header.Status == PhieuTraHangStatus.DangXuLyQTChung)
                    {
                        bool okHeader = _phieuTraHangRepo.UpdateStatusIfCurrentIs(header.Id, PhieuTraHangStatus.DangXuLyQTChung, PhieuTraHangStatus.HoanTat, nguoiThucHien);
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
            catch (Exception ex) { SafeRollback(); return ScanResult.Fail("Lỗi khi thực hiện hủy QT Chung: " + ex.Message); }
        }

        public PhieuXuLyBatThuong GetById(int phieuXuLyId) => GetRequired(phieuXuLyId);

        private void UpdateStatusWithConcurrencyCheck(int phieuXuLyId, QTChungStatus expectedFrom, QTChungStatus newStatus, string nguoiThucHien)
        {
            bool success = _repo.UpdateStatusIfCurrentIs(phieuXuLyId, expectedFrom, newStatus, nguoiThucHien);
            if (!success) throw new InvalidOperationException($"Trạng thái phiếu {phieuXuLyId} đã bị thay đổi bởi người khác (Kỳ vọng: {expectedFrom}) — Vui lòng tải lại.");
        }

        public IReadOnlyList<QTChungStatus> GetAllowedNext(int phieuXuLyId)
        {
            var phieu = GetRequired(phieuXuLyId);
            var result = new List<QTChungStatus>();
            foreach (QTChungStatus candidate in Enum.GetValues(typeof(QTChungStatus)))
            {
                if (candidate == phieu.Status) continue;
                if (!_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)candidate)) continue;
                if (!QTChungBranchMap.IsReachableForHuong(candidate, phieu.HuongXuLy)) continue;
                result.Add(candidate);
            }
            return result;
        }

        public List<QTChungTimelineItem> GetTimeline(int phieuXuLyId)
        {
            GetRequired(phieuXuLyId);
            throw new NotImplementedException("IPhieuXuLyBatThuongRepository hiện tại chưa cung cấp GetTimeline().");
        }

        private void TryHoanTatHeader(int? phieuTraHangId, string nguoiThucHien)
        {
            if (!phieuTraHangId.HasValue) return;
            if (_phieuTraHangRepo.ConChoXuLy(phieuTraHangId.Value)) return;
            var header = _phieuTraHangRepo.GetById(phieuTraHangId.Value);
            if (header?.Status == PhieuTraHangStatus.DangXuLyQTChung)
                _phieuTraHangRepo.UpdateStatus(header.Id, PhieuTraHangStatus.HoanTat, nguoiThucHien);
        }

        internal static class QTChungBranchMap
        {
            private static readonly Dictionary<QTChungStatus, HuongXuLyBatThuong> _owner =
                new Dictionary<QTChungStatus, HuongXuLyBatThuong>
                {
                    { QTChungStatus.TuChoiGiaoBu, HuongXuLyBatThuong.TuChoiGiaoBu },
                    { QTChungStatus.ChoGiaoBu, HuongXuLyBatThuong.ChiGiaoBu },
                    { QTChungStatus.DaGiaoBu, HuongXuLyBatThuong.ChiGiaoBu },
                    { QTChungStatus.DaXuatKhoRework, HuongXuLyBatThuong.CanRework },
                    { QTChungStatus.DaGiaoSanXuat, HuongXuLyBatThuong.CanRework },
                    { QTChungStatus.DaQCXacNhanCuoi, HuongXuLyBatThuong.CanRework },
                    { QTChungStatus.DaNhapLaiKho, HuongXuLyBatThuong.CanRework }
                };

            public static bool IsReachableForHuong(QTChungStatus status, HuongXuLyBatThuong huong)
            {
                HuongXuLyBatThuong requiredHuong;
                if (!_owner.TryGetValue(status, out requiredHuong)) return true;
                return huong == requiredHuong;
            }
        }
    }
}
