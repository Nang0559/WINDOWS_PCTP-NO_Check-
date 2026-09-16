using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    /// <summary>
    /// Aggregate root của xử lý hàng bất thường.
    ///
    /// Các số lượng QC/Rework/Loại bỏ/Giao bù được tách thành các model con
    /// để không trộn lẫn Quality Result với Customer Compensation.
    /// </summary>
    public class PhieuXuLyBatThuong
    {
        public int Id { get; set; }
        public string SoPhieu { get; set; }
        public NguonXuLyBatThuong Nguon { get; set; }

        // ============================================================
        // LIÊN KẾT NGHIỆP VỤ
        // ============================================================
        public int? PhieuTraHangId { get; set; }
        public int PhieuTraHangCTId { get; set; }
        public string SoPhieuTraHangGoc { get; set; }

        // ============================================================
        // NGUỒN KHÁCH
        // ============================================================
        public int? PhieuKhachTraId { get; set; }

        // ============================================================
        // NGUỒN NỘI BỘ
        // ============================================================
        public int? SlotIdNguon { get; set; }
        public string LotNguon { get; set; }

        // ============================================================
        // THÔNG TIN HÀNG
        // ============================================================
        public string Model { get; set; }
        public string MaSanPham { get; set; }
        public string SoLo { get; set; }
        public string SoLoLoi { get; set; }

        /// <summary>
        /// Legacy quantity của phiếu cũ. Không dùng làm source of truth cho
        /// QC/Rework/Giao bù mới; các quantity mới nằm ở child models.
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

        /// <summary>
        /// Hướng xử lý do QC quyết định. Không phải state machine.
        /// </summary>
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
        /// Đặt trạng thái mới cho entity trong bộ nhớ.
        /// Workflow transition phải được validate ở Service trước khi gọi.
        /// </summary>
        public void ChangeStatus(QTChungStatus newStatus, string updatedBy)
        {
            Status = newStatus;
            UpdatedAt = DateTime.Now;
            UpdatedBy = updatedBy;
        }
    }
}
