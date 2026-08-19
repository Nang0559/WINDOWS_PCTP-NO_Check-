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

        public PhieuSqlExecutor(SQLPROVIDER sql)
        {
            _sql = sql ?? throw new ArgumentNullException(nameof(sql));
        }

        public SQLPROVIDER Sql => _sql;

        // ============================================================
        // CONNECTION MẶC ĐỊNH
        // ============================================================

        public DataTable LoadData(
            string sqlText,
            params SqlParameter[] parameters)
        {
            return _sql.LoadData1(
                _sql.B7R2_FCCdb,
                sqlText,
                parameters ?? Array.Empty<SqlParameter>());
        }

        public object ExecuteScalar(
            string sqlText,
            params SqlParameter[] parameters)
        {
            return _sql.ExecuteScalar(
                _sql.B7R2_FCCdb,
                sqlText,
                parameters ?? Array.Empty<SqlParameter>());
        }

        public int ExecuteNonQuery(
            string sqlText,
            params SqlParameter[] parameters)
        {
            return _sql.ExecuteNonQuery(
                _sql.B7R2_FCCdb,
                sqlText,
                parameters ?? Array.Empty<SqlParameter>());
        }

        // ============================================================
        // TRANSACTION
        // ============================================================

        public int ExecuteNonQuery(
            SqlConnection conn,
            SqlTransaction tran,
            string sqlText,
            params SqlParameter[] parameters)
        {
            if (conn == null)
                throw new ArgumentNullException(nameof(conn));

            if (tran == null)
                throw new ArgumentNullException(nameof(tran));

            if (string.IsNullOrWhiteSpace(sqlText))
                throw new ArgumentException(
                    "SQL không được rỗng.",
                    nameof(sqlText));

            return _sql.ExecuteNonQuery(
                conn,
                tran,
                sqlText,
                parameters ?? Array.Empty<SqlParameter>());
        }

        public object ExecuteScalar(
            SqlConnection conn,
            SqlTransaction tran,
            string sqlText,
            params SqlParameter[] parameters)
        {
            if (conn == null)
                throw new ArgumentNullException(nameof(conn));

            if (tran == null)
                throw new ArgumentNullException(nameof(tran));

            if (string.IsNullOrWhiteSpace(sqlText))
                throw new ArgumentException(
                    "SQL không được rỗng.",
                    nameof(sqlText));

            return _sql.ExecuteScalar(
                conn,
                tran,
                sqlText,
                parameters ?? Array.Empty<SqlParameter>());
        }

        public DataTable LoadData(
            SqlConnection conn,
            SqlTransaction tran,
            string sqlText,
            params SqlParameter[] parameters)
        {
            if (conn == null)
                throw new ArgumentNullException(nameof(conn));

            if (tran == null)
                throw new ArgumentNullException(nameof(tran));

            if (string.IsNullOrWhiteSpace(sqlText))
                throw new ArgumentException(
                    "SQL không được rỗng.",
                    nameof(sqlText));

            using (var cmd = new SqlCommand(sqlText, conn, tran))
            {
                cmd.CommandType = CommandType.Text;

                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var table = new DataTable();
                    adapter.Fill(table);
                    return table;
                }
            }
        }
        // Thêm vào PhieuSqlExecutor.cs
        public object ExecuteScalarStoredProcedure(string procedureName, params SqlParameter[] parameters)
        {
            using (var conn = new SqlConnection(_sql.B7R2_FCCdb))
            {
                conn.Open();
                using (var cmd = new SqlCommand(procedureName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    if (parameters != null && parameters.Length > 0)
                        cmd.Parameters.AddRange(parameters);
                    return cmd.ExecuteScalar();
                }
            }
        }
        // ============================================================
        // STORED PROCEDURE
        // ============================================================

        public DataTable ExecuteStoredProcedure(
            string procedureName,
            params SqlParameter[] parameters)
        {
            var ds = _sql.ExecuteProcedureReturnDataSet(
                _sql.B7R2_FCCdb,
                procedureName,
                parameters ?? Array.Empty<SqlParameter>());

            return ds != null && ds.Tables.Count > 0
                ? ds.Tables[0]
                : new DataTable();
        }

        public int ExecuteStoredProcedureNonQuery(
            string procedureName,
            params SqlParameter[] parameters)
        {
            return _sql.ExecuteStoredProcedure(
                _sql.B7R2_FCCdb,
                procedureName,
                parameters ?? Array.Empty<SqlParameter>());
        }

        public DataSet ExecuteStoredProcedureDataSet(
            string procedureName,
            params SqlParameter[] parameters)
        {
            return _sql.ExecuteProcedureReturnDataSet(
                _sql.B7R2_FCCdb,
                procedureName,
                parameters ?? Array.Empty<SqlParameter>());
        }

        // ============================================================
        // HELPER
        // ============================================================


        public void ValidateTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException(
                    "Tên bảng không được rỗng.",
                    nameof(tableName));

            if (Regex.IsMatch(tableName, @"[^A-Za-z0-9_]"))
            {
                throw new ArgumentException(
                    $"Tên bảng không hợp lệ: '{tableName}'.",
                    nameof(tableName));
            }
        }
    }
}
