using PCTP.Modules.XuLyHangLoi.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.XuLyHangLoi.Models
{
    public static class QTChungStatusTransition
    {
        private static readonly Dictionary<QTChungStatus, QTChungStatus[]> Map =
            new Dictionary<QTChungStatus, QTChungStatus[]>
            {
                // ============================================================
                // 0. MỚI
                // Tạo phiếu xong -> chờ QC định hướng
                // ============================================================
                [QTChungStatus.Moi] = new[]
                {
                QTChungStatus.ChoQCDinhHuong,
                QTChungStatus.Huy
                },

                // ============================================================
                // 10. CHỜ QC ĐỊNH HƯỚNG
                // QC định hướng xong -> chờ xuất kho rework
                // ============================================================
                [QTChungStatus.ChoQCDinhHuong] = new[]
                {
                QTChungStatus.ChoXuatKhoRework,
                QTChungStatus.Huy
                },

                // ============================================================
                // 20. CHỜ XUẤT KHO REWORK
                // Xuất kho -> Đã xuất kho
                //
                // Cho phép giữ nguyên trạng thái vì có thể scan/xử lý nhiều lần.
                // ============================================================
                [QTChungStatus.ChoXuatKhoRework] = new[]
                {
                QTChungStatus.ChoXuatKhoRework,
                QTChungStatus.DaXuatKhoRework,
                QTChungStatus.Huy
                },

                // ============================================================
                // 30. ĐÃ XUẤT KHO REWORK
                // Có thể:
                //   - quay lại Chờ xuất kho nếu còn xử lý/xuất bổ sung
                //   - chuyển sang Chờ giao sản xuất
                //   - Hủy
                // ============================================================
                [QTChungStatus.DaXuatKhoRework] = new[]
                {
                QTChungStatus.ChoXuatKhoRework,
                QTChungStatus.ChoGiaoSanXuat,
                QTChungStatus.Huy
                },

                // ============================================================
                // 40. CHỜ GIAO SẢN XUẤT
                // Giao hàng -> Đã giao sản xuất
                // ============================================================
                [QTChungStatus.ChoGiaoSanXuat] = new[]
                {
                QTChungStatus.ChoGiaoSanXuat,
                QTChungStatus.DaGiaoSanXuat,
                QTChungStatus.Huy
                },

                // ============================================================
                // 50. ĐÃ GIAO SẢN XUẤT
                // Sản xuất nhận hàng và bắt đầu rework
                // ============================================================
                [QTChungStatus.DaGiaoSanXuat] = new[]
                {
                QTChungStatus.DangRework,
                QTChungStatus.Huy
                },

                // ============================================================
                // 60. ĐANG REWORK
                // Sản xuất rework xong -> chờ QC xác nhận cuối
                // ============================================================
                [QTChungStatus.DangRework] = new[]
                {
                QTChungStatus.DangRework,
                QTChungStatus.ChoQCXacNhanCuoi,
                QTChungStatus.Huy
                },

                // ============================================================
                // 70. CHỜ QC XÁC NHẬN CUỐI
                // QC xác nhận -> QCDaXacNhan
                // ============================================================
                [QTChungStatus.ChoQCXacNhanCuoi] = new[]
                {
                QTChungStatus.ChoQCXacNhanCuoi,
                QTChungStatus.QCDaXacNhan,
                QTChungStatus.Huy
                },

                // ============================================================
                // 80. QC ĐÃ XÁC NHẬN
                //
                // Nếu OK toàn bộ:
                //      -> HoanTat
                //
                // Nếu có NG:
                //      -> DaNhapNG
                //
                // Hủy vẫn được phép nếu nghiệp vụ cho phép.
                // ============================================================
                [QTChungStatus.QCDaXacNhan] = new[]
                {
                QTChungStatus.DaNhapNG,
                QTChungStatus.HoanTat,
                QTChungStatus.Huy
                },

                // ============================================================
                // 90. ĐÃ NHẬP NG
                // Nhập đủ hàng NG -> Hoàn tất
                // ============================================================
                [QTChungStatus.DaNhapNG] = new[]
                {
                QTChungStatus.DaNhapNG,
                QTChungStatus.HoanTat,
                QTChungStatus.Huy
                },

                // ============================================================
                // 100. HOÀN TẤT
                // Terminal state - không được chuyển tiếp.
                // IsValid() vẫn cho phép from == to.
                // ============================================================
                [QTChungStatus.HoanTat] = Array.Empty<QTChungStatus>(),

                // ============================================================
                // 900. HỦY
                // Terminal state - không được chuyển tiếp.
                // ============================================================
                [QTChungStatus.Huy] = Array.Empty<QTChungStatus>()
            };

        /// <summary>
        /// Kiểm tra transition từ trạng thái hiện tại sang trạng thái mới.
        /// Cho phép from == to để hỗ trợ các thao tác idempotent.
        /// </summary>
        public static bool IsValid(
            QTChungStatus from,
            QTChungStatus to)
        {
            // Giữ nguyên trạng thái là hợp lệ.
            if (from == to)
                return true;

            return Map.TryGetValue(from, out var allowed)
                && allowed.Contains(to);
        }

        /// <summary>
        /// Lấy danh sách trạng thái có thể chuyển tới từ trạng thái hiện tại.
        /// </summary>
        public static IReadOnlyCollection<QTChungStatus> GetAllowedTransitions(
            QTChungStatus from)
        {
            if (!Map.TryGetValue(from, out var allowed))
                return Array.Empty<QTChungStatus>();

            return allowed;
        }
    }
}
