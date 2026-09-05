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

        // Cho phép override theo repo nếu cần SP nặng hơn
        protected virtual int CommandTimeoutSeconds => 120;

        protected SqlRepositoryBase(PhieuSqlExecutor db, IUnitOfWork uow)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            Uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        protected SqlConnection Connection => Uow.Connection;
        protected SqlTransaction Transaction => Uow.Transaction;
        protected bool HasTransaction => Uow.IsInTransaction;

        protected DataTable LoadData(string sql, params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.LoadData(sql, CloneParameters(parameters));

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

        protected object ExecuteScalar(string sql, params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteScalar(sql, CloneParameters(parameters));

            using (SqlCommand cmd = CreateCommand(sql, CommandType.Text))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteScalar();
            }
        }

        protected int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteNonQuery(sql, CloneParameters(parameters));

            using (SqlCommand cmd = CreateCommand(sql, CommandType.Text))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        protected DataTable ExecuteStoredProcedure(string procedureName, params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteStoredProcedure(procedureName, CloneParameters(parameters));

            using (SqlCommand cmd = CreateCommand(procedureName, CommandType.StoredProcedure))
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

        /// <summary>
        /// Chạy stored procedure trả về NHIỀU resultset (VD: SP vừa trả dữ liệu vừa trả bảng lỗi).
        /// Không transaction → uỷ quyền Db.ExecuteStoredProcedureDataSet (tự mở/đóng connection).
        /// Có transaction → dùng chung Uow.Connection/Uow.Transaction.
        /// </summary>
        protected DataSet ExecuteStoredProcedureDataSet(string procedureName, params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteStoredProcedureDataSet(procedureName, CloneParameters(parameters));

            using (SqlCommand cmd = CreateCommand(procedureName, CommandType.StoredProcedure))
            {
                AddParameters(cmd, parameters);
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);
                    return ds;
                }
            }
        }

        /// <summary>
        /// Bulk insert vào bảng staging bằng SqlBulkCopy.
        /// Không transaction → uỷ quyền Db.BulkInsertDataTable (connection + transaction nội bộ riêng,
        /// đúng hành vi cũ của SqlTableCreator.BulkInsertDataTable).
        /// Có transaction → SqlBulkCopy dùng chung Uow.Connection/Uow.Transaction, tham gia
        /// transaction ngoài (khác hành vi cũ — CHỈ áp dụng khi caller chủ động Uow.Begin()).
        /// </summary>
        protected void BulkInsert(string tableName, DataTable table)
        {
            Db.ValidateTableName(tableName);

            if (!HasTransaction)
            {
                Db.BulkInsertDataTable(tableName, table);
                return;
            }

            using (var bulkCopy = new SqlBulkCopy(Connection, SqlBulkCopyOptions.TableLock, Transaction))
            {
                bulkCopy.DestinationTableName = tableName;
                bulkCopy.WriteToServer(table);
            }
        }

        protected object ExecuteScalarStoredProcedure(string procedureName, params SqlParameter[] parameters)
        {
            if (!HasTransaction)
                return Db.ExecuteScalarStoredProcedure(procedureName, CloneParameters(parameters));

            using (SqlCommand cmd = CreateCommand(procedureName, CommandType.StoredProcedure))
            {
                AddParameters(cmd, parameters);
                return cmd.ExecuteScalar();
            }
        }

        private SqlCommand CreateCommand(string commandText, CommandType commandType)
        {
            if (Connection == null)
                throw new InvalidOperationException(
                    "HasTransaction=true nhưng Uow.Connection là null — UnitOfWork chưa Begin() đúng cách.");

            SqlCommand cmd = Connection.CreateCommand();
            cmd.CommandText = commandText;
            cmd.CommandType = commandType;
            cmd.Transaction = Transaction;
            cmd.CommandTimeout = CommandTimeoutSeconds;   // ← THÊM
            return cmd;
        }

        // ── AddParameters (dùng cho nhánh có transaction — command sở hữu riêng) ──
        private static void AddParameters(SqlCommand command, SqlParameter[] parameters)
        {
            if (parameters == null) return;
            foreach (var p in CloneParameters(parameters))
                command.Parameters.Add(p);
        }

        // ── Clone để tránh "parameter already in another collection" ──────────
        // Áp dụng ở MỌI điểm ra khỏi lớp này (kể cả gọi Db.*), vì tham số truyền vào
        // có thể bị caller tái sử dụng cho lệnh gọi khác (retry, vòng lặp...).
        private static SqlParameter[] CloneParameters(SqlParameter[] parameters)
        {
            if (parameters == null) return Array.Empty<SqlParameter>();

            var result = new SqlParameter[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p == null) continue;

                result[i] = new SqlParameter(p.ParameterName, p.SqlDbType, p.Size)
                {
                    Value = p.Value,
                    Direction = p.Direction,
                    IsNullable = p.IsNullable,
                    Precision = p.Precision,
                    Scale = p.Scale
                };
            }
            return result;
        }

        /// <summary>
        /// Lấy Status hiện tại của một entity theo ID
        /// </summary>
        protected TStatus? GetStatus<TStatus, TId>(string tableName, TId id, string idColumnName = "Id", string statusColumnName = "Status")
            where TStatus : struct
        {
            Db.ValidateTableName(tableName);

            string sql = $"SELECT {statusColumnName} FROM {tableName} WHERE {idColumnName} = @Id";
            object kq = ExecuteScalar(sql, new SqlParameter("@Id", id));

            if (kq == null || kq == DBNull.Value)
                return null;

            return (TStatus)Enum.ToObject(typeof(TStatus), kq);
        }

        /// <summary>
        /// Update Status có kiểm tra Concurrency (WHERE Status = expectedFrom)
        /// </summary>
        protected bool UpdateStatusIfCurrentIs<TStatus, TId>(
            string tableName,
            TId id,
            TStatus expectedFrom,
            TStatus newStatus,
            string nguoiThucHien,
            string idColumnName = "Id",
            string statusColumnName = "Status")
            where TStatus : struct
        {
            Db.ValidateTableName(tableName);

            string sql = $@"
            UPDATE {tableName}
            SET {statusColumnName} = @NewStatus,
                UpdatedAt = GETDATE(),
                UpdatedBy = @UpdatedBy
            WHERE {idColumnName} = @Id 
              AND {statusColumnName} = @ExpectedFrom;";

            int affected = ExecuteNonQuery(
                sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@NewStatus", Convert.ToInt32(newStatus)),
                new SqlParameter("@ExpectedFrom", Convert.ToInt32(expectedFrom)),
                new SqlParameter("@UpdatedBy", (object)nguoiThucHien ?? DBNull.Value));

            return affected > 0;
        }
    }
}