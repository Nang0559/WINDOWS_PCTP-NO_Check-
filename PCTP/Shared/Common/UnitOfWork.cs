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
                _connection = new SqlConnection(_sql.B7R2_FCCdb);
                _connection.Open();
                _transaction = _connection.BeginTransaction();
                _depth = 1;
                _rollbackOnly = false;
                return;
            }
            _depth++;
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
