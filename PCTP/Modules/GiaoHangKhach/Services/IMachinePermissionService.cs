using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using System;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Quyền bắn QR của máy hiện tại TRÊN MỘT KHÁCH HÀNG cụ thể.
    /// Mỗi khách có tối đa một máy giữ quyền (FVN_QrReaderLock); khách khác nhau độc lập.
    /// Instance gắn với đúng một customerNo — Dispose để nhả khoá.
    /// </summary>
    public interface IMachinePermissionService : IDisposable
    {
        string CustomerNo { get; }

        /// <summary>Máy đang giữ quyền đọc QR của khách này, dạng "TÊN (IP)" (rỗng nếu không có).</summary>
        string OwnerMachine { get; }

        /// <summary>Máy này có được đăng ký đọc QR cho khách này không (sau lần GetCurrentRole đầu).</summary>
        bool IsRegistered { get; }

        /// <summary>
        /// Role của máy này với khách này. Lần gọi đầu tiên sẽ giành khoá (first-come);
        /// máy giành được => DuocBanQR, ngược lại => ChiXem.
        /// </summary>
        MachineRole GetCurrentRole();

        /// <summary>
        /// Kiểm tra bắt buộc ngay trước hành động ghi (bắn QR/CNK): xác nhận lại với DB rằng máy này
        /// vẫn là chủ khoá. Throw UnauthorizedAccessException nếu không (hoặc không xác nhận được).
        /// </summary>
        void EnsureCanBanQR();

        /// <summary>Phát ra (thread nền) khi heartbeat phát hiện máy khác đã chiếm quyền.</summary>
        event EventHandler LockLost;

        string GetTenBanTheoRole(CustomerConfig cfg, bool isSP, string tenBanView);
    }
}
