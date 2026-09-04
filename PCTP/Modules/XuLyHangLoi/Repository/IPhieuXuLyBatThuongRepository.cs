using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using PCTP.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    /// <summary>
    /// Repository thao tác với bảng FVN_PhieuXuLyBatThuong.
    /// 
    /// Repository này chịu trách nhiệm:
    /// - Tạo phiếu xử lý bất thường.
    /// - Tra cứu phiếu theo Id / Nguồn / PhieuTraHangId.
    /// - Đọc và cập nhật trạng thái QTChung.
    /// - Ghi nhận kết quả QC Định Hướng.
    /// 
    /// Không chứa logic nghiệp vụ chuyển trạng thái.
    /// Việc kiểm tra transition thuộc QTChungStatusTransition / Service.
    /// </summary>
    public interface IPhieuXuLyBatThuongRepository
    {
        // ============================================================
        // TẠO PHIẾU XỬ LÝ BẤT THƯỜNG
        // ============================================================
        QTChungStatus? GetStatus(int id);


        bool UpdateStatusIfCurrentIs(int id, QTChungStatus expectedFrom, QTChungStatus newStatus, string nguoiThucHien);
          
        /// <summary>
        /// Tạo mới một phiếu xử lý bất thường cho một dòng
        /// PhieuTraHangCT.
        ///
        /// Quan hệ:
        ///     PhieuTraHang
        ///          └── PhieuTraHangCT
        ///                    └── PhieuXuLyBatThuong
        ///
        /// Tham số phieuTraHangCTId dùng để xác định dòng trả hàng
        /// đang phát sinh phiếu xử lý bất thường.
        ///
        /// Repository thực hiện việc lưu phiếu và các khóa liên kết
        /// cần thiết theo nguồn phát sinh.
        ///
        /// Nguon = KhachTra:
        ///     - PhieuTraHangId
        ///     - PhieuKhachTraId nếu nghiệp vụ sử dụng
        ///
        /// Nguon = TraNoiBo:
        ///     - PhieuTraHangId
        ///     - SlotIdNguon
        ///     - LotNguon
        ///
        /// Trạng thái ban đầu bắt buộc:
        ///     QTChungStatus.Moi
        ///
        /// Việc phiếu có được phép tạo hay không là trách nhiệm của
        /// Service; Repository chịu trách nhiệm persistence và các
        /// ràng buộc dữ liệu trực tiếp.
        /// </summary>
        /// <param name="phieuTraHangCTId">
        /// Id dòng PhieuTraHangCT phát sinh xử lý bất thường.
        /// </param>
        /// <param name="p">
        /// Phiếu xử lý bất thường cần tạo.
        /// </param>
        /// <returns>
        /// Id của PhieuXuLyBatThuong vừa được tạo.
        /// </returns>
        int Insert(
            int phieuTraHangCTId,
            PhieuXuLyBatThuong p);


        // ============================================================
        // TRA CỨU
        // ============================================================

        /// <summary>
        /// Lấy một phiếu xử lý bất thường theo Id.
        /// </summary>
        /// <param name="id">
        /// Id của PhieuXuLyBatThuong.
        /// </param>
        /// <returns>
        /// Phiếu xử lý bất thường nếu tồn tại;
        /// null nếu không tìm thấy.
        /// </returns>
        PhieuXuLyBatThuong GetById(int id);


        /// <summary>
        /// Lấy danh sách phiếu xử lý bất thường theo nguồn phát sinh.
        ///
        /// Nguồn:
        ///     - NguonXuLyBatThuong.KhachTra
        ///     - NguonXuLyBatThuong.TraNoiBo
        ///
        /// Kết quả được sắp xếp theo CreatedAt giảm dần
        /// để phiếu mới nhất đứng trước.
        /// </summary>
        /// <param name="nguon">
        /// Nguồn phát sinh phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Danh sách PhieuXuLyBatThuong thuộc nguồn chỉ định.
        /// </returns>
        List<PhieuXuLyBatThuong> GetByNguon(
            NguonXuLyBatThuong nguon);


        /// <summary>
        /// Lấy phiếu xử lý bất thường theo PhieuTraHangId.
        ///
        /// PhieuTraHangId là khóa liên kết nghiệp vụ giữa:
        ///
        ///     FVN_PhieuTraHang
        ///             ↓
        ///     FVN_PhieuXuLyBatThuong
        ///
        /// Không dùng SoPhieu để join giữa hai bảng.
        ///
        /// Nếu một PhieuTraHang có nhiều phiếu xử lý bất thường,
        /// phương thức trả về phiếu mới nhất theo CreatedAt.
        /// </summary>
        /// <param name="phieuTraHangId">
        /// Id của PhieuTraHang gốc.
        /// </param>
        /// <returns>
        /// Phiếu xử lý bất thường mới nhất nếu tồn tại;
        /// null nếu chưa có.
        /// </returns>
        PhieuXuLyBatThuong GetByPhieuTraHangId(
            int phieuTraHangId);


        // ============================================================
        // STATE — QT CHUNG
        // ============================================================

        /// <summary>
        /// Cập nhật trạng thái QTChung của phiếu xử lý bất thường.
        ///
        /// Repository chỉ chịu trách nhiệm ghi trạng thái xuống DB.
        ///
        /// Repository KHÔNG tự quyết định transition có hợp lệ hay không.
        /// Service phải kiểm tra trước bằng:
        ///
        ///     QTChungStatusTransition.IsValidTransition(...)
        ///
        /// Sau khi cập nhật:
        ///     - Status = status
        ///     - UpdatedAt được cập nhật
        ///     - UpdatedBy = nguoiThucHien
        /// </summary>
        /// <param name="id">
        /// Id PhieuXuLyBatThuong.
        /// </param>
        /// <param name="status">
        /// Trạng thái QTChung mới.
        /// </param>
        /// <param name="nguoiThucHien">
        /// Người thực hiện thao tác.
        /// </param>
        void UpdateStatus(
            int id,
            QTChungStatus status,
            string nguoiThucHien);


        /// <summary>
        /// Đọc trạng thái QTChung hiện tại của phiếu xử lý bất thường.
        /// </summary>
        /// <param name="id">
        /// Id PhieuXuLyBatThuong.
        /// </param>
        /// <returns>
        /// QTChungStatus hiện tại.
        /// </returns>
        //QTChungStatus GetStatus(int id);


        // ============================================================
        // QC ĐỊNH HƯỚNG XỬ LÝ
        // ============================================================

        /// <summary>
        /// Ghi nhận hướng xử lý do QC xác định.
        ///
        /// Các hướng hợp lệ:
        ///     - TuChoiGiaoBu
        ///     - ChiGiaoBu
        ///     - CanRework
        ///
        /// Repository cập nhật:
        ///     - HuongXuLy
        ///     - NgayDinhHuong
        ///     - NguoiDinhHuong
        ///     - UpdatedAt
        ///     - UpdatedBy
        ///
        /// Phương thức này KHÔNG tự động chuyển QTChungStatus.
        ///
        /// Sau khi ghi nhận hướng xử lý, Service phải thực hiện
        /// transition:
        ///
        ///     DaTaoPhieuBatThuong
        ///             ↓
        ///     DaDinhHuongRework
        ///
        /// và các transition tiếp theo phải phụ thuộc vào
        /// HuongXuLyBatThuong.
        /// </summary>
        /// <param name="id">
        /// Id PhieuXuLyBatThuong.
        /// </param>
        /// <param name="huongXuLy">
        /// Hướng xử lý do QC xác định.
        /// </param>
        /// <param name="nguoiThucHien">
        /// Người thực hiện định hướng.
        /// </param>
        void UpdateDinhHuong(
            int id,
            HuongXuLyBatThuong huongXuLy,
            string nguoiThucHien);
        /// <summary>
        /// Ghi nhận lý do hủy phiếu và chuyển trạng thái sang QTChungStatus.Huy
        /// trong CÙNG 1 câu UPDATE — atomic, tránh race condition giữa việc đổi
        /// Status và ghi LyDoHuy qua 2 lệnh rời rạc.
        ///
        /// Dùng pattern optimistic-check giống UpdateStatusIfCurrentIs: chỉ
        /// UPDATE thành công nếu Status hiện tại đúng bằng expectedFrom — Service
        /// vẫn phải tự validate qua QTChungStatusTransition.IsValidTransition(...)
        /// TRƯỚC khi gọi, Repository chỉ đảm bảo tính atomic ở tầng DB.
        ///
        /// Trả về false nếu phiếu không còn ở đúng trạng thái expectedFrom
        /// (đã bị thao tác khác thay đổi trước đó) — Service tự quyết định
        /// báo lỗi "phiếu đã bị cập nhật bởi người khác" hay bỏ qua.
        /// </summary>
        /// <param name="id">Id PhieuXuLyBatThuong.</param>
        /// <param name="expectedFrom">Trạng thái hiện tại kỳ vọng — điều kiện WHERE.</param>
        /// <param name="lyDoHuy">Lý do hủy — bắt buộc không rỗng.</param>
        /// <param name="nguoiThucHien">Người thực hiện hủy.</param>
        /// <returns>true nếu cập nhật thành công; false nếu Status không khớp expectedFrom.</returns>
        bool UpdateLyDoHuy(
            int id,
            QTChungStatus expectedFrom,
            string lyDoHuy,
            string nguoiThucHien);

        // Thêm vào IPhieuXuLyBatThuongRepository, cạnh GetStatus/GetById
        /// <summary>
        /// Đếm số phiếu xử lý bất thường đang ở đúng 1 trạng thái QTChung — dùng cho dashboard.
        /// </summary>
        int CountByStatus(QTChungStatus status);
    }
}
