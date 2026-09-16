using PCTP.Modules.KhoVatLy.Kho.Models;
using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Modules.XuLyHangLoi.Repository;
using PCTP.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCTP.Modules.XuLyHangLoi.Services
{
    /// <summary>
    /// Điều phối nghiệp vụ xuất kho đi rework / nhập lại sau rework / hoàn trả khi huỷ.
    ///
    /// Stock mutation không còn thuộc contract này: implementation phải route
    /// Slot/SlotLot/STOCKTP mutation qua KhoCore.IStockMovementService.
    /// Interface chỉ mô tả nghiệp vụ Rework; persistence details không được leak ra caller.
    /// </summary>
    public interface IReworkStockService
    {
        List<LotInfo> GetLotsCanRework(string maHang, string lotNo);
        List<LotInfo> GetLotsCanReworkByPhieuXuLy(int phieuXuLyId);

        ScanResult XuatKhoRework(
            int phieuXuLyId,
            int slotLotId,
            string lotNo,
            int soLuong,
            string nguoiXuat);

        ScanResult NhapLaiHangNG(
            int phieuXuLyId,
            string lotNo,
            int soLuongNG,
            int? slotIdOK,
            int? slotIdNG,
            string nguoiNhap);

        ScanResult NhapLaiHangOK(
            int phieuXuLyId,
            string lotNo,
            int soLuongOK,
            int slotIdOK,
            string nguoiNhap);

        ScanResult HoanTraKhoKhiHuy(
            int phieuXuLyId,
            string nguoiThucHien);
    }

    /// <summary>
    /// Phase 4 - nguồn sự thật của kế hoạch Rework.
    /// Không tự suy ra số lượng Rework từ tổng NG; chỉ dùng SoLuongRework đã
    /// được QC ban đầu phân bổ trong snapshot Phase 2 / Initial QC Phase 3.
    /// </summary>
    public sealed class ReworkPhase4Plan
    {
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongReworkDuocPhep { get; set; }
        public int SoLuongDaXuatRework { get; set; }
        public int SoLuongDaGiaoSanXuat { get; set; }

        public int SoLuongConPhaiXuat
        {
            get { return Math.Max(0, SoLuongReworkDuocPhep - SoLuongDaXuatRework); }
        }

        public int SoLuongConPhaiGiao
        {
            get { return Math.Max(0, SoLuongReworkDuocPhep - SoLuongDaGiaoSanXuat); }
        }

        public bool DaXuatDu
        {
            get { return SoLuongDaXuatRework == SoLuongReworkDuocPhep; }
        }

        public bool DaGiaoDu
        {
            get { return SoLuongDaGiaoSanXuat == SoLuongReworkDuocPhep; }
        }
    }

    public interface IReworkPhase4Service
    {
        ReworkPhase4Plan GetPlan(int phieuXuLyId);

        void EnsureCanExport(
            int phieuXuLyId,
            int soLuong,
            string nguoiThucHien);

        void EnsureCanDeliver(
            int phieuXuLyId,
            int soLuongGiao,
            string nguoiThucHien);

        void EnsureCanFinalQC(
            int phieuXuLyId,
            int soLuongOK,
            int soLuongNG,
            string nguoiQC);
    }

    /// <summary>
    /// Phase 4 guard. Đây là lớp chặn nghiệp vụ trước khi mutation stock / giao sản xuất / QC cuối.
    /// Nó không thực hiện mutation để giữ ranh giới giữa validation và transaction stock.
    /// </summary>
    public sealed class ReworkPhase4Service : IReworkPhase4Service
    {
        private readonly IPhieuXuLyBatThuongRepository _phieuRepo;
        private readonly ITraHangQTChungRepository _qtRepo;
        private readonly IInitialQCService _initialQC;

        public ReworkPhase4Service(
            IPhieuXuLyBatThuongRepository phieuRepo,
            ITraHangQTChungRepository qtRepo,
            IInitialQCService initialQC)
        {
            _phieuRepo = phieuRepo ?? throw new ArgumentNullException(nameof(phieuRepo));
            _qtRepo = qtRepo ?? throw new ArgumentNullException(nameof(qtRepo));
            _initialQC = initialQC ?? throw new ArgumentNullException(nameof(initialQC));
        }

        public ReworkPhase4Plan GetPlan(int phieuXuLyId)
        {
            if (phieuXuLyId <= 0)
                throw new ArgumentException("phieuXuLyId không hợp lệ.", nameof(phieuXuLyId));

            var phieu = _phieuRepo.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException("Không tìm thấy phiếu xử lý bất thường.");

            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                throw new InvalidOperationException("Phiếu không có hướng CanRework.");

            var qc = _initialQC.Get(phieuXuLyId);
            if (qc == null)
                throw new InvalidOperationException("Chưa có QC ban đầu. Không được chuyển sang Rework.");

            var exports = _qtRepo.GetXuat(phieuXuLyId) ?? new List<TraHangQTChungXuat>();
            var deliveries = _qtRepo.GetGiao(phieuXuLyId) ?? new List<TraHangQTChungGiao>();

            return new ReworkPhase4Plan
            {
                PhieuXuLyBatThuongId = phieuXuLyId,
                SoLuongReworkDuocPhep = qc.SoLuongRework,
                SoLuongDaXuatRework = exports
                    .Where(x => x != null && string.Equals(x.LoaiXuat, "Rework", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Max(0, x.SoLuongXuat)),
                SoLuongDaGiaoSanXuat = deliveries
                    .Where(x => x != null)
                    .Sum(x => Math.Max(0, x.SoLuongGiao))
            };
        }

        public void EnsureCanExport(int phieuXuLyId, int soLuong, string nguoiThucHien)
        {
            ValidateActor(nguoiThucHien);
            if (soLuong <= 0)
                throw new InvalidOperationException("Số lượng xuất Rework phải lớn hơn 0.");

            var phieu = _phieuRepo.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException("Không tìm thấy phiếu xử lý bất thường.");
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                throw new InvalidOperationException("Chỉ phiếu CanRework mới được xuất Rework.");
            if (phieu.Status != QTChungStatus.DaDinhHuong &&
                phieu.Status != QTChungStatus.DaXuatKhoRework)
                throw new InvalidOperationException($"Không thể xuất Rework khi trạng thái là {phieu.Status}.");

            var plan = GetPlan(phieuXuLyId);
            if (plan.SoLuongReworkDuocPhep <= 0)
                throw new InvalidOperationException("QC ban đầu không phân bổ số lượng Rework.");
            if (plan.SoLuongDaXuatRework + soLuong > plan.SoLuongReworkDuocPhep)
                throw new InvalidOperationException(
                    $"Xuất Rework vượt số lượng được QC phân bổ. Được phép={plan.SoLuongReworkDuocPhep:n0}, " +
                    $"đã xuất={plan.SoLuongDaXuatRework:n0}, yêu cầu thêm={soLuong:n0}.");
        }

        public void EnsureCanDeliver(int phieuXuLyId, int soLuongGiao, string nguoiThucHien)
        {
            ValidateActor(nguoiThucHien);
            if (soLuongGiao <= 0)
                throw new InvalidOperationException("Số lượng giao Rework phải lớn hơn 0.");

            var phieu = _phieuRepo.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException("Không tìm thấy phiếu xử lý bất thường.");
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                throw new InvalidOperationException("Chỉ phiếu CanRework mới được giao sản xuất.");
            if (phieu.Status != QTChungStatus.DaXuatKhoRework)
                throw new InvalidOperationException($"Phải xuất đủ hàng Rework trước khi giao. Trạng thái hiện tại: {phieu.Status}.");

            var plan = GetPlan(phieuXuLyId);
            if (!plan.DaXuatDu)
                throw new InvalidOperationException(
                    $"Chưa xuất đủ hàng Rework: còn {plan.SoLuongConPhaiXuat:n0}.");

            if (plan.SoLuongDaGiaoSanXuat + soLuongGiao > plan.SoLuongReworkDuocPhep)
                throw new InvalidOperationException("Tổng giao Rework vượt số lượng được QC phân bổ.");
        }

        public void EnsureCanFinalQC(int phieuXuLyId, int soLuongOK, int soLuongNG, string nguoiQC)
        {
            ValidateActor(nguoiQC);
            if (soLuongOK < 0 || soLuongNG < 0)
                throw new InvalidOperationException("Kết quả QC cuối không được âm.");

            var phieu = _phieuRepo.GetById(phieuXuLyId);
            if (phieu == null)
                throw new InvalidOperationException("Không tìm thấy phiếu xử lý bất thường.");
            if (phieu.HuongXuLy != HuongXuLyBatThuong.CanRework)
                throw new InvalidOperationException("QC cuối chỉ áp dụng cho phiếu CanRework.");
            if (phieu.Status != QTChungStatus.DaGiaoSanXuat)
                throw new InvalidOperationException($"Chỉ QC cuối sau khi giao sản xuất. Trạng thái hiện tại: {phieu.Status}.");

            var plan = GetPlan(phieuXuLyId);
            if (!plan.DaGiaoDu)
                throw new InvalidOperationException(
                    $"Chưa giao đủ hàng Rework: còn {plan.SoLuongConPhaiGiao:n0}.");

            if (soLuongOK + soLuongNG != plan.SoLuongReworkDuocPhep)
                throw new InvalidOperationException(
                    $"QC cuối phải kiểm tra đủ {plan.SoLuongReworkDuocPhep:n0}. " +
                    $"Hiện OK+NG={soLuongOK + soLuongNG:n0}.");
        }

        private static void ValidateActor(string actor)
        {
            if (string.IsNullOrWhiteSpace(actor))
                throw new ArgumentException("Người thực hiện không được rỗng.", nameof(actor));
        }
    }
}
