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

        private SqlConnection _connection;
        private SqlTransaction _transaction;

        public UnitOfWork(SQLPROVIDER sql)
        {
            _sql = sql ?? throw new ArgumentNullException(nameof(sql));
        }

        public SqlConnection Connection
        {
            get { return _connection; }
        }

        public SqlTransaction Transaction
        {
            get { return _transaction; }
        }

        public bool IsInTransaction
        {
            get { return _transaction != null; }
        }

        public void Begin()
        {
            if (IsInTransaction)
                throw new InvalidOperationException(
                    "UnitOfWork đang có transaction.");

            _connection = new SqlConnection(
                _sql.B7R2_FCCdb);

            _connection.Open();

            _transaction = _connection.BeginTransaction();
        }

        public void Commit()
        {
            if (_transaction == null)
                return;

            try
            {
                _transaction.Commit();
            }
            finally
            {
                DisposeTransaction();
            }
        }

        public void Rollback()
        {
            if (_transaction == null)
                return;

            try
            {
                _transaction.Rollback();
            }
            finally
            {
                DisposeTransaction();
            }
        }

        private void DisposeTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }

            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }

        public void Dispose()
        {
            DisposeTransaction();
        }
    }
}
