

namespace PCTP.Shared.Common
{
    using PCTP.Modules.GiaoHangKhach;
    using System;
    using System.Data;
    using System.Data.SqlClient;

    public abstract class SqlRepositoryBase
    {
        protected readonly PhieuSqlExecutor Db;
        protected readonly IUnitOfWork Uow;

        protected SqlRepositoryBase(
            PhieuSqlExecutor db,
            IUnitOfWork uow)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            Uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        protected SqlConnection Connection
        {
            get { return Uow.Connection; }
        }

        protected SqlTransaction Transaction
        {
            get { return Uow.Transaction; }
        }

        protected bool HasTransaction
        {
            get { return Uow.IsInTransaction; }
        }

        protected DataTable LoadData(
            string sql,
            params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.LoadData(sql, parameters);

            using (SqlCommand cmd = CreateCommand(sql, CommandType.Text))
            {
                AddParameters(cmd, parameters);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        protected object ExecuteScalar(
            string sql,
            params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteScalar(sql, parameters);

            using (SqlCommand cmd = CreateCommand(sql, CommandType.Text))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteScalar();
            }
        }

        protected int ExecuteNonQuery(
            string sql,
            params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteNonQuery(sql, parameters);

            using (SqlCommand cmd = CreateCommand(sql, CommandType.Text))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        protected DataTable ExecuteStoredProcedure(
            string procedureName,
            params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteStoredProcedure(
                    procedureName,
                    parameters);

            using (SqlCommand cmd =
                CreateCommand(
                    procedureName,
                    CommandType.StoredProcedure))
            {
                AddParameters(cmd, parameters);

                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        protected object ExecuteScalarStoredProcedure(string procedureName, params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteScalarStoredProcedure(procedureName, parameters);   // ✅ sửa
            using (SqlCommand cmd = CreateCommand(procedureName, CommandType.StoredProcedure))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteScalar();
            }
        }

        private SqlCommand CreateCommand(
            string commandText,
            CommandType commandType)
        {
            SqlCommand cmd = Connection.CreateCommand();

            cmd.CommandText = commandText;
            cmd.CommandType = commandType;
            cmd.Transaction = Transaction;

            return cmd;
        }

        private static void AddParameters(
            SqlCommand command,
            SqlParameter[] parameters)
        {
            if (parameters == null)
                return;

            foreach (SqlParameter parameter in parameters)
            {
                if (parameter != null)
                    command.Parameters.Add(parameter);
            }
        }
    }
}
