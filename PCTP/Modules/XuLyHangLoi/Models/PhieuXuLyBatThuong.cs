using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Aggregate root của xử lý hàng bất thường.
    /// Quality Result, Rework, Disposition và Customer Compensation được
    /// tách thành các child model để không trộn lẫn các khái niệm nghiệp vụ.
    /// </summary>
    public class PhieuXuLyBatThuong
    {
        public int Id { get; set; }
        public string SoPhieu { get; set; }
        public NguonXuLyBatThuong Nguon { get; set; }

        public int? PhieuTraHangId { get; set; }
        public int PhieuTraHangCTId { get; set; }
        public string SoPhieuTraHangGoc { get; set; }

        public int? PhieuKhachTraId { get; set; }

        public int? SlotIdNguon { get; set; }
        public string LotNguon { get; set; }

        public string Model { get; set; }
        public string MaSanPham { get; set; }
        public string SoLo { get; set; }
        public string SoLoLoi { get; set; }

        /// <summary>
        /// Legacy quantity. Không dùng làm source of truth cho QC/Rework/Giao bù mới.
        /// </summary>
        public int SoLuongLoi { get; set; }

        public string NoiDungBatThuong { get; set; }
        public string PhanLoaiXuLy { get; set; }
        public string BoPhanPhatHanh { get; set; }

        // ============================================================
        // PHASE 1 - DOMAIN DETAILS
        // ============================================================
        public virtual List<PhieuXuLyBatThuongAffectedLot> AffectedLots { get; set; }
            = new List<PhieuXuLyBatThuongAffectedLot>();

        public virtual QCInspectionResult QCInspection { get; set; }
        public virtual ReworkResult Rework { get; set; }
        public virtual DispositionResult Disposition { get; set; }
        public virtual CustomerCompensation Compensation { get; set; }

        // ============================================================
        // QT CHUNG
        // ============================================================
        public QTChungStatus Status { get; set; } = QTChungStatus.Moi;

        /// <summary>Hướng xử lý do QC quyết định; không phải state machine.</summary>
        public HuongXuLyBatThuong HuongXuLy { get; set; }
            = HuongXuLyBatThuong.ChuaXacDinh;

        public DateTime? NgayDinhHuong { get; set; }
        public string NguoiDinhHuong { get; set; }

        // ============================================================
        // AUDIT
        // ============================================================
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public string LyDoHuy { get; set; }
        public DateTime? NgayHuy { get; set; }
        public string NguoiHuy { get; set; }

        /// <summary>
        /// Workflow transition phải được validate ở Service trước khi gọi method này.
        /// </summary>
        public void ChangeStatus(QTChungStatus newStatus, string updatedBy)
        {
            Status = newStatus;
            UpdatedAt = DateTime.Now;
            UpdatedBy = updatedBy;
        }
    }

    /// <summary>Nguồn snapshot khi truy vết LOT bị ảnh hưởng.</summary>
    public enum AffectedLotSourceType
    {
        Kho = 1,
        SanXuat = 2,
        KhachTra = 3
    }

    /// <summary>
    /// Snapshot nguồn hàng bị ảnh hưởng tại thời điểm truy vết LOT.
    /// </summary>
    public class PhieuXuLyBatThuongAffectedLot
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public AffectedLotSourceType SourceType { get; set; }
        public string SourceReference { get; set; }
        public int? SlotId { get; set; }
        public string LotNo { get; set; }
        public string MaSanPham { get; set; }
        public string Model { get; set; }
        public int SoLuongAnhHuong { get; set; }
        public int SoLuongDaKiemTra { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongLoaiBo { get; set; }
        public DateTime SnapshotAt { get; set; }
        public string SnapshotBy { get; set; }
    }

    /// <summary>Kết quả QC lần kiểm tra ban đầu.</summary>
    public class QCInspectionResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongAnhHuong { get; set; }
        public int SoLuongDaKiemTra { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongLoaiBoBanDau { get; set; }
        public string NoiDungKiemTra { get; set; }
        public string KetLuan { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }
    }

    /// <summary>Kết quả QC sau Rework; không ghi đè kết quả QC ban đầu.</summary>
    public class ReworkResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongRework { get; set; }
        public int SoLuongOK { get; set; }
        public int SoLuongNG { get; set; }
        public string KetLuan { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }
    }

    /// <summary>Kết quả disposition cuối cùng của hàng NG.</summary>
    public class DispositionResult
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int SoLuongLoaiBoBanDau { get; set; }
        public int SoLuongReworkNG { get; set; }
        public int SoLuongLoaiBoCuoi { get; set; }
        public string LyDo { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string ConfirmedBy { get; set; }
    }

    /// <summary>
    /// Nghĩa vụ giao bù cho khách hàng. Quantity lấy từ đơn hàng/đợt giao bị ảnh hưởng,
    /// không tính từ NG sau Rework.
    /// </summary>
    public class CustomerCompensation
    {
        public int Id { get; set; }
        public int PhieuXuLyBatThuongId { get; set; }
        public int? PhieuKhachTraId { get; set; }
        public int? OrderId { get; set; }
        public int? OrderLineId { get; set; }
        public string OriginalDeliveryReference { get; set; }
        public int SoLuongCanGiaoBu { get; set; }
        public int SoLuongDaGiaoBu { get; set; }
        public int SoLuongConLai
        {
            get { return Math.Max(0, SoLuongCanGiaoBu - SoLuongDaGiaoBu); }
        }
        public bool IsCompleted
        {
            get { return SoLuongDaGiaoBu >= SoLuongCanGiaoBu; }
        }
        public DateTime? CompletedAt { get; set; }
        public string CompletedBy { get; set; }
    }
}
