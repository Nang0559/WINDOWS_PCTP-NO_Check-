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
            ValidateNguoiThucHien(nguoiThucHien);

            var ct = _phieuTraHangRepo.GetItemById(phieuTraHangCTId);
            if (ct == null)
                throw new InvalidOperationException($"Không tìm thấy dòng phiếu trả PhieuTraHangCT Id={phieuTraHangCTId}.");

            var header = _phieuTraHangRepo.GetById(ct.PhieuTraHangId);
            if (header == null)
                throw new InvalidOperationException($"Không tìm thấy phiếu trả hàng Id={ct.PhieuTraHangId}.");

            try
            {
                _uow.Begin();
                int stt = (_repo.GetAllByPhieuTraHangId(header.Id) ?? new List<PhieuXuLyBatThuong>()).Count + 1;
                var id = TaoPhieuXuLyCore(header, ct, stt, model, phanLoaiXuLy, boPhanPhatHanh, nguoiThucHien);
                NangTrangThaiHeader(header.Id, PhieuTraHangStatus.DaTaoPhieuBatThuong, nguoiThucHien);
                _uow.Commit();
                return id;
            }
            catch { SafeRollback(); throw; }
        }

        public List<int> TaoPhieuXuLyTuPhieuTra(int phieuTraHangId, string model, string phanLoaiXuLy, string nguoiThucHien)
        {
            if (phieuTraHangId <= 0) throw new ArgumentException("phieuTraHangId không hợp lệ.", nameof(phieuTraHangId));
            ValidateNguoiThucHien(nguoiThucHien);

            var header = _phieuTraHangRepo.GetById(phieuTraHangId);
            if (header == null)
                throw new InvalidOperationException($"Không tìm thấy phiếu trả hàng Id={phieuTraHangId}.");

            var chuaXuLy = new HashSet<int>(_phieuTraHangRepo.GetItemIdsChuaCoPhieuXuLy(phieuTraHangId));
            var items = (_phieuTraHangRepo.GetItems(phieuTraHangId) ?? new List<PhieuTraHangCT>())
                .Where(x => chuaXuLy.Contains(x.Id))
                .ToList();

            if (items.Count == 0)
                throw new InvalidOperationException("Phiếu này không còn dòng nào chưa tạo phiếu xử lý bất thường.");

            var ids = new List<int>();

            try
            {
                _uow.Begin();

                int stt = (_repo.GetAllByPhieuTraHangId(header.Id) ?? new List<PhieuXuLyBatThuong>()).Count;

                foreach (var ct in items)
                    ids.Add(TaoPhieuXuLyCore(header, ct, ++stt, model, phanLoaiXuLy, null, nguoiThucHien));

                NangTrangThaiHeader(header.Id, PhieuTraHangStatus.DaTaoPhieuBatThuong, nguoiThucHien);
                _uow.Commit();
                return ids;
            }
            catch { SafeRollback(); throw; }
        }

        // Không tự mở transaction: gọi trong _uow.Begin()/Commit() của hàm public.
        private int TaoPhieuXuLyCore(PhieuTraHang header, PhieuTraHangCT ct, int stt, string model, string phanLoaiXuLy, string boPhanPhatHanh, string nguoiThucHien)
        {
            if (string.IsNullOrWhiteSpace(ct.MaHang))
                throw new InvalidOperationException($"Dòng phiếu trả Id={ct.Id} thiếu Mã hàng.");
            if (ct.SoLuong <= 0)
                throw new InvalidOperationException($"Dòng phiếu trả Id={ct.Id} ({ct.MaHang}) có số lượng không hợp lệ.");

            if (header.Nguon == NguonXuLyBatThuong.TraNoiBo)
            {
                if (!ct.SlotIdNguon.HasValue || ct.SlotIdNguon.Value <= 0)
                    throw new InvalidOperationException($"Dòng {ct.MaHang} của phiếu nội bộ chưa có Slot nguồn.");
                if (string.IsNullOrWhiteSpace(ct.LotNo))
                    throw new InvalidOperationException($"Dòng {ct.MaHang} của phiếu nội bộ chưa có LOT.");
            }

            // Model không bắt buộc nhập: mặc định theo mã hàng của dòng.
            string modelValue = string.IsNullOrWhiteSpace(model) ? ct.MaHang.Trim() : model.Trim();

            string phanLoai = string.IsNullOrWhiteSpace(phanLoaiXuLy) ? "Hàng lỗi" : phanLoaiXuLy.Trim();

            // Bộ phận phát hành: lấy từ phiếu trả (BP phát hiện lỗi → Phòng ban) khi không truyền.
            string boPhan = !string.IsNullOrWhiteSpace(boPhanPhatHanh) ? boPhanPhatHanh.Trim()
                : !string.IsNullOrWhiteSpace(header.BoPhanPhatHienLoi) ? header.BoPhanPhatHienLoi.Trim()
                : !string.IsNullOrWhiteSpace(header.PhongBan) ? header.PhongBan.Trim()
                : "Chưa xác định";

            string noiDung = header.LyDo;
            if (!string.IsNullOrWhiteSpace(ct.LyDoNg))
                noiDung = string.IsNullOrWhiteSpace(noiDung) ? ct.LyDoNg : noiDung + " | " + ct.LyDoNg;

            var p = new PhieuXuLyBatThuong
            {
                // Ngắn gọn để không vượt độ dài cột: {SoPhieu phiếu trả}-{số thứ tự}
                SoPhieu = $"{header.SoPhieu}-{stt}",
                Nguon = header.Nguon,
                PhieuTraHangId = header.Id,
                SoPhieuTraHangGoc = header.SoPhieu,
                // Phiếu khách trả: header phiếu trả chính là chứng từ khách trả.
                PhieuKhachTraId = header.Nguon == NguonXuLyBatThuong.KhachTra ? (int?)header.Id : null,
                SlotIdNguon = header.Nguon == NguonXuLyBatThuong.TraNoiBo ? ct.SlotIdNguon : null,
                LotNguon = header.Nguon == NguonXuLyBatThuong.TraNoiBo ? ct.LotNo : null,
                Model = modelValue,
                MaSanPham = ct.MaHang.Trim(),
                SoLo = ct.LotNo,
                SoLoLoi = ct.LotNo,
                SoLuongLoi = ct.SoLuong,
                NoiDungBatThuong = noiDung,
                PhanLoaiXuLy = phanLoai,
                BoPhanPhatHanh = boPhan,
                Status = QTChungStatus.Moi,
                CreatedBy = nguoiThucHien.Trim()
            };

            var id = _repo.Insert(ct.Id, p);
            _repo.UpdateStatus(id, QTChungStatus.DaTaoPhieuBatThuong, nguoiThucHien);
            return id;
        }

        // Roll-up trạng thái header: chỉ tiến lên và chỉ khi workflow (DB) cho phép.
        private void NangTrangThaiHeader(int phieuTraHangId, PhieuTraHangStatus target, string nguoiThucHien)
        {
            var header = _phieuTraHangRepo.GetById(phieuTraHangId);
            if (header == null) return;
            if ((int)header.Status >= (int)target) return;
            if (!_workflow.CanTransition(ProcessCodePhieuTraHang, (int)header.Status, (int)target)) return;
            _phieuTraHangRepo.UpdateStatus(header.Id, target, nguoiThucHien);
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
                // Header bắt đầu "đang xử lý QT Chung" → cho phép đóng phiếu khi xử lý xong.
                if (phieu.PhieuTraHangId.HasValue)
                    NangTrangThaiHeader(phieu.PhieuTraHangId.Value, PhieuTraHangStatus.DangXuLyQTChung, nguoiThucHien);
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

            return _reworkStockService.GetLotsCanReworkByPhieuXuLy(phieuXuLyId)
                ?? new List<LotInfo>();
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
            if (phieu.Status != QTChungStatus.DaDinhHuong &&
                phieu.Status != QTChungStatus.DaXuatKhoRework)
                return ScanResult.Fail($"Chỉ được xuất rework khi phiếu ở trạng thái DaDinhHuong hoặc DaXuatKhoRework. Hiện tại: {phieu.Status}.");
            if (phieu.Status == QTChungStatus.DaDinhHuong &&
                !_workflow.CanTransition(ProcessCodeQTChung, (int)phieu.Status, (int)QTChungStatus.DaXuatKhoRework))
                return ScanResult.Fail($"Workflow không cho phép chuyển {phieu.Status} → {QTChungStatus.DaXuatKhoRework}.");

            try
            {
                _uow.Begin();
                var result = _reworkStockService.XuatKhoRework(phieuXuLyId, slotId, lotNo, soLuong, nguoiXuat);
                if (!result.IsOK) { SafeRollback(); return result; }

                // Chỉ chuyển trạng thái ở lần xuất đầu tiên; cho phép nhiều lần xuất
                // trong cùng trạng thái DaXuatKhoRework cho đến khi đủ kế hoạch.
                if (phieu.Status == QTChungStatus.DaDinhHuong &&
                    !_repo.UpdateStatusIfCurrentIs(phieuXuLyId, QTChungStatus.DaDinhHuong, QTChungStatus.DaXuatKhoRework, nguoiXuat))
                {
                    SafeRollback();
                    return ScanResult.Fail("Trạng thái phiếu đã thay đổi trong lúc xuất rework. Vui lòng tải lại.");
                }

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

            DateTime ngayGiaoParsed;
            if (!DateTime.TryParse(ngayGiao.Trim(), out ngayGiaoParsed))
                return ScanResult.Fail("NgayGiao không đúng định dạng ngày.");

            var xuat = (_traHangQTChungRepository.GetXuat(phieuXuLyId) ?? new List<TraHangQTChungXuat>())
                .Where(x => x != null && string.Equals(x.LoaiXuat, "Rework", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var giaoHienTai = _traHangQTChungRepository.GetGiao(phieuXuLyId) ?? new List<TraHangQTChungGiao>();
            if (xuat.Count == 0)
                return ScanResult.Fail("Chưa có lần xuất kho rework nào cho phiếu này.");

            var daXuatTheoLot = xuat
                .Where(x => !string.IsNullOrWhiteSpace(x.LotXuat))
                .GroupBy(x => x.LotXuat.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.SoLuongXuat)), StringComparer.OrdinalIgnoreCase);
            var daGiaoTheoLot = giaoHienTai
                .Where(x => !string.IsNullOrWhiteSpace(x.LotGiao))
                .GroupBy(x => x.LotGiao.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.SoLuongGiao)), StringComparer.OrdinalIgnoreCase);

            var lotGiaoHopLe = new List<LotInfo>();
            foreach (var lot in lots)
            {
                if (lot == null || string.IsNullOrWhiteSpace(lot.LotNo) || lot.Quantity <= 0)
                    return ScanResult.Fail("Mỗi dòng giao rework phải có LOT và số lượng lớn hơn 0.");
                if (!string.IsNullOrWhiteSpace(lot.ItemCode) &&
                    !string.Equals(lot.ItemCode.Trim(), (phieu.MaSanPham ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                    return ScanResult.Fail($"Mã hàng của LOT [{lot.LotNo}] không khớp phiếu xử lý.");

                string key = lot.LotNo.Trim();
                int tongDaXuat;
                if (!daXuatTheoLot.TryGetValue(key, out tongDaXuat))
                    return ScanResult.Fail($"LOT [{key}] chưa được xuất kho cho phiếu rework này.");
                int tongDaGiao;
                daGiaoTheoLot.TryGetValue(key, out tongDaGiao);
                int soLuongYeuCauTruocDo = lotGiaoHopLe.Where(x => string.Equals(x.LotNo.Trim(), key, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity);
                if (tongDaGiao + soLuongYeuCauTruocDo + lot.Quantity > tongDaXuat)
                    return ScanResult.Fail($"Giao LOT [{key}] vượt số lượng đã xuất. Đã xuất={tongDaXuat:n0}, đã giao={tongDaGiao:n0}, yêu cầu thêm={soLuongYeuCauTruocDo + lot.Quantity:n0}.");
                lotGiaoHopLe.Add(lot);
            }

            if (lotGiaoHopLe.Count == 0)
                return ScanResult.Fail("Không có LOT hợp lệ để giao sản xuất.");

            int tongXuatRework = xuat.Sum(x => Math.Max(0, x.SoLuongXuat));
            int tongDaGiaoTruoc = giaoHienTai.Sum(x => Math.Max(0, x.SoLuongGiao));
            int tongYeuCauGiao = lotGiaoHopLe.Sum(x => x.Quantity);
            if (tongDaGiaoTruoc + tongYeuCauGiao != tongXuatRework)
                return ScanResult.Fail($"Chỉ được chuyển sang giao sản xuất khi giao đủ số lượng đã xuất rework. Đã xuất={tongXuatRework:n0}, đã giao={tongDaGiaoTruoc:n0}, yêu cầu lần này={tongYeuCauGiao:n0}.");

            try
            {
                _uow.Begin();
                foreach (var lot in lotGiaoHopLe)
                {
                    _traHangQTChungRepository.InsertGiao(new TraHangQTChungGiao
                    {
                        PhieuXuLyBatThuongId = phieuXuLyId,
                        LotGiao = lot.LotNo.Trim(),
                        MaHang = string.IsNullOrWhiteSpace(lot.ItemCode) ? phieu.MaSanPham : lot.ItemCode.Trim(),
                        SoLuongGiao = lot.Quantity,
                        ThoiGian = DateTime.Now,
                        NgayGiao = ngayGiao.Trim(),
                        NguoiNhan = nguoiNhan.Trim(),
                        BoPhanNhan = boPhanNhan.Trim(),
                        Note = "Giao rework"
                    });
                }

                if (!_repo.UpdateStatusIfCurrentIs(phieuXuLyId, phieu.Status, QTChungStatus.DaGiaoSanXuat, nguoiNhan))
                {
                    SafeRollback();
                    return ScanResult.Fail("Trạng thái phiếu đã thay đổi trong lúc ghi nhận giao rework. Vui lòng tải lại.");
                }

                _uow.Commit();
                return ScanResult.OK($"Đã ghi nhận giao rework {lotGiaoHopLe.Sum(x => x.Quantity):n0} sản phẩm cho bộ phận {boPhanNhan.Trim()}.");
            }
            catch (Exception ex)
            {
                SafeRollback();
                return ScanResult.Fail("Lỗi ghi nhận giao rework: " + ex.Message);
            }
        }

        public void GhiNhanDangRework(int phieuXuLyId, string ghiChu, string nguoiThucHien)
        {
            ValidateNguoiThucHien(nguoiThucHien);
            var phieu = GetRequired(phieuXuLyId);
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                throw new InvalidOperationException("Chỉ phiếu CanRework mới được ghi nhận rework.");
            if (phieu.Status != QTChungStatus.DaGiaoSanXuat)
                throw new InvalidOperationException($"Không thể ghi nhận đang rework khi QT Chung đang {phieu.Status}.");
            if (string.IsNullOrWhiteSpace(ghiChu))
                throw new ArgumentException("Ghi chú rework không được rỗng.", nameof(ghiChu));

            _traHangQTChungRepository.UpdateLatestGiaoNote(phieuXuLyId, ghiChu.Trim(), nguoiThucHien.Trim());
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
            _traHangQTChungRepository.UpdateDaKiemTraTem(qcId, daKiemTra);
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
            return _traHangQTChungRepository.GetTimeline(phieuXuLyId) ?? new List<QTChungTimelineItem>();
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
