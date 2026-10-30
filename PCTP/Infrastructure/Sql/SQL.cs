
using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;


namespace PCTP.ClassSQL
{
    public class SQLPROVIDER
    {
        public static List<string> c_Ns = new List<string>();

        // ════════════════════════════════════════════════════════════════════
        // CONNECTION STRINGS
        // ⚠ B7R2_FCCdb và B7R2_FCCdbb hiện trỏ CÙNG server + CÙNG catalog
        // (192.168.200.57 / B7R2_FCC) — KHÔNG phải 2 DB khác nhau, chỉ là 2 tên
        public string B7R2_FCCdb = GetConfiguredConnection("B7R2_FCCdb");
        public string B7R2_FCCdbb = GetConfiguredConnection("B7R2_FCCdbb");

        /// <summary>
        /// Reads the named connection string from the external PCTP.Database.config file.
        /// QcSqlOverride remains the final gate for isolated CI fixture redirection.
        /// </summary>
        /// <summary>
        /// Single source of truth for the application's SQL Server database.
        /// Legacy callers may still pass an old connection-string alias; all
        /// aliases are normalized to the canonical B7R2_FCCdb setting.
        /// QcSqlOverride remains able to redirect this connection to an
        /// isolated CI fixture.
        /// </summary>
        internal static string GetConfiguredConnection(string key = "B7R2_FCCdb")
        {
            var configured = ConfigurationManager.ConnectionStrings["B7R2_FCCdb"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(configured))
            {
                throw new InvalidOperationException(
                    "Missing canonical SQL setting 'B7R2_FCCdb'. Check PCTP.Database.config next to PCTP.exe.");
            }

            if (configured.IndexOf("YOUR_SQL_USER", StringComparison.OrdinalIgnoreCase) >= 0 ||
                configured.IndexOf("YOUR_SQL_PASSWORD", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(
                    "SQL credentials are not configured. Set the local B7R2_FCCdb connection in PCTP.Database.config.");
            }

            return QcSqlOverride.Resolve(configured);
        }

        /// <summary>Creates SQL connections through the canonical DB configuration.</summary>
        internal static SqlConnection CreateConnection(string legacyConnectionString = null)
        {
            // Explicit legacy strings are intentionally ignored: all SQL Server
            // modules in this application use the same B7R2_FCC database.
            return new SqlConnection(GetConfiguredConnection());
        }
        // lịch sử còn sót lại. Giữ cả 2 vì nhiều repository tham chiếu theo tên
        // cũ; đổi tên sẽ vỡ build hàng loạt.
        // ════════════════════════════════════════════════════════════════════
        // Mặc định KHÔNG trỏ Production. Môi trường khác (Production, CI fixture) phải được cấp rõ ràng:
        //   * CI/QC fixture : PCTP_QC_SQL_CONNECTION + PCTP_QC_ISOLATED_FIXTURE=1 (QcSqlOverride, fail-closed).
        //   * Production    : build/cấu hình riêng của bản phát hành — không commit vào mặc định này.
        // Các alias cũ phải dùng chung cấu hình, không tự chỉ định máy chủ riêng.


        #region ══ TRANSACTION LIFECYCLE ══════════════════════════════════════

        /// <summary>
        /// Mở 1 SqlConnection + bắt đầu transaction cục bộ. Caller BẮT BUỘC
        /// dùng "using" cho SqlConnection trả về, và tự Commit()/Rollback()
        /// trên SqlTransaction trước khi Dispose.
        /// </summary>
        /// <example>
        /// using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
        /// {
        ///     try { _sql.ExecuteNonQuery(conn, tran, "...", ...); tran.Commit(); }
        ///     catch { tran.Rollback(); throw; }
        /// }
        /// </example>
        public SqlConnection BeginTransaction(string connectionSTR, out SqlTransaction tran)
        {
            var conn = CreateConnection(connectionSTR);
            conn.Open();
            tran = conn.BeginTransaction();
            return conn;
        }

        #endregion

        #region ══ TEXT MODE — TRANSACTION-AWARE (dùng khi đang trong 1 transaction) ══

        /// <summary>Chạy UPDATE/INSERT/DELETE trong transaction hiện có. Ném exception nếu lỗi — caller tự Rollback() trong catch.</summary>
        public int ExecuteNonQuery(SqlConnection conn, SqlTransaction tran,
            string query, params SqlParameter[] parameters)
        {
            using (var command = new SqlCommand(query, conn, tran))
            {
                command.CommandType = CommandType.Text;
                if (parameters != null && parameters.Length > 0)
                    command.Parameters.AddRange(parameters);
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>Trả về giá trị đơn (COUNT, SUM, 1 ô...) trong transaction hiện có.</summary>
        public object ExecuteScalar(SqlConnection conn, SqlTransaction tran,
            string query, SqlParameter[] parameters = null)
        {
            using (var command = new SqlCommand(query, conn, tran))
            {
                command.CommandType = CommandType.Text;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        command.Parameters.Add(new SqlParameter(p.ParameterName, p.SqlDbType)
                        {
                            Value = p.Value ?? DBNull.Value,
                            Direction = p.Direction,
                            IsNullable = p.IsNullable,
                            Size = p.Size
                        });
                    }
                }
                return command.ExecuteScalar();
            }
        }

        /// <summary>Trả về DataTable từ câu lệnh Text (SELECT có tham số) trong transaction hiện có.</summary>
        public DataTable ExecuteQuery(SqlConnection conn, SqlTransaction tran,
            string query, List<SqlParameter> parameters = null)
        {
            var data = new DataTable();
            using (var command = new SqlCommand(query, conn, tran))
            {
                command.CommandType = CommandType.Text;
                if (parameters != null)
                    command.Parameters.AddRange(parameters.ToArray());
                using (var adapter = new SqlDataAdapter(command))
                    adapter.Fill(data);
            }
            return data;
        }

        /// <summary>Trả về DataTable từ câu lệnh Text (SELECT có tham số) trong transaction hiện có — bản params SqlParameter[].</summary>
        public DataTable LoadData1(SqlConnection conn, SqlTransaction tran,
            string query, params SqlParameter[] paramList)
        {
            using (var cmd = new SqlCommand(query, conn, tran))
            {
                cmd.CommandType = CommandType.Text;
                if (paramList != null && paramList.Length > 0)
                    cmd.Parameters.AddRange(paramList);
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        /// <summary>Đọc giá trị cột đầu tiên của dòng cuối cùng khớp điều kiện, trong transaction hiện có. Query KHÔNG tham số hoá — chỉ dùng cho câu lệnh tĩnh, không ghép input người dùng.</summary>
        public string ExecuteReader(SqlConnection conn, SqlTransaction tran, string query)
        {
            string value = "";
            using (var command = new SqlCommand(query, conn, tran) { CommandType = CommandType.Text })
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    value = string.Format("{0}", reader[0]);
            }
            return value;
        }

        /// <summary>Chạy stored procedure trong transaction hiện có, trả về DataSet.</summary>
        public DataSet ExecuteProcedureReturnDataSet(SqlConnection conn, SqlTransaction tran,
            string procName, params SqlParameter[] parameters)
        {
            using (var command = conn.CreateCommand())
            using (var sda = new SqlDataAdapter(command))
            {
                command.Transaction = tran;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = procName;
                sda.SelectCommand.CommandTimeout = 1200;
                if (parameters != null)
                    command.Parameters.AddRange(parameters);
                var result = new DataSet();
                sda.Fill(result);
                return result;
            }
        }

        #endregion

        #region ══ TEXT MODE — STANDALONE (không cần transaction, tự mở/đóng connection) ══

        /// <summary>
        /// Chạy UPDATE/INSERT/DELETE độc lập, tự mở/đóng connection riêng.
        /// Nuốt exception và hiển thị MessageBox, trả về -1 nếu lỗi —
        /// KHÔNG dùng hàm này bên trong 1 transaction đang mở (lỗi sẽ bị
        /// nuốt thay vì rollback đúng cách); trong trường hợp đó dùng bản
        /// nhận (conn, tran) ở trên.
        /// </summary>
        public int ExecuteNonQuery(string connectionSTR, string query, params SqlParameter[] parameters)
        {
            int data;
            try
            {
                using (var connection = CreateConnection(connectionSTR))
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection) { CommandType = CommandType.Text })
                    {
                        if (parameters != null && parameters.Length > 0)
                            command.Parameters.AddRange(parameters);
                        data = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery");
                data = -1;
            }
            return data;
        }

        /// <summary>Trả về giá trị đơn, tự mở/đóng connection riêng (không transaction).</summary>
        public object ExecuteScalar(string connectionSTR, string query, SqlParameter[] parameters = null)
        {
            using (var connection = CreateConnection(connectionSTR))
            using (var command = new SqlCommand(query, connection) { CommandType = CommandType.Text })
            {
                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        command.Parameters.Add(new SqlParameter(p.ParameterName, p.SqlDbType)
                        {
                            Value = p.Value ?? DBNull.Value,
                            Direction = p.Direction,
                            IsNullable = p.IsNullable,
                            Size = p.Size
                        });
                    }
                }
                connection.Open();
                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Đọc dữ liệu bằng câu lệnh Text ad hoc (SELECT có tham số), tự
        /// mở/đóng connection riêng. Đây là hàm CHUẨN cho mọi query Text
        /// không cần transaction — dùng thay cho mọi cách viết SqlCommand
        /// thủ công lặp lại.
        /// </summary>
        public DataTable LoadData1(string connString, string query, params SqlParameter[] paramList)
        {
            using (var sqlConnection = CreateConnection(connString))
            using (var cmd = sqlConnection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = query;
                if (paramList != null && paramList.Length > 0)
                    cmd.Parameters.AddRange(paramList);
                using (var adap = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    try
                    {
                        adap.Fill(dt);
                    }
                    catch (Exception ex)
                    {
                        XtraMessageBox.Show("Đọc dữ liệu thất bại" + Environment.NewLine + ex.Message);
                        dt = null;
                    }
                    cmd.Parameters.Clear();
                    return dt;
                }
            }
        }

        #endregion

        #region ══ STORED PROCEDURE — STANDALONE ══════════════════════════════

        /// <summary>Đọc dữ liệu bằng stored procedure, tự mở/đóng connection riêng, trả DataTable. Log chi tiết lỗi nếu có.</summary>
        public DataTable LoadData(string connString, string procName, params SqlParameter[] paramList)
        {
            using (var sqlConnection = CreateConnection(connString))
            using (var cmd = sqlConnection.CreateCommand())
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = procName;
                cmd.Parameters.AddRange(paramList);
                using (var adap = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    try
                    {
                        adap.Fill(dt);
                    }
                    catch (Exception ex)
                    {
                        var paramInfo = string.Join(", ", paramList.Select(p => $"{p.ParameterName}={p.Value}"));
                        System.Diagnostics.Debug.WriteLine(
                            $"[LoadData ERROR] Proc={procName}, Params=[{paramInfo}]\nMessage={ex.Message}");
                        XtraMessageBox.Show(
                            $"Đọc dữ liệu thất bại{Environment.NewLine}Proc: {procName}{Environment.NewLine}{ex.Message}");
                        dt = null;
                    }
                    cmd.Parameters.Clear();
                    return dt;
                }
            }
        }

        /// <summary>Chạy stored procedure, trả DataSet, tự mở/đóng connection riêng. Log chi tiết lỗi SQL nếu có.</summary>
        public DataSet ExecuteProcedureReturnDataSet(string connString, string procName,
            params SqlParameter[] paramters)
        {
            DataSet result;
            using (var sqlConnection = CreateConnection(connString))
            using (var command = sqlConnection.CreateCommand())
            using (var sda = new SqlDataAdapter(command))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = procName;
                sda.SelectCommand.CommandTimeout = 1200;
                if (paramters != null)
                    command.Parameters.AddRange(paramters);

                System.Diagnostics.Debug.WriteLine($"[SP] {procName}");
                if (paramters != null)
                    foreach (var p in paramters)
                        System.Diagnostics.Debug.WriteLine(
                            $"  Param: {p.ParameterName} = {p.Value} (Length={p.Value?.ToString()?.Length})");

                try
                {
                    result = new DataSet();
                    sda.Fill(result);
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SQL ERROR] SP={procName}");
                    System.Diagnostics.Debug.WriteLine($"  Message : {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"  Number  : {ex.Number}");
                    System.Diagnostics.Debug.WriteLine($"  State   : {ex.State}");
                    System.Diagnostics.Debug.WriteLine($"  LineNum : {ex.LineNumber}");
                    foreach (SqlError err in ex.Errors)
                        System.Diagnostics.Debug.WriteLine($"  SqlError: Line={err.LineNumber}, Msg={err.Message}");
                    throw;
                }
            }
            return result;
        }

        public string ExecuteReader(string connectionSTR, string query)
        {
            string value = "";

            using (var connection = CreateConnection(connectionSTR))
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.Text;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        value = reader[0]?.ToString() ?? "";
                    }
                }
            }

            return value;
        }
        public int ExecuteStoredProcedure(
        string connectionString,
        string procedureName,
        params SqlParameter[] parameters)
        {
            using (var connection = CreateConnection(connectionString))
            using (var command = new SqlCommand(procedureName, connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null && parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                connection.Open();

                return command.ExecuteNonQuery();
            }
        }

        #endregion


    }

    class IFSPROVIDER
    {

        static string host = "192.168.200.12";
        static int port = 1521;
        static string sid = "fccprod";
        //static string sid = "FCCSTG"; // Tét 
        static string user = "IFSAPP";
        static string password = "fccifs";
        //static string password = "IFSAPP";

        private string connectionSTR = @"Data Source=(DESCRIPTION =(ADDRESS = (PROTOCOL = TCP)(HOST = "
                 + host + ")(PORT = " + port + "))(CONNECT_DATA = (SERVER = DEDICATED)(SERVICE_NAME = "
                 + sid + ")));Password=" + password + ";User ID=" + user;
        public DataTable ExecuteQuery(string query, object[] parameter = null)
        {
            DataTable data = new DataTable();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);

                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery IFS");
            }
            return data;
        }

        public DataSet ExecuteQuery_Dataset(string query, object[] parameter = null)
        {
            DataSet data = new DataSet();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery_Dataset IFS");
            }
            return data;
        }

        public int ExecuteNonQuery(string query, object[] parameter = null)
        {
            int data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteNonQuery();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery IFS");
            }
            return data;
        }

        public object ExecuteScalar(string query, object[] parameter = null)
        {
            object data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteScalar();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteScalar IFS");
            }
            return data;
        }

        public string ExecuteReader(string query)
        {
            DataTable data = new DataTable();

            string _value = "";
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);


                    OracleDataReader MyReader = command.ExecuteReader();



                    while (MyReader.Read())
                    {

                        _value = String.Format("{0}", MyReader[0]);
                    }


                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
            }
            return _value;
        }



    }
    // Ket Nối WH 4W
    class WH4SQLPROVIDER
    {
        // Dùng chung cấu hình B7R2_FCCdb với SQLPROVIDER.
        // Có thể truyền connection string riêng qua overload tương thích bên dưới.
        public string AutoWH { get; } = SQLPROVIDER.GetConfiguredConnection("B7R2_FCCdb");

        private string ResolveConnection(string connectionSTR)
        {
            return string.IsNullOrWhiteSpace(connectionSTR) ? AutoWH : QcSqlOverride.Resolve(connectionSTR);
        }

        private static void AddNamedParameters(SqlCommand command, string query, object[] values)
        {
            if (values == null || values.Length == 0) return;

            var names = System.Text.RegularExpressions.Regex.Matches(query, @"@(?:\w+)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count != values.Length)
                throw new ArgumentException(
                    $"Số tham số truyền vào ({values.Length}) không khớp số tham số trong câu SQL ({names.Count}).");

            for (var i = 0; i < names.Count; i++)
                command.Parameters.AddWithValue(names[i], values[i] ?? DBNull.Value);
        }

        private static void AddParameters(SqlCommand command, IEnumerable<SqlParameter> parameters)
        {
            if (parameters == null) return;
            foreach (var parameter in parameters)
            {
                if (parameter == null) continue;
                var clone = (SqlParameter)((ICloneable)parameter).Clone();
                command.Parameters.Add(clone);
            }
        }

        public DataTable WH4ExecuteQuery(string query, List<SqlParameter> parameter = null)
            => WH4ExecuteQuery(null, query, parameter);

        public DataTable WH4ExecuteQuery(string connectionSTR, string query, List<SqlParameter> parameter = null)
        {
            var data = new DataTable();
            try
            {
                using (var connection = SQLPROVIDER.CreateConnection(connectionSTR))
                using (var command = new SqlCommand(query, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    command.CommandType = parameter != null && parameter.Count > 0
                        ? CommandType.StoredProcedure
                        : CommandType.Text;
                    AddParameters(command, parameter);
                    adapter.Fill(data);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
            }
            return data;
        }

        public string WH4ExecuteReader(string query) => WH4ExecuteReader(null, query);

        public string WH4ExecuteReader(string connectionSTR, string query)
        {
            var value = "";
            using (var connection = SQLPROVIDER.CreateConnection(connectionSTR))
            using (var command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        value = Convert.ToString(reader[0]);
            }
            return value;
        }

        public int WH4ExecuteReaderint(string query) => WH4ExecuteReaderint(null, query);

        public int WH4ExecuteReaderint(string connectionSTR, string query)
        {
            var value = WH4ExecuteReader(connectionSTR, query);
            return string.IsNullOrWhiteSpace(value) ? 0 : Convert.ToInt32(value);
        }

        public DataSet WH4ExecuteQuery_Dataset(string query, object[] parameter = null)
            => WH4ExecuteQuery_Dataset(null, query, parameter);

        public DataSet WH4ExecuteQuery_Dataset(string connectionSTR, string query, object[] parameter = null)
        {
            var data = new DataSet();
            try
            {
                using (var connection = SQLPROVIDER.CreateConnection(connectionSTR))
                using (var command = new SqlCommand(query, connection))
                using (var adapter = new SqlDataAdapter(command))
                {
                    AddNamedParameters(command, query, parameter);
                    adapter.Fill(data);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery_Dataset B7R2");
            }
            return data;
        }

        public DataSet WH4ExecuteProcedureReturnDataSet(string procName, params SqlParameter[] parameters)
            => WH4ExecuteProcedureReturnDataSet(null, procName, parameters);

        public DataSet WH4ExecuteProcedureReturnDataSet(string connString, string procName,
            params SqlParameter[] parameters)
        {
            using (var connection = SQLPROVIDER.CreateConnection(connString))
            using (var command = connection.CreateCommand())
            using (var adapter = new SqlDataAdapter(command))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = procName;
                command.CommandTimeout = 1200;
                if (parameters != null && parameters.Length > 0)
                    AddParameters(command, parameters);
                var result = new DataSet();
                adapter.Fill(result);
                return result;
            }
        }

        public int WH4ExecuteNonQuery(string query, object[] parameter = null)
            => WH4ExecuteNonQuery(null, query, parameter);

        public int WH4ExecuteNonQuery(string connectionSTR, string query, object[] parameter = null)
        {
            try
            {
                using (var connection = SQLPROVIDER.CreateConnection(connectionSTR))
                using (var command = new SqlCommand(query, connection))
                {
                    AddNamedParameters(command, query, parameter);
                    connection.Open();
                    return command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery AUTOWH");
                return -1;
            }
        }

        public object WH4ExecuteScalar(string query, object[] parameter = null)
            => WH4ExecuteScalar(null, query, parameter);

        public object WH4ExecuteScalar(string connectionSTR, string query, object[] parameter = null)
        {
            try
            {
                using (var connection = SQLPROVIDER.CreateConnection(connectionSTR))
                using (var command = new SqlCommand(query, connection))
                {
                    AddNamedParameters(command, query, parameter);
                    connection.Open();
                    return command.ExecuteScalar() ?? DBNull.Value;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteScalar B7R2");
                return DBNull.Value;
            }
        }
    }
    class WMSPROVIDER
    {

        static string host = "192.168.2.10";
        static int port = 8088;
        static string sid = "ORCL";
        //static string sid = "FCCSTG"; // Tét 
        static string user = "wms";
        static string password = "wms";
        //static string password = "IFSAPP";

        private string connectionSTR = @"Data Source=(DESCRIPTION =(ADDRESS = (PROTOCOL = TCP)(HOST = "
                 + host + ")(PORT = " + port + "))(CONNECT_DATA = (SERVER = DEDICATED)(SERVICE_NAME = "
                 + sid + ")));Password=" + password + ";User ID=" + user;

        public DataTable WMSExecuteQuery(string query, object[] parameter = null)
        {
            DataTable data = new DataTable();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);

                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery IFS");
            }
            return data;
        }

        public DataSet WMSExecuteQuery_Dataset(string query, object[] parameter = null)
        {
            DataSet data = new DataSet();
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    OracleDataAdapter adapter = new OracleDataAdapter(command);

                    adapter.Fill(data);

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery_Dataset IFS");
            }
            return data;
        }

        public int WMSExecuteNonQuery(string query, object[] parameter = null)
        {
            int data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteNonQuery();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteNonQuery IFS");
            }
            return data;
        }

        public object WMSExecuteScalar(string query, object[] parameter = null)
        {
            object data = 0;
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);

                    if (parameter != null)
                    {
                        string[] listPara = query.Split(' ');
                        int i = 0;
                        foreach (string item in listPara)
                        {
                            if (item.Contains('@'))
                            {
                                command.Parameters.Add(item, parameter[i]);
                                i++;
                            }
                        }
                    }

                    data = command.ExecuteScalar();

                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteScalar IFS");
            }
            return data;
        }

        public string WMSExecuteReader(string query)
        {
            DataTable data = new DataTable();

            string _value = "";
            try
            {
                using (OracleConnection connection = new OracleConnection(connectionSTR))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(query, connection);


                    OracleDataReader MyReader = command.ExecuteReader();



                    while (MyReader.Read())
                    {

                        _value = String.Format("{0}", MyReader[0]);
                    }


                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi ExecuteQuery");
            }
            return _value;
        }


        // kết nối SQL và WMS where house 4W

    }
}