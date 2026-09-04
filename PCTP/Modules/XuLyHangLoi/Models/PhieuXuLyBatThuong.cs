using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public class PhieuXuLyBatThuong
    {
        public int Id { get; set; }

        public string SoPhieu { get; set; }

        public NguonXuLyBatThuong Nguon { get; set; }

        // ============================================================
        // LIÊN KẾT NGHIỆP VỤ
        // ============================================================

        /// <summary>
        /// Phiếu trả hàng Header.
        /// </summary>
        public int? PhieuTraHangId { get; set; }

        /// <summary>
        /// Dòng trả hàng tạo ra phiếu xử lý bất thường.
        ///
        /// Một PhieuTraHangCT chỉ được sinh tối đa một
        /// PhieuXuLyBatThuong.
        /// </summary>
        public int PhieuTraHangCTId { get; set; }

        /// <summary>
        /// Denormalize để hiển thị/report.
        /// Không phải source of truth.
        /// </summary>
        public string SoPhieuTraHangGoc { get; set; }

        // ============================================================
        // NGUỒN KHÁCH
        // ============================================================

        /// <summary>
        /// Chỉ dùng khi Nguon = KhachTra.
        /// </summary>
        public int? PhieuKhachTraId { get; set; }

        // ============================================================
        // NGUỒN NỘI BỘ
        // ============================================================

        /// <summary>
        /// Chỉ dùng khi Nguon = TraNoiBo.
        /// </summary>
        public int? SlotIdNguon { get; set; }

        public string LotNguon { get; set; }

        // ============================================================
        // THÔNG TIN HÀNG
        // ============================================================

        public string Model { get; set; }

        public string MaSanPham { get; set; }

        public string SoLo { get; set; }

        public string SoLoLoi { get; set; }

        public int SoLuongLoi { get; set; }

        public string NoiDungBatThuong { get; set; }

        public string PhanLoaiXuLy { get; set; }

        public string BoPhanPhatHanh { get; set; }

        // ============================================================
        // QT CHUNG
        // ============================================================

        public QTChungStatus Status { get; set; }
            = QTChungStatus.Moi;

        /// <summary>
        /// Hướng xử lý do QC quyết định.
        /// Không phải state machine.
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
        /// Đặt trạng thái mới cho entity trong bộ nhớ (không tự query DB, không
        /// tự validate workflow). Method này KHÔNG còn gọi
        /// QTChungStatusTransition (đã xoá) — entity không nên tự phụ thuộc vào
        /// 1 service workflow qua DI.
        ///
        /// Việc kiểm tra transition có hợp lệ hay không (bảng
        /// sys_WorkflowTransitions + QTChungBranchMap theo HuongXuLy) PHẢI được
        /// gọi TRƯỚC ở tầng Service (xem QTChungService.ValidateTransition),
        /// trước khi gọi ChangeStatus() này. Nếu bạn thấy chỗ nào đang gọi
        /// ChangeStatus() trực tiếp mà KHÔNG qua service đã validate, đó là
        /// chỗ cần bổ sung validate, không phải tự ý cho ChangeStatus tự kiểm
        /// tra lại.
        /// </summary>
        public void ChangeStatus(
            QTChungStatus newStatus,
            string updatedBy)
        {
            Status = newStatus;
            UpdatedAt = DateTime.Now;
            UpdatedBy = updatedBy;
        }
    }
}
