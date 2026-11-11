using System;
using System.Collections.Generic;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>Trạng thái khoá đọc QR của một khách hàng. Định danh máy = IP do SQL Server tự đọc.</summary>
    public sealed class QrLockInfo
    {
        public string CustomerNo { get; set; }
        public string ScanMode { get; set; } = "MP";
        /// <summary>IP (server đọc) của máy đang giữ khoá. Rỗng nếu khách chưa có máy nào đọc.</summary>
        public string OwnerClientIp { get; set; }
        /// <summary>Tên máy do chủ khoá khai báo — CHỈ để hiển thị, không dùng phân quyền.</summary>
        public string OwnerMachine { get; set; }
        public string OwnerUser { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? HeartbeatAt { get; set; }
        /// <summary>Quá TTL không heartbeat — máy khác (đã đăng ký) có thể tiếp quản.</summary>
        public bool IsStale { get; set; }
        /// <summary>true nếu máy hiện tại (theo IP server thấy) đang là chủ khoá.</summary>
        public bool IsOwnedByMe { get; set; }
        /// <summary>IP máy này đã được đăng ký (FVN_QrReaderMachine) đọc QR cho khách này. false => luôn chỉ xem.</summary>
        public bool Registered { get; set; }
        /// <summary>Lỗi khi đọc/giành khoá (fail-closed); null nếu không lỗi.</summary>
        public string Error { get; set; }

        public bool HasOwner => !string.IsNullOrWhiteSpace(OwnerClientIp);

        /// <summary>"TÊN-MÁY (192.168.1.50)" hoặc chỉ IP nếu không có tên.</summary>
        public string OwnerDisplay
        {
            get
            {
                if (!HasOwner) return string.Empty;
                return string.IsNullOrWhiteSpace(OwnerMachine)
                    ? OwnerClientIp
                    : OwnerMachine + " (" + OwnerClientIp + ")";
            }
        }
    }

    /// <summary>
    /// Khoá "một máy đọc QR cho một khách hàng" (bảng dbo.FVN_QrReaderLock).
    /// Khác khách hàng => khoá độc lập => nhiều máy đọc song song.
    /// </summary>
    public interface ICustomerQrLockService
    {
        /// <summary>
        /// Giành/giữ khoá cho khách. Thành công khi khoá trống, đã thuộc máy này,
        /// hoặc đã hết hạn heartbeat. force=true: chiếm quyền (chỉ dùng sau khi người dùng xác nhận).
        /// Không bao giờ throw khi lỗi DB — trả IsOwnedByMe=false (fail-closed: chỉ xem).
        /// </summary>
        QrLockInfo TryAcquire(string customerNo, bool force = false);
        QrLockInfo TryAcquire(string customerNo, string scanMode, bool force = false);

        /// <summary>true nếu máy này vẫn là chủ khoá. Throw khi lỗi DB để caller tự quyết định.</summary>
        bool Heartbeat(string customerNo);
        bool Heartbeat(string customerNo, string scanMode);

        /// <summary>Nhả khoá — chỉ có tác dụng nếu máy này đang là chủ.</summary>
        void Release(string customerNo);
        void Release(string customerNo, string scanMode);
        bool IsRegistered(string customerNo);

        /// <summary>Trạng thái khoá của mọi khách đang có chủ.</summary>
        IList<QrLockInfo> ListAll();
    }
}
