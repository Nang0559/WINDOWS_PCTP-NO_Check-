using System;
using System.Data;
using System.Data.SqlClient;

namespace PCTP.Modules.GiaoHangKhach
{
    // Test-only executor. The SqlRepositoryBase integration scenario must take
    // its active-transaction path and must never fall back to this executor.
    public sealed class PhieuSqlExecutor
    {
        public DataTable LoadData(string sql, params SqlParameter[] parameters) => throw OutsideTransaction();
        public object ExecuteScalar(string sql, params SqlParameter[] parameters) => throw OutsideTransaction();
        public int ExecuteNonQuery(string sql, params SqlParameter[] parameters) => throw OutsideTransaction();
        public DataTable ExecuteStoredProcedure(string name, params SqlParameter[] parameters) => throw OutsideTransaction();
        public DataSet ExecuteStoredProcedureDataSet(string name, params SqlParameter[] parameters) => throw OutsideTransaction();
        public void BulkInsertDataTable(string name, DataTable table) => throw OutsideTransaction();
        public void ValidateTableName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { ';', '\'', '[', ']' }) >= 0)
                throw new ArgumentException("Invalid test table name.", nameof(name));
        }
        public object ExecuteScalarStoredProcedure(string name, params SqlParameter[] parameters) => throw OutsideTransaction();

        private static InvalidOperationException OutsideTransaction() =>
            new InvalidOperationException("SqlRepositoryBase incorrectly bypassed the active UnitOfWork transaction.");
    }
}
