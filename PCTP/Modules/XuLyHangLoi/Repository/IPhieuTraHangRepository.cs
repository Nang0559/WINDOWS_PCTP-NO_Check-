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
    /// Repository quản lý dữ liệu phiếu trả hàng ở cấp Header và Detail.
    ///
    /// Trách nhiệm chính:
    /// - CRUD PhieuTraHang.
    /// - CRUD PhieuTraHangCT.
    /// - Tra cứu dữ liệu phục vụ quy trình xử lý.
    /// - Cập nhật trạng thái Header.
    /// - Cập nhật thông tin nghiệp vụ giao lại bộ phận đối với TraNoiBo.
    ///
    /// QUAN TRỌNG:
    /// Repository chỉ chịu trách nhiệm đọc/ghi dữ liệu.
    ///
    /// Repository KHÔNG quyết định state transition có hợp lệ hay không.
    /// Việc kiểm tra transition phải được thực hiện ở Service bằng:
    ///     PhieuTraHangStatusTransition
    ///
    /// Ví dụ:
    ///     Service kiểm tra:
    ///         IsValidTransition(nguon, currentStatus, newStatus)
    ///
    ///     Sau khi hợp lệ mới gọi:
    ///         UpdateStatus(...)
    ///
    /// Các thao tác liên quan nhiều bảng hoặc nhiều bản ghi phải được
    /// Service thực hiện trong transaction phù hợp.
    /// </summary>
    /// 

    public interface IPhieuTraHangRepository
    {

        PhieuTraHangStatus? GetStatus(int id);
        bool UpdateStatusIfCurrentIs(int id, PhieuTraHangStatus expectedFrom, PhieuTraHangStatus newStatus, string nguoiThucHien);
        // ============================================================
        // HEADER - PHIEU TRA HANG
        // ============================================================

        /// <summary>
        /// Tạo mới một PhieuTraHang.
        ///
        /// Status ban đầu thông thường là:
        ///     PhieuTraHangStatus.Moi
        ///
        /// Repository chỉ chịu trách nhiệm ghi dữ liệu.
        /// Việc kiểm tra nghiệp vụ và state transition thuộc Service.
        /// </summary>
        int Insert(
            PhieuTraHang e);


        /// <summary>
        /// Lấy PhieuTraHang theo Id.
        ///
        /// Trả về null nếu không tồn tại.
        /// </summary>
        PhieuTraHang GetById(
            int id);


        /// <summary>
        /// Lấy PhieuTraHang theo số phiếu nội bộ SoPhieu.
        ///
        /// SoPhieu là định danh nghiệp vụ của Header.
        /// </summary>
        PhieuTraHang GetBySoPhieu(
            string soPhieu);


        /// <summary>
        /// Lấy danh sách PhieuTraHang theo nguồn phát sinh.
        ///
        /// Nguon:
        ///     - KhachTra
        ///     - TraNoiBo
        /// </summary>
        List<PhieuTraHang> GetByNguon(
            NguonXuLyBatThuong nguon);


        /// <summary>
        /// Lấy các PhieuTraHang chưa hoàn tất.
        ///
        /// Nguồn sự thật về trạng thái là:
        ///     PhieuTraHang.Status
        ///
        /// Không dựa trên các cờ boolean trạng thái cũ.
        /// </summary>
        List<PhieuTraHang> GetChoXuLy();


        /// <summary>
        /// Lấy các PhieuTraHang chưa hoàn tất theo nguồn.
        ///
        /// Nguon:
        ///     - KhachTra
        ///     - TraNoiBo
        /// </summary>
        List<PhieuTraHang> GetChoXuLyByNguon(
            NguonXuLyBatThuong nguon);


        /// <summary>
        /// Cập nhật thông tin của PhieuTraHang.
        ///
        /// Không dùng method này để thực hiện state transition.
        /// Khi thay đổi Status phải sử dụng UpdateStatus().
        /// </summary>
        void Update(
            PhieuTraHang e);


        /// <summary>
        /// Cập nhật Status của PhieuTraHang.
        ///
        /// Repository chỉ thực hiện persistence.
        /// Service phải validate transition trước khi gọi method này
        /// thông qua PhieuTraHangStatusTransition.
        ///
        /// Status là nguồn sự thật duy nhất của state machine cấp Header.
        /// </summary>
        void UpdateStatus(
            int id,
            PhieuTraHangStatus status,
            string nguoiThucHien);


        /// <summary>
        /// Cập nhật Note của PhieuTraHang.
        ///
        /// Không thay đổi Status.
        /// </summary>
        void UpdateNote(
            int id,
            string note,
            string nguoiThucHien);


        // ============================================================
        // DETAIL - PHIEU TRA HANG CT
        // ============================================================

        /// <summary>
        /// Tạo một dòng PhieuTraHangCT.
        /// </summary>
        int InsertItem(
            PhieuTraHangCT item);


        /// <summary>
        /// Tạo nhiều dòng PhieuTraHangCT thuộc cùng một PhieuTraHang.
        ///
        /// Thường được sử dụng trong transaction khi tạo Header
        /// và các dòng chi tiết.
        /// </summary>
        void InsertItems(
            int phieuTraHangId,
            IEnumerable<PhieuTraHangCT> items);


        /// <summary>
        /// Lấy toàn bộ dòng PhieuTraHangCT thuộc một PhieuTraHang.
        /// </summary>
        List<PhieuTraHangCT> GetItems(
            int phieuTraHangId);


        /// <summary>
        /// Lấy một dòng PhieuTraHangCT theo Id.
        ///
        /// Trả về null nếu không tồn tại.
        /// </summary>
        PhieuTraHangCT GetItemById(
            int itemId);


        // ============================================================
        // TRA NOI BO - GIAO LAI BO PHAN
        // ============================================================

        /// <summary>
        /// Cập nhật thông tin nghiệp vụ của việc giao lại bộ phận.
        ///
        /// Chỉ áp dụng cho Nguon = TraNoiBo.
        ///
        /// Cập nhật các thông tin:
        ///     - BoPhanNhanLai
        ///     - SoLuongGiaoLai
        ///     - NgayGiaoLaiBoPhan
        ///     - NguoiGiaoLaiBoPhan
        ///     - UpdatedAt
        ///     - UpdatedBy
        ///
        /// Method này không tự quyết định transition.
        /// Service phải gọi UpdateStatus() riêng để chuyển:
        ///
        ///     ChoGiaoLaiBoPhan
        ///         ->
        ///     DaGiaoLaiBoPhan
        /// </summary>
        void UpdateThongTinGiaoLaiBoPhan(
            int phieuTraHangId,
            string boPhanNhan,
            int soLuongGiaoLai,
            DateTime ngayGiaoLai,
            string nguoiThucHien);


        // ============================================================
        // DOI CHIEU PHIEU GIAO
        // ============================================================

        /// <summary>
        /// Lấy các dòng PhieuTraHangCT chưa xác định được phiếu giao gốc.
        ///
        /// Dòng chưa xác định là dòng chưa có DinhDanhPhieuGiao.
        ///
        /// Dữ liệu này được Service sử dụng để tìm ứng viên
        /// thông qua IPhieuGiaoRepository.
        /// </summary>
        List<PhieuTraHangCT> GetItemsChuaXacDinhPhieuGiao(
            int phieuTraHangId);


        /// <summary>
        /// Cập nhật thông tin phiếu giao gốc đã xác định cho một dòng hàng.
        ///
        /// Cập nhật:
        ///     - DinhDanhPhieuGiao
        ///     - PoNo
        ///     - NgayGiao
        ///     - NhaMay
        ///
        /// Không thay đổi Status của PhieuTraHang.
        /// </summary>
        void UpdateItemDinhDanhPhieuGiao(
            int itemId,
            string dinhDanhPhieuGiao,
            string poNo,
            DateTime? ngayGiao,
            string nhaMay);

        bool ConChoXuLy(int phieuTraHangId); // true nếu còn ≥1 PhieuXuLyBatThuong con chưa HoanTat/Huy
    }
}
