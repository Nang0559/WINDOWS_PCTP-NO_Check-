using PCTP.ClassSQL;
using PCTP.Shared.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach
{
    /// <summary>
    /// Hạ tầng SQL dùng chung cho các Phieu*Repository.
    ///
    /// Không chứa business logic của:
    /// - Tmp
    /// - Lot
    /// - Kho
    /// - Lưu trữ
    /// - Validation
    /// - GiaoDB
    ///
    /// Chỉ chịu trách nhiệm:
    /// - Validate tên bảng
    /// - Execute SP trả DataSet/DataTable
    /// - Drop/Create hoặc Truncate bảng staging
    /// - Một số thao tác SQL infrastructure dùng chung
    /// </summary>
    public sealed class PhieuSqlExecutor
    {
        private readonly SQLPROVIDER _sql;
        private const int DefaultTimeoutSeconds = 120;
        private const int HeavySpTimeoutSeconds = 1200; // khớp ExecuteProcedureReturnDataSet cũ

        public PhieuSqlExecutor(SQLPROVIDER sql)
            => _sql = sql ?? throw new ArgumentNullException(nameof(sql));

        public SQLPROVIDER Sql => _sql;

        // ============================================================
        // CONNECTION MẶC ĐỊNH (không transaction)
        // ============================================================

        public DataTable LoadData(string sqlText, params SqlParameter[] parameters)
        {
            RequireSql(sqlText);
            return _sql.LoadData1(_sql.B7R2_FCCdb, sqlText, CloneParameters(parameters));
        }

        public object ExecuteScalar(string sqlText, params SqlParameter[] parameters)
        {
            RequireSql(sqlText);
            return _sql.ExecuteScalar(_sql.B7R2_FCCdb, sqlText, CloneParameters(parameters));
        }

        public int ExecuteNonQuery(string sqlText, params SqlParameter[] parameters)
        {
            RequireSql(sqlText);
            return _sql.ExecuteNonQuery(_sql.B7R2_FCCdb, sqlText, CloneParameters(parameters));
        }

        // ============================================================
        // TRANSACTION — TEXT SQL
        // ============================================================

        public int ExecuteNonQuery(SqlConnection conn, SqlTransaction tran,
            string sqlText, params SqlParameter[] parameters)
        {
            RequireConnTran(conn, tran);
            RequireSql(sqlText);
            return _sql.ExecuteNonQuery(conn, tran, sqlText, CloneParameters(parameters));
        }

        public object ExecuteScalar(SqlConnection conn, SqlTransaction tran,
            string sqlText, params SqlParameter[] parameters)
        {
            RequireConnTran(conn, tran);
            RequireSql(sqlText);
            return _sql.ExecuteScalar(conn, tran, sqlText, CloneParameters(parameters));
        }

        public DataTable LoadData(SqlConnection conn, SqlTransaction tran,
            string sqlText, params SqlParameter[] parameters)
        {
            RequireConnTran(conn, tran);
            RequireSql(sqlText);

            using (var cmd = new SqlCommand(sqlText, conn, tran))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = DefaultTimeoutSeconds;

                foreach (var p in CloneParameters(parameters))
                    cmd.Parameters.Add(p);

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        // ============================================================
        // STORED PROCEDURE — không transaction
        // ============================================================

        public DataTable ExecuteStoredProcedure(string procedureName, params SqlParameter[] parameters)
        {
            RequireSql(procedureName);
            var ds = _sql.ExecuteProcedureReturnDataSet(_sql.B7R2_FCCdb, procedureName, CloneParameters(parameters));
            return ds != null && ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
        }

        public DataSet ExecuteStoredProcedureDataSet(string procedureName, params SqlParameter[] parameters)
        {
            RequireSql(procedureName);
            return _sql.ExecuteProcedureReturnDataSet(_sql.B7R2_FCCdb, procedureName, CloneParameters(parameters));
        }

        // ✅ FIX: SQLPROVIDER không có ExecuteStoredProcedure(string,string,SqlParameter[]) —
        // tự implement bằng SqlCommand riêng thay vì gọi method không tồn tại.
        public int ExecuteStoredProcedureNonQuery(string procedureName, params SqlParameter[] parameters)
        {
            RequireSql(procedureName);
            using (var conn = new SqlConnection(_sql.B7R2_FCCdb))
            using (var cmd = new SqlCommand(procedureName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = HeavySpTimeoutSeconds;
                foreach (var p in CloneParameters(parameters))
                    cmd.Parameters.Add(p);

                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public object ExecuteScalarStoredProcedure(string procedureName, params SqlParameter[] parameters)
        {
            RequireSql(procedureName);
            using (var conn = new SqlConnection(_sql.B7R2_FCCdb))
            using (var cmd = new SqlCommand(procedureName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = DefaultTimeoutSeconds;
                foreach (var p in CloneParameters(parameters))
                    cmd.Parameters.Add(p);

                conn.Open();
                return cmd.ExecuteScalar();
            }
        }

        // ============================================================
        // STORED PROCEDURE — trong transaction (BỔ SUNG — thiếu ở bản cũ)
        // ============================================================

        public DataTable ExecuteStoredProcedure(SqlConnection conn, SqlTransaction tran,
            string procedureName, params SqlParameter[] parameters)
        {
            RequireConnTran(conn, tran);
            RequireSql(procedureName);

            using (var cmd = new SqlCommand(procedureName, conn, tran))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = HeavySpTimeoutSeconds;
                foreach (var p in CloneParameters(parameters))
                    cmd.Parameters.Add(p);

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }

        public int ExecuteStoredProcedureNonQuery(SqlConnection conn, SqlTransaction tran,
            string procedureName, params SqlParameter[] parameters)
        {
            RequireConnTran(conn, tran);
            RequireSql(procedureName);

            using (var cmd = new SqlCommand(procedureName, conn, tran))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = HeavySpTimeoutSeconds;
                foreach (var p in CloneParameters(parameters))
                    cmd.Parameters.Add(p);

                return cmd.ExecuteNonQuery();
            }
        }

        public object ExecuteScalarStoredProcedure(SqlConnection conn, SqlTransaction tran,
            string procedureName, params SqlParameter[] parameters)
        {
            RequireConnTran(conn, tran);
            RequireSql(procedureName);

            using (var cmd = new SqlCommand(procedureName, conn, tran))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = DefaultTimeoutSeconds;
                foreach (var p in CloneParameters(parameters))
                    cmd.Parameters.Add(p);

                return cmd.ExecuteScalar();
            }
        }

        // ============================================================
        // HELPER
        // ============================================================

        public void ValidateTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Tên bảng không được rỗng.", nameof(tableName));

            if (Regex.IsMatch(tableName, @"[^A-Za-z0-9_]"))
                throw new ArgumentException($"Tên bảng không hợp lệ: '{tableName}'.", nameof(tableName));
        }

        private static void RequireConnTran(SqlConnection conn, SqlTransaction tran)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));
            if (tran == null) throw new ArgumentNullException(nameof(tran));
        }

        private static void RequireSql(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("SQL/Tên thủ tục không được rỗng.", nameof(text));
        }

        // Clone để tránh "The SqlParameter is already contained by another SqlParameterCollection"
        // — cùng nguyên tắc đã áp dụng ở SqlRepositoryBase và SQLPROVIDER.ExecuteScalar.
        private static SqlParameter[] CloneParameters(SqlParameter[] parameters)
        {
            if (parameters == null || parameters.Length == 0) return Array.Empty<SqlParameter>();

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
    }
}
