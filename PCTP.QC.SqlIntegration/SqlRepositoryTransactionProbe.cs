using System;
using System.Data.SqlClient;
using PCTP.Modules.GiaoHangKhach;
using PCTP.Shared.Common;

namespace PCTP.QC.SqlIntegration
{
    internal sealed class SqlRepositoryTransactionProbe : SqlRepositoryBase
    {
        public SqlRepositoryTransactionProbe(PhieuSqlExecutor executor, IUnitOfWork uow)
            : base(executor, uow) { }

        public bool IsUsingUnitOfWorkTransaction =>
            HasTransaction && ReferenceEquals(Connection, Uow.Connection)
            && ReferenceEquals(Transaction, Uow.Transaction);

        public int ExecuteUpdate(string sql) => ExecuteNonQuery(sql);

        // Expose the protected SqlRepositoryBase.InTransaction scope to the integration scenarios.
        public T RunFunc<T>(Func<T> work) => InTransaction(work);
        public void RunAction(Action work) => InTransaction(work);
    }
}
