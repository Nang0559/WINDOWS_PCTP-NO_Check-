using System;
using System.Data;

namespace PCTP.Security
{
    public sealed class AuthenticationService
    {
        private readonly AuthenticationRepository _repository;
        public AuthenticationService(AuthenticationRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public bool HasUsers() { return _repository.HasUsers(); }

        public UserSession Authenticate(string userName, string password)
        {
            userName = (userName ?? string.Empty).Trim();
            if (userName.Length == 0 || string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Vui lòng nhập tên đăng nhập và mật khẩu.");

            DataRow row = _repository.GetCredential(userName);
            if (row == null)
            {
                // Reduce username-enumeration timing differences.
                PasswordHasher.Hash(password, new byte[PasswordHasher.SaltSize], PasswordHasher.DefaultIterations);
                _repository.RecordLogin(null, userName, false, "INVALID_CREDENTIALS");
                throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");
            }

            long userId = Convert.ToInt64(row["UserId"]);
            if (!Convert.ToBoolean(row["IsActive"]))
            {
                _repository.RecordLogin(userId, userName, false, "INACTIVE");
                throw new UnauthorizedAccessException("Tài khoản đã bị khóa hoặc ngừng hoạt động.");
            }

            DateTime? lockedUntil = row["LockedUntilUtc"] == DBNull.Value
                ? (DateTime?)null : Convert.ToDateTime(row["LockedUntilUtc"]);
            if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
                throw new UnauthorizedAccessException("Tài khoản đang tạm khóa đến " + lockedUntil.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + ".");

            bool valid = PasswordHasher.Verify(password, (byte[])row["PasswordSalt"],
                (byte[])row["PasswordHash"], Convert.ToInt32(row["PasswordIterations"]));
            if (!valid)
            {
                _repository.RecordLogin(userId, userName, false, "INVALID_CREDENTIALS");
                throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");
            }

            _repository.RecordLogin(userId, userName, true, null);
            return _repository.LoadSession(row);
        }

        public UserSession InitializeFirstAdmin(string userName, string displayName, string password)
        {
            PasswordHasher.ValidatePolicy(password);
            if (string.IsNullOrWhiteSpace(userName) || userName.Trim().Length < 3)
                throw new InvalidOperationException("Tên đăng nhập phải có ít nhất 3 ký tự.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("Vui lòng nhập họ tên hiển thị.");

            byte[] salt = PasswordHasher.CreateSalt();
            byte[] hash = PasswordHasher.Hash(password, salt);
            long userId = _repository.InitializeFirstAdmin(userName.Trim(), displayName.Trim(),
                salt, hash, PasswordHasher.DefaultIterations);
            DataRow row = _repository.GetCredential(userName);
            return _repository.LoadSession(row);
        }

        public void ChangePassword(UserSession session, string currentPassword, string newPassword)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            PasswordHasher.ValidatePolicy(newPassword);
            byte[] currentHash = null;
            bool firstPassword = session.MustChangePassword;
            if (!firstPassword)
            {
                DataRow row = _repository.GetCredential(session.UserName);
                if (row == null) throw new UnauthorizedAccessException("Không tìm thấy tài khoản.");
                currentHash = PasswordHasher.Hash(currentPassword ?? string.Empty,
                    (byte[])row["PasswordSalt"], Convert.ToInt32(row["PasswordIterations"]));
            }
            byte[] salt = PasswordHasher.CreateSalt();
            byte[] hash = PasswordHasher.Hash(newPassword, salt);
            _repository.ChangePassword(session.UserId, currentHash, salt, hash,
                PasswordHasher.DefaultIterations, firstPassword);
        }
    }
}