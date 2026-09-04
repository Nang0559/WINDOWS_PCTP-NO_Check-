using PCTP.Modules.XuLyHangLoi.Enums;
using PCTP.Modules.XuLyHangLoi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Repository
{
    public interface ITraHangQTChungRepository
    {

        //public QTChungStatus? GetStatus(int id)
        //=> GetStatus<QTChungStatus, int>("FVN_PhieuXuLyBatThuong", id);

        //public bool UpdateStatusIfCurrentIs(int id, QTChungStatus expectedFrom, QTChungStatus newStatus, string nguoiThucHien)
        //    => UpdateStatusIfCurrentIs("FVN_PhieuXuLyBatThuong", id, expectedFrom, newStatus, nguoiThucHien);
        // ============================================================
        // XUẤT KHO REWORK / GIAO BÙ NG
        // ============================================================

        /// <summary>
        /// Tạo một bản ghi xuất kho cho phiếu xử lý bất thường.
        /// 
        /// Loại xuất được xác định bởi TraHangQTChungXuat.LoaiXuat:
        /// - Rework
        /// - GiaoBuNG
        /// 
        /// Repository chỉ thực hiện ghi dữ liệu.
        /// Việc kiểm tra state machine và điều kiện nghiệp vụ
        /// phải được thực hiện tại Service.
        /// </summary>
        /// <param name="entity">
        /// Thông tin xuất kho.
        /// </param>
        /// <returns>
        /// Id của bản ghi xuất kho vừa tạo.
        /// </returns>
        int InsertXuat(
            TraHangQTChungXuat entity);

        /// <summary>
        /// Lấy toàn bộ các lần xuất kho của một phiếu xử lý bất thường.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Danh sách các bản ghi xuất kho.
        /// </returns>
        List<TraHangQTChungXuat> GetXuat(
            int phieuXuLyId);

        /// <summary>
        /// Lấy một bản ghi xuất kho theo Id.
        /// </summary>
        /// <param name="id">
        /// Id bản ghi xuất kho.
        /// </param>
        /// <returns>
        /// Bản ghi nếu tồn tại; ngược lại null.
        /// </returns>
        TraHangQTChungXuat GetXuatById(
            int id);


        // ============================================================
        // GIAO CHO SẢN XUẤT
        // ============================================================

        /// <summary>
        /// Tạo một bản ghi giao hàng cho bộ phận sản xuất/rework.
        /// 
        /// Repository không tự chuyển trạng thái QTChung.
        /// State transition phải được thực hiện tại Service.
        /// </summary>
        /// <param name="entity">
        /// Thông tin giao hàng.
        /// </param>
        /// <returns>
        /// Id của bản ghi giao vừa tạo.
        /// </returns>
        int InsertGiao(
            TraHangQTChungGiao entity);

        /// <summary>
        /// Lấy toàn bộ các lần giao cho sản xuất
        /// của một phiếu xử lý bất thường.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Danh sách các bản ghi giao.
        /// </returns>
        List<TraHangQTChungGiao> GetGiao(
            int phieuXuLyId);

        /// <summary>
        /// Lấy một bản ghi giao cho sản xuất theo Id.
        /// </summary>
        /// <param name="id">
        /// Id bản ghi giao.
        /// </param>
        /// <returns>
        /// Bản ghi nếu tồn tại; ngược lại null.
        /// </returns>
        TraHangQTChungGiao GetGiaoById(
            int id);


        // ============================================================
        // QC XÁC NHẬN CUỐI
        // ============================================================

        /// <summary>
        /// Tạo kết quả QC xác nhận cuối cho một phiếu xử lý bất thường.
        /// 
        /// Bản ghi QC là nguồn dữ liệu ghi nhận:
        /// - Số lượng đã rework
        /// - Số lượng OK
        /// - Số lượng NG
        /// - Kết luận QC
        /// - Người QC
        /// 
        /// Repository không tự quyết định state transition.
        /// </summary>
        /// <param name="entity">
        /// Kết quả QC xác nhận cuối.
        /// </param>
        /// <returns>
        /// Id bản ghi QC vừa tạo.
        /// </returns>
        int InsertQC(
            TraHangQTChungQC entity);

        /// <summary>
        /// Lấy kết quả QC xác nhận cuối của một phiếu xử lý bất thường.
        /// 
        /// Mỗi phiếu xử lý bất thường được kỳ vọng có tối đa
        /// một kết quả QC xác nhận cuối.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Kết quả QC nếu tồn tại; ngược lại null.
        /// </returns>
        TraHangQTChungQC GetQC(
            int phieuXuLyId);


        // ============================================================
        // NHẬP NG
        // ============================================================

        /// <summary>
        /// Tạo một bản ghi nhập hàng NG sau khi QC xác nhận.
        /// 
        /// Bản ghi này dùng để ghi nhận số lượng NG thực tế
        /// được nhập vào vị trí/slot tương ứng.
        /// </summary>
        /// <param name="entity">
        /// Thông tin nhập NG.
        /// </param>
        /// <returns>
        /// Id bản ghi nhập NG vừa tạo.
        /// </returns>
        int InsertNhapNG(
            TraHangQTChungNhapNG entity);

        /// <summary>
        /// Lấy toàn bộ các lần nhập NG của một phiếu xử lý bất thường.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Danh sách các bản ghi nhập NG.
        /// </returns>
        List<TraHangQTChungNhapNG> GetNhapNG(
            int phieuXuLyId);

        /// <summary>
        /// Lấy một bản ghi nhập NG theo Id.
        /// </summary>
        /// <param name="id">
        /// Id bản ghi nhập NG.
        /// </param>
        /// <returns>
        /// Bản ghi nếu tồn tại; ngược lại null.
        /// </returns>
        TraHangQTChungNhapNG GetNhapNGById(
            int id);


        // ============================================================
        // TIMELINE QTCHUNG
        // ============================================================

        /// <summary>
        /// Lấy timeline xử lý QTChung của một phiếu bất thường.
        /// 
        /// Timeline tổng hợp các bước nghiệp vụ:
        /// - XUAT
        /// - GIAO
        /// - QC
        /// - NHAP_NG
        /// 
        /// Repository chỉ tổng hợp dữ liệu để hiển thị/truy vấn,
        /// không thay đổi trạng thái nghiệp vụ.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Danh sách timeline theo thứ tự thời gian.
        /// </returns>
        List<QTChungTimelineItem> GetTimeline(
            int phieuXuLyId);


        // ============================================================
        // KIỂM TRA TIẾN ĐỘ
        // ============================================================

        /// <summary>
        /// Kiểm tra phiếu đã phát sinh xuất kho hay chưa.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// true nếu đã có ít nhất một bản ghi xuất kho;
        /// ngược lại false.
        /// </returns>
        bool DaXuatKho(
            int phieuXuLyId);

        /// <summary>
        /// Kiểm tra phiếu đã giao cho sản xuất hay chưa.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// true nếu đã có bản ghi giao sản xuất;
        /// ngược lại false.
        /// </returns>
        bool DaGiaoSanXuat(
            int phieuXuLyId);

        /// <summary>
        /// Kiểm tra phiếu đã có kết quả QC xác nhận cuối hay chưa.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// true nếu đã có kết quả QC;
        /// ngược lại false.
        /// </returns>
        bool DaQCXacNhan(
            int phieuXuLyId);

        /// <summary>
        /// Kiểm tra phiếu đã phát sinh nhập NG hay chưa.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// true nếu đã có bản ghi nhập NG;
        /// ngược lại false.
        /// </returns>
        bool DaNhapNG(
            int phieuXuLyId);


        // ============================================================
        // TỔNG HỢP SỐ LƯỢNG
        // ============================================================

        /// <summary>
        /// Cập nhật trạng thái kiểm tra tem của bản ghi QC.
        /// 
        /// DaKiemTraTem = true khi phiếu thuộc trường hợp cần
        /// kiểm tra tem và đã hoàn tất bước Inspection.
        /// </summary>
        /// <param name="qcId">
        /// Id bản ghi QC.
        /// </param>
        /// <param name="daKiemTra">
        /// true nếu đã kiểm tra tem; false nếu chưa.
        /// </param>
        void UpdateDaKiemTraTem(
            int qcId,
            bool daKiemTra);

        /// <summary>
        /// Tính tổng số lượng đã xuất kho của phiếu xử lý bất thường.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Tổng số lượng đã xuất.
        /// </returns>
        int GetTongSoLuongDaXuat(
            int phieuXuLyId);

        /// <summary>
        /// Tính tổng số lượng đã giao cho sản xuất.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Tổng số lượng đã giao.
        /// </returns>
        int GetTongSoLuongDaGiao(
            int phieuXuLyId);

        /// <summary>
        /// Lấy tổng số lượng hàng được QC xác nhận OK.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Tổng số lượng OK.
        /// </returns>
        int GetTongSoLuongOK(
            int phieuXuLyId);

        /// <summary>
        /// Lấy tổng số lượng hàng được QC xác nhận NG.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Tổng số lượng NG.
        /// </returns>
        int GetTongSoLuongNG(
            int phieuXuLyId);

        /// <summary>
        /// Lấy tổng số lượng NG đã thực tế nhập kho.
        /// </summary>
        /// <param name="phieuXuLyId">
        /// Id phiếu xử lý bất thường.
        /// </param>
        /// <returns>
        /// Tổng số lượng NG đã nhập.
        /// </returns>
        int GetTongSoLuongDaNhapNG(
            int phieuXuLyId);
    }
}
