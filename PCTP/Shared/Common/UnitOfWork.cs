using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Common
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly SQLPROVIDER _sql;
        private int _depth;
        private bool _rollbackOnly;   // ← THÊM
        private SqlConnection _connection;
        private SqlTransaction _transaction;

        public UnitOfWork(SQLPROVIDER sql)
            => _sql = sql ?? throw new ArgumentNullException(nameof(sql));

        public SqlConnection Connection => _connection;
        public SqlTransaction Transaction => _transaction;
        public bool IsInTransaction => _transaction != null;
        public int Depth => _depth;

        public void Begin()
        {
            if (_transaction == null)
            {
                OpenConnectionAndBeginWithRetry();
                _depth = 1;
                _rollbackOnly = false;
                return;
            }
            _depth++;
        }

        /// <summary>
        /// Retries only opening a SQL connection / beginning an empty transaction.
        /// Never retries a command or replays a business write: after a connection
        /// loss during a write, the caller must roll back/reconcile before retrying
        /// the idempotent business request.
        /// </summary>
        private void OpenConnectionAndBeginWithRetry()
        {
            const int maxAttempts = 3;
            Exception last = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                SqlConnection candidate = null;
                try
                {
                    candidate = new SqlConnection(_sql.B7R2_FCCdb);
                    candidate.Open();
                    SqlTransaction transaction = candidate.BeginTransaction();
                    _connection = candidate;
                    _transaction = transaction;
                    return;
                }
                catch (SqlException ex) when (IsTransientConnectionError(ex) && attempt < maxAttempts)
                {
                    last = ex;
                    candidate?.Dispose();
                    System.Threading.Thread.Sleep(200 * attempt);
                }
                catch
                {
                    candidate?.Dispose();
                    throw;
                }
            }

            throw new InvalidOperationException(
                "Không thể mở SQL transaction sau 3 lần thử kết nối. Không tự chạy lại thao tác ghi; hãy kiểm tra kết nối rồi truy vấn trạng thái theo ReferenceType/ReferenceId.",
                last);
        }

        internal static bool IsTransientConnectionError(SqlException exception)
        {
            if (exception == null) return false;
            foreach (SqlError error in exception.Errors)
            {
                switch (error.Number)
                {
                    case -2:      // connect timeout
                    case 2:       // SQL network endpoint not found
                    case 53:      // network path not found
                    case 64:      // network name deleted
                    case 233:     // connection initialization failure
                    case 10053:   // connection aborted
                    case 10054:   // connection reset
                    case 10060:   // network timeout
                    case 11001:   // host not found
                        return true;
                }
            }
            return false;
        }

        public void Commit()
        {
            if (_transaction == null) { _depth = 0; return; }

            if (_depth > 1)
            {
                _depth--;          // tầng con — chưa commit thật
                return;
            }

            // tầng ngoài cùng
            try
            {
                if (_rollbackOnly)
                {
                    _transaction.Rollback();
                    throw new InvalidOperationException(
                        "Transaction đã bị đánh dấu rollback-only bởi 1 scope con — " +
                        "toàn bộ thay đổi đã bị huỷ.");
                }
                _transaction.Commit();
            }
            finally
            {
                DisposeTransaction();
                _depth = 0;
                _rollbackOnly = false;
            }
        }

        public void Rollback()
        {
            if (_transaction == null) { _depth = 0; return; }

            if (_depth > 1)
            {
                // ← FIX CHÍNH: không rollback thật ở đây, chỉ đánh dấu
                _rollbackOnly = true;
                _depth--;
                return;
            }

            // tầng ngoài cùng — rollback thật
            try { _transaction.Rollback(); }
            finally
            {
                DisposeTransaction();
                _depth = 0;
                _rollbackOnly = false;
            }
        }

        private void DisposeTransaction()
        {
            _transaction?.Dispose();
            _transaction = null;
            _connection?.Dispose();
            _connection = null;
        }

        public void Dispose()
        {
            try
            {
                if (_transaction != null)
                {
                    try { _transaction.Rollback(); } catch { /* SQL Server có thể đã tự đóng */ }
                }
            }
            finally
            {
                DisposeTransaction();
                _depth = 0;
                _rollbackOnly = false;
            }
        }
    }
}
