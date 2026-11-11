using PCTP.ClassSQL;
using PCTP.Shared.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public sealed class CustomerQrLockService : ICustomerQrLockService
    {
        /// <summary>Quá thời gian này không heartbeat thì khoá hết hạn. Heartbeat gửi mỗi ~30s.</summary>
        public const int TtlSeconds = 120;

        private readonly SQLPROVIDER _sql;
        private readonly string _machine;

        public CustomerQrLockService(SQLPROVIDER sql)
        {
            _sql = sql ?? throw new ArgumentNullException(nameof(sql));
            _machine = Environment.MachineName;
        }

        public QrLockInfo TryAcquire(string customerNo, bool force = false) => TryAcquire(customerNo, "MP", force);

        public QrLockInfo TryAcquire(string customerNo, string scanMode, bool force = false)
        {
            string key = CustomerTableConfig.NormalizeCustomerNo(customerNo);
            string mode = (scanMode ?? "MP").Trim().ToUpperInvariant();
            try
            {
                mode = NormalizeMode(scanMode); // chế độ sai => fail-closed, không throw ra ngoài
                DataTable dt = Query(
                    "dbo.Usp_QrReaderLock_Acquire",
                    new SqlParameter("@CustomerNo", key),
                    new SqlParameter("@ScanMode", mode),
                    new SqlParameter("@MachineName", _machine), // chỉ để hiển thị; định danh là IP server đọc
                    new SqlParameter("@UserName", Environment.UserName),
                    new SqlParameter("@TtlSeconds", TtlSeconds),
                    new SqlParameter("@Force", force));

                if (dt.Rows.Count == 0)
                    return NoOwner(key, mode);

                return Map(dt.Rows[0], key, mode);
            }
            catch (Exception ex)
            {
                // Fail-closed: lỗi DB => KHÔNG được coi là chủ khoá.
                System.Diagnostics.Debug.WriteLine("[QrLock.TryAcquire] " + ex.Message);
                return new QrLockInfo
                {
                    CustomerNo = key,
                    ScanMode = mode,
                    Error = "không đọc được trạng thái khoá: " + ex.Message,
                    IsOwnedByMe = false
                };
            }
        }

        public bool Heartbeat(string customerNo) => Heartbeat(customerNo, "MP");

        public bool Heartbeat(string customerNo, string scanMode)
        {
            string key = CustomerTableConfig.NormalizeCustomerNo(customerNo);
            DataTable dt = Query(
                "dbo.Usp_QrReaderLock_Heartbeat",
                new SqlParameter("@CustomerNo", key),
                new SqlParameter("@ScanMode", NormalizeMode(scanMode)));
            return dt.Rows.Count > 0 && Convert.ToBoolean(dt.Rows[0]["Owned"]);
        }

        public void Release(string customerNo) => Release(customerNo, "MP");

        public void Release(string customerNo, string scanMode)
        {
            string key = CustomerTableConfig.NormalizeCustomerNo(customerNo);
            try
            {
                Query(
                    "dbo.Usp_QrReaderLock_Release",
                    new SqlParameter("@CustomerNo", key),
                    new SqlParameter("@ScanMode", NormalizeMode(scanMode)));
            }
            catch (Exception ex)
            {
                // Không nhả được thì TTL sẽ tự hết hạn — không làm hỏng việc đóng form.
                System.Diagnostics.Debug.WriteLine("[QrLock.Release] " + ex.Message);
            }
        }

        public bool IsRegistered(string customerNo)
        {
            string key = CustomerTableConfig.NormalizeCustomerNo(customerNo);
            try
            {
                DataTable dt = Query(
                    "dbo.Usp_QrReaderMachine_CanReadAnyMode",
                    new SqlParameter("@CustomerNo", key));
                return dt.Rows.Count > 0 && Convert.ToBoolean(dt.Rows[0]["Registered"]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[QrLock.IsRegistered] " + ex.Message);
                return false;
            }
        }

        public IList<QrLockInfo> ListAll()
        {
            var result = new List<QrLockInfo>();
            try
            {
                DataTable dt = Query(
                    "dbo.Usp_QrReaderLock_List",
                    new SqlParameter("@TtlSeconds", TtlSeconds));
                foreach (DataRow r in dt.Rows)
                    result.Add(Map(r, Convert.ToString(r["CustomerNo"]), Convert.ToString(r["ScanMode"])));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[QrLock.ListAll] " + ex.Message);
            }
            return result;
        }

        // Dùng SqlConnection riêng + EXEC proc: không dùng SQLPROVIDER.LoadData* vì chúng hiện MessageBox
        // khi lỗi, mà heartbeat chạy trên thread nền.
        private DataTable Query(string procName, params SqlParameter[] parameters)
        {
            using (var conn = new SqlConnection(_sql.B7R2_FCCdb))
            using (var cmd = new SqlCommand(procName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddRange(parameters);
                conn.Open();
                var dt = new DataTable();
                using (var reader = cmd.ExecuteReader())
                    dt.Load(reader);
                return dt;
            }
        }

        private QrLockInfo Map(DataRow r, string customerNo, string scanMode)
        {
            return new QrLockInfo
            {
                CustomerNo = customerNo,
                ScanMode = Has(r, "ScanMode") ? Str(r, "ScanMode") : NormalizeMode(scanMode),
                OwnerClientIp = Str(r, "OwnerClientIp"),
                OwnerMachine = Str(r, "OwnerMachine"),
                OwnerUser = Str(r, "OwnerUser"),
                LockedAt = Dt(r, "LockedAt"),
                HeartbeatAt = Dt(r, "HeartbeatAt"),
                IsStale = Bool(r, "IsStale"),
                // IsMine do SERVER tính từ IP kết nối — client không tự khai được.
                IsOwnedByMe = Bool(r, "IsMine"),
                Registered = Bool(r, "Registered")
            };
        }

        private static bool Has(DataRow r, string col) => r.Table.Columns.Contains(col) && r[col] != DBNull.Value;
        private static string Str(DataRow r, string col) => Has(r, col) ? Convert.ToString(r[col]) : string.Empty;
        private static bool Bool(DataRow r, string col) => Has(r, col) && Convert.ToBoolean(r[col]);
        private static DateTime? Dt(DataRow r, string col) => Has(r, col) ? Convert.ToDateTime(r[col]) : (DateTime?)null;

        private static string NormalizeMode(string mode)
        {
            string value = (mode ?? "MP").Trim().ToUpperInvariant();
            if (value != "MP" && value != "SP") throw new ArgumentException("Chế độ đọc QR chỉ nhận MP hoặc SP.", nameof(mode));
            return value;
        }

        private static QrLockInfo NoOwner(string customerNo, string mode)
            => new QrLockInfo { CustomerNo = customerNo, ScanMode = mode, IsOwnedByMe = false };
    }
}
