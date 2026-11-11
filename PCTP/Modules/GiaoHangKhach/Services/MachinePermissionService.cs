using PCTP.ClassSQL;
using PCTP.Shared.Enums;
using PCTP.Shared.Models;
using System;
using System.Threading;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public class MachinePermissionService : IMachinePermissionService
    {
        private const int HeartbeatIntervalMs = 30000;

        private readonly ICustomerQrLockService _locks;
        private readonly string _customerNo;
        private string _scanMode = "MP";

        private MachineRole? _cachedRole;
        private string _owner = string.Empty;
        private bool _holdsLock;
        private bool _registered;
        private Timer _heartbeat;
        private readonly object _gate = new object();

        public event EventHandler LockLost = delegate { };

        public MachinePermissionService(SQLPROVIDER sql, string customerNo)
            : this(new CustomerQrLockService(sql), customerNo)
        {
        }

        public MachinePermissionService(ICustomerQrLockService locks, string customerNo)
        {
            _locks = locks ?? throw new ArgumentNullException(nameof(locks));
            _customerNo = CustomerTableConfig.NormalizeCustomerNo(customerNo);
        }

        public string CustomerNo => _customerNo;

        public string OwnerMachine
        {
            get { lock (_gate) return _owner; }
        }

        public void SetScanMode(string scanMode)
        {
            string mode = (scanMode ?? "MP").Trim().ToUpperInvariant();
            if (mode != "MP" && mode != "SP") throw new ArgumentException("Chế độ đọc QR chỉ nhận MP hoặc SP.", nameof(scanMode));
            bool release;
            string oldMode;
            lock (_gate)
            {
                if (_scanMode == mode) return;
                oldMode = _scanMode;
                release = _holdsLock;
                _heartbeat?.Dispose();
                _heartbeat = null;
                _holdsLock = false;
                _registered = false;
                _owner = string.Empty;
                _cachedRole = null;
                _scanMode = mode;
            }
            if (release) _locks.Release(_customerNo, oldMode);
        }

        public bool IsRegisteredForAnyMode() => _locks.IsRegistered(_customerNo);

        public void ReleaseScanLock()
        {
            string mode;
            lock (_gate)
            {
                // Máy chỉ-xem (không giữ khoá) không được đụng gì: tránh xoá cache/khoá của máy khác.
                if (!_holdsLock) return;
                mode = _scanMode;
                _heartbeat?.Dispose();
                _heartbeat = null;
                _holdsLock = false;
                _owner = string.Empty;
                _cachedRole = null; // lần dùng kế tiếp sẽ giành lại khoá
            }

            _locks.Release(_customerNo, mode);
        }

        public bool IsRegistered
        {
            get { lock (_gate) return _registered; }
        }

        public MachineRole GetCurrentRole()
        {
            lock (_gate)
            {
                if (_cachedRole.HasValue) return _cachedRole.Value;

                QrLockInfo info = _locks.TryAcquire(_customerNo, _scanMode);
                _owner = info.HasOwner ? info.OwnerDisplay : (info.Error ?? string.Empty);
                _holdsLock = info.IsOwnedByMe;
                _registered = info.Registered;
                MachineRole role = info.IsOwnedByMe ? MachineRole.DuocBanQR : MachineRole.ChiXem;

                if (_holdsLock && _heartbeat == null)
                    _heartbeat = new Timer(OnHeartbeat, _scanMode, HeartbeatIntervalMs, HeartbeatIntervalMs);

                // Chỉ cache khi đã giành được khoá, hoặc máy chưa đăng ký (kết quả không đổi cho tới khi
                // admin đăng ký). Máy đã đăng ký nhưng khoá đang do máy khác giữ / lỗi DB thì KHÔNG cache,
                // để khi máy kia nhả khoá (kết thúc phiên) thì lần mở DOC QRCODE kế tiếp giành lại được.
                if (info.IsOwnedByMe || (!info.Registered && info.Error == null))
                    _cachedRole = role;

                return role;
            }
        }

        public void EnsureCanBanQR()
        {
            if (GetCurrentRole() != MachineRole.DuocBanQR)
                throw new UnauthorizedAccessException(IsRegistered
                    ? $"Khách {_customerNo} chế độ {_scanMode} đang được máy [{OwnerMachine}] đọc QR. Máy [{Environment.MachineName}] chỉ được xem."
                    : $"Máy [{Environment.MachineName}] chưa được đăng ký đọc QR cho khách {_customerNo} chế độ {_scanMode}.");

            // Xác nhận lại với DB ngay lúc ghi: chặn trường hợp máy khác vừa chiếm quyền
            // trong khoảng giữa hai lần heartbeat. Lỗi DB => fail-closed.
            bool stillOwner;
            try
            {
                stillOwner = _locks.Heartbeat(_customerNo, _scanMode);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException(
                    $"Không xác nhận được quyền bắn QR của khách {_customerNo} (lỗi kết nối DB): {ex.Message}");
            }

            if (!stillOwner)
            {
                MarkLost();
                throw new UnauthorizedAccessException(
                    $"Máy [{Environment.MachineName}] đã mất quyền bắn QR của khách {_customerNo} " +
                    "(máy khác đã chiếm quyền hoặc khoá đã hết hạn). Hãy đóng và mở lại màn hình giao hàng.");
            }
        }

        public string GetTenBanTheoRole(CustomerConfig cfg, bool isSP, string tenBanView)
        {
            SetScanMode(isSP ? "SP" : "MP");
            if (GetCurrentRole() == MachineRole.DuocBanQR)
            {
                string loai = isSP ? "SP" : "MP";
                return $"Bàn bắn QR — {cfg?.DisplayName} ({loai})";
            }

            return $"{tenBanView} (chỉ xem — máy {OwnerMachine} đang đọc QR)";
        }

        public void Dispose()
        {
            bool release;
            lock (_gate)
            {
                _heartbeat?.Dispose();
                _heartbeat = null;
                release = _holdsLock;
                _holdsLock = false;
                _cachedRole = MachineRole.ChiXem;
            }

            // Chỉ nhả khi chính instance này đã giành khoá — form chỉ-xem đóng lại không được
            // đụng vào khoá của máy khác (và Release ở DB cũng chỉ xoá khi đúng MachineName).
            if (release)
                _locks.Release(_customerNo, _scanMode);
        }

        private void OnHeartbeat(object state)
        {
            string mode = Convert.ToString(state);
            lock (_gate)
            {
                if (!_holdsLock || !string.Equals(mode, _scanMode, StringComparison.OrdinalIgnoreCase)) return;
            }
            try
            {
                if (!_locks.Heartbeat(_customerNo, mode))
                    MarkLost();
            }
            catch
            {
                // Lỗi mạng thoáng qua: giữ nguyên. Nếu quá TTL, máy khác sẽ tiếp quản và
                // EnsureCanBanQR() sẽ chặn lần ghi kế tiếp.
            }
        }

        private void MarkLost()
        {
            bool raise;
            lock (_gate)
            {
                raise = _holdsLock;
                _holdsLock = false;
                _cachedRole = MachineRole.ChiXem;
                _heartbeat?.Dispose();
                _heartbeat = null;
            }

            if (raise)
            {
                try { LockLost(this, EventArgs.Empty); } catch { }
            }
        }
    }
}
