using PCTP.ClassSQL;
using PCTP.Shared.Models;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Net;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    /// <summary>
    /// Quản lý danh sách máy được đăng ký đọc QR (dbo.FVN_QrReaderMachine) và thao tác admin trên khoá.
    /// Định danh máy = IP do SQL Server tự đọc (CONNECTIONPROPERTY('client_net_address')); tên máy chỉ để hiển thị.
    /// Danh sách khách hàng KHÔNG hard-code: lấy từ CustomerTableConfig; "*" = mọi khách (kể cả khách thêm sau).
    /// Lỗi DB được ném ra cho UI hiển thị (khác với CustomerQrLockService luôn fail-closed).
    /// </summary>
    public sealed class QrReaderMachineService
    {
        public const string AllCustomers = "*";

        /// <summary>Giá trị SQL Server trả về khi app chạy cùng máy với SQL Server (shared memory).</summary>
        public const string LocalMachineIp = "<local machine>";

        private readonly SQLPROVIDER _sql;

        public QrReaderMachineService(SQLPROVIDER sql)
        {
            _sql = sql ?? throw new ArgumentNullException(nameof(sql));
        }

        /// <summary>"*" hoặc customerNo đã cấu hình Delivery trong CustomerTableConfig; ném lỗi nếu khác.</summary>
        public static string NormalizeScope(string customerNo)
        {
            string s = (customerNo ?? string.Empty).Trim();
            if (s == AllCustomers) return AllCustomers;
            return CustomerTableConfig.GetForDelivery(s).CustomerNo;
        }

        /// <summary>IP hợp lệ (IPv4/IPv6) hoặc "&lt;local machine&gt;"; chuẩn hoá để so khớp với giá trị server trả về.</summary>
        public static string NormalizeIp(string ip)
        {
            string s = (ip ?? string.Empty).Trim();
            if (string.Equals(s, LocalMachineIp, StringComparison.OrdinalIgnoreCase)) return LocalMachineIp;
            IPAddress parsed;
            if (s.Length == 0 || !IPAddress.TryParse(s, out parsed))
                throw new ArgumentException("IP máy không hợp lệ: \"" + s + "\" (ví dụ 192.168.1.50).", nameof(ip));
            return parsed.ToString();
        }

        /// <summary>IP mà SERVER thấy cho kết nối của máy này (null nếu server không xác định được).</summary>
        public string GetMyClientIp()
        {
            DataTable dt = Run("dbo.Usp_QrReaderMachine_WhoAmI");
            if (dt.Rows.Count == 0 || dt.Rows[0]["ClientIp"] == DBNull.Value) return null;
            string ip = Convert.ToString(dt.Rows[0]["ClientIp"]);
            return string.IsNullOrWhiteSpace(ip) ? null : ip.Trim();
        }

        public DataTable ListRegistrations()
        {
            return Run("dbo.Usp_QrReaderMachine_List");
        }

        public void Upsert(string customerNo, string clientIp, string machineName, bool isActive, string note)
            => Upsert(customerNo, "MP", clientIp, machineName, isActive, note);

        public void Upsert(string customerNo, string scanMode, string clientIp, string machineName, bool isActive, string note)
        {
            Run("dbo.Usp_QrReaderMachine_Upsert",
                new SqlParameter("@CustomerNo", NormalizeScope(customerNo)),
                new SqlParameter("@ScanMode", NormalizeMode(scanMode)),
                new SqlParameter("@ClientIp", NormalizeIp(clientIp)),
                new SqlParameter("@MachineName", Nullable(machineName, 100)),
                new SqlParameter("@IsActive", isActive),
                new SqlParameter("@Note", Nullable(note, 200)));
        }

        public void Remove(string customerNo, string clientIp) => Remove(customerNo, "MP", clientIp);

        public void Remove(string customerNo, string scanMode, string clientIp)
        {
            Run("dbo.Usp_QrReaderMachine_Remove",
                new SqlParameter("@CustomerNo", NormalizeScope(customerNo)),
                new SqlParameter("@ScanMode", NormalizeMode(scanMode)),
                new SqlParameter("@ClientIp", NormalizeIp(clientIp)));
        }

        /// <summary>Admin nhả khoá của một khách. Máy đang giữ mất quyền ở lần quét kế tiếp.</summary>
        public void ForceRelease(string customerNo) => ForceRelease(customerNo, "MP");

        public void ForceRelease(string customerNo, string scanMode)
        {
            Run("dbo.Usp_QrReaderLock_ForceRelease",
                new SqlParameter("@CustomerNo", CustomerTableConfig.GetForDelivery(customerNo).CustomerNo),
                new SqlParameter("@ScanMode", NormalizeMode(scanMode)));
        }

        private static string NormalizeMode(string mode)
        {
            string value = (mode ?? "MP").Trim().ToUpperInvariant();
            if (value != "MP" && value != "SP") throw new ArgumentException("Chế độ đọc QR chỉ nhận MP hoặc SP.", nameof(mode));
            return value;
        }

        private static object Nullable(string value, int max)
        {
            string s = (value ?? string.Empty).Trim();
            if (s.Length == 0) return DBNull.Value;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private DataTable Run(string procName, params SqlParameter[] parameters)
        {
            using (var conn = new SqlConnection(_sql.B7R2_FCCdb))
            using (var cmd = new SqlCommand(procName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 15;
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                conn.Open();
                var dt = new DataTable();
                using (var reader = cmd.ExecuteReader())
                    dt.Load(reader);
                return dt;
            }
        }
    }
}
