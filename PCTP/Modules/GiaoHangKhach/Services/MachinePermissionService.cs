using PCTP.ClassSQL;
using PCTP.Shared.Enums;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public class MachinePermissionService : IMachinePermissionService
    {
        private readonly SQLPROVIDER _sql;

        // Cache trong phiên làm việc — tránh query lại DB mỗi lần gọi liên tục
        // trong cùng 1 thao tác UI (ví dụ vừa check quyền vừa lấy tên bàn).
        private MachineRole? _cachedRole;
        private string _cachedTenMayBanQR;

        public MachinePermissionService(SQLPROVIDER sql) => _sql = sql;

        public MachineRole GetCurrentRole()
        {
            if (_cachedRole.HasValue) return _cachedRole.Value;

            _cachedTenMayBanQR = LayTenMayDangBanQR();
            _cachedRole = string.Equals(
                Environment.MachineName,
                _cachedTenMayBanQR,
                StringComparison.OrdinalIgnoreCase)
                ? MachineRole.DuocBanQR
                : MachineRole.ChiXem;

            return _cachedRole.Value;
        }

        public void EnsureCanBanQR()
        {
            if (GetCurrentRole() != MachineRole.DuocBanQR)
                throw new UnauthorizedAccessException(
                    $"Máy [{Environment.MachineName}] không có quyền bắn QR. " +
                    $"Máy đang được cấp quyền: [{_cachedTenMayBanQR}].");
        }

        public string GetTenBanTheoRole(CustomerConfig cfg, bool isSP, string tenBanView)
        {
            var role = GetCurrentRole();
            if (role == MachineRole.DuocBanQR)
            {
                // Máy bắn QR: hiển thị tên bàn theo khách hàng + loại SP (bán thành phẩm/thành phẩm)
                // ⚠ Giả định cfg có property tên khách hàng hiển thị — cần xác nhận đúng tên field
                // thật trong CustomerConfig (đoán là cfg.TenKhachHang hoặc cfg.CustomerName).
                string loai = isSP ? "SP" : "BTP";
                return $"Bàn bắn QR — {cfg?.DisplayName} ({loai})";
            }

            // Máy chỉ xem: dùng tên bàn truyền vào, gắn nhãn rõ ràng để người dùng
            // không nhầm tưởng mình có quyền bắn.
            return $"{tenBanView} (chỉ xem)";
        }

        public void ChuyenMayBan(string tenMayMoi)
        {
            if (string.IsNullOrWhiteSpace(tenMayMoi))
                throw new ArgumentException("Tên máy mới không được để trống.", nameof(tenMayMoi));

            string tenMayCu = LayTenMayDangBanQR();
            string ghiChu = $"{DateTime.Now:dd/MM/yyyy HH:mm} — {Environment.UserName} đổi từ [{tenMayCu}] sang [{tenMayMoi}]";

            // Cắt về đúng 100 ký tự để khớp nvarchar(100), tránh SqlException truncate.
            if (ghiChu.Length > 100)
                ghiChu = ghiChu.Substring(0, 100);

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    // 1) Tắt cờ TT của máy đang giữ quyền (nếu có)
                    _sql.ExecuteNonQuery(conn, tran,
                        "UPDATE tbl_QR_MAY_DOCQR SET TT = 0 WHERE TT = 1");

                    // 2) Bật cờ cho máy mới — nếu đã tồn tại dòng thì UPDATE,
                    //    chưa có thì INSERT (upsert đơn giản bằng kiểm tra tồn tại trước).
                    object existsObj = _sql.ExecuteScalar(conn, tran,
                        "SELECT COUNT(*) FROM tbl_QR_MAY_DOCQR WHERE TenMay = @ten",
                        new[] { new SqlParameter("@ten", tenMayMoi) });
                    bool exists = Convert.ToInt32(existsObj) > 0;

                    if (exists)
                    {
                        _sql.ExecuteNonQuery(conn, tran,
                            "UPDATE tbl_QR_MAY_DOCQR SET TT = 1, LichSu = @lichSu WHERE TenMay = @ten",
                            new SqlParameter("@lichSu", ghiChu),
                            new SqlParameter("@ten", tenMayMoi));
                    }
                    else
                    {
                        _sql.ExecuteNonQuery(conn, tran,
                            "INSERT INTO tbl_QR_MAY_DOCQR (TenMay, LichSu, TT) VALUES (@ten, @lichSu, 1)",
                            new SqlParameter("@ten", tenMayMoi),
                            new SqlParameter("@lichSu", ghiChu));
                    }

                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }
            }

            // Xoá cache — lần gọi GetCurrentRole() tiếp theo sẽ đọc lại DB.
            _cachedRole = null;
            _cachedTenMayBanQR = null;
        }

        /// <summary>Đọc tên máy đang có TT=1. Trả về "" nếu chưa có máy nào được cấp quyền.</summary>
        private string LayTenMayDangBanQR()
        {
            object kq = _sql.ExecuteScalar(_sql.B7R2_FCCdb,
                "SELECT TenMay FROM tbl_QR_MAY_DOCQR WHERE TT = 1");
            return kq?.ToString() ?? "";
        }
    }
}
