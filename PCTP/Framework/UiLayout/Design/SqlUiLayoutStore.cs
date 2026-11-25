using DevExpress.XtraPrinting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PCTP.Framework.UiLayout
{
    public interface IUiLayoutStore
    {
        IList<UiDataSourceInfo> ListSources(long actorUserId);
        IList<UiSourceColumn> GetSourceColumns(long actorUserId, string sourceCode);
        IList<string> ListCandidateViews(long actorUserId);
        void ApproveSource(long actorUserId, string actorCode, UiDataSourceInfo source);
        void CreateScreen(long actorUserId, string actorCode, string screenCode, string title, string sourceCode);
        UiScreenLoadResult Load(long actorUserId, string screenCode);
        /// <summary>Returns the new ConfigVersion. Throws <see cref="UiConfigConflictException"/> on a version mismatch.</summary>
        int Save(long actorUserId, string actorCode, UiScreenLayout layout, int expectedConfigVersion);
    }

    /// <summary>Calls migration 056 procedures. Not a security boundary: every procedure re-checks RBAC and re-validates names.</summary>
    public sealed class SqlUiLayoutStore : IUiLayoutStore
    {
        private const int ConfigVersionConflict = 56131;
        private readonly string _connectionString;

        public SqlUiLayoutStore(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public IList<UiDataSourceInfo> ListSources(long actorUserId)
        {
            var table = Fill("MES.usp_FVN_UiDataSource_List", p => p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId);
            return table.Rows.Cast<DataRow>().Select(r => new UiDataSourceInfo
            {
                SourceCode = (string)r["SourceCode"],
                ViewName = (string)r["ViewName"],
                DisplayName = (string)r["DisplayName"],
                ViewFunctionCode = (string)r["ViewFunctionCode"],
                SiteColumn = (string)r["SiteColumn"],
                IsActive = (bool)r["IsActive"]
            }).ToList();
        }

        public IList<UiSourceColumn> GetSourceColumns(long actorUserId, string sourceCode)
        {
            var table = Fill("MES.usp_FVN_UiDataSource_Columns", p =>
            {
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                p.Add("@SourceCode", SqlDbType.VarChar, 60).Value = sourceCode;
            });
            return ToColumns(table.Rows.Cast<DataRow>());
        }

        public IList<string> ListCandidateViews(long actorUserId)
        {
            var table = Fill("MES.usp_FVN_UiDataSource_Candidates", p => p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId);
            return table.Rows.Cast<DataRow>().Select(r => (string)r["ViewName"]).ToList();
        }

        public void ApproveSource(long actorUserId, string actorCode, UiDataSourceInfo source)
        {
            if (source == null) throw new ArgumentNullException("source");
            Execute("MES.usp_FVN_UiDataSource_Approve", p =>
            {
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                p.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                p.Add("@SourceCode", SqlDbType.VarChar, 60).Value = source.SourceCode;
                p.Add("@ViewName", SqlDbType.NVarChar, 128).Value = source.ViewName;
                p.Add("@DisplayName", SqlDbType.NVarChar, 160).Value = source.DisplayName;
                p.Add("@ViewFunctionCode", SqlDbType.VarChar, 100).Value = source.ViewFunctionCode;
                p.Add("@SiteColumn", SqlDbType.NVarChar, 128).Value = source.SiteColumn;
                p.Add("@IsActive", SqlDbType.Bit).Value = source.IsActive;
            });
        }

        public void CreateScreen(long actorUserId, string actorCode, string screenCode, string title, string sourceCode)
        {
            Execute("MES.usp_FVN_UiScreen_Create", p =>
            {
                p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                p.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                p.Add("@ScreenCode", SqlDbType.VarChar, 60).Value = screenCode;
                p.Add("@Title", SqlDbType.NVarChar, 160).Value = title;
                p.Add("@SourceCode", SqlDbType.VarChar, 60).Value = sourceCode;
            });
        }

        public UiScreenLoadResult Load(long actorUserId, string screenCode)
        {
            var ds = new DataSet();
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand("MES.usp_FVN_UiScreen_Load", cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                cmd.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                cmd.Parameters.Add("@ScreenCode", SqlDbType.VarChar, 60).Value = screenCode;
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(ds);
            }
            if (ds.Tables.Count < 5 || ds.Tables[0].Rows.Count != 1)
                throw new InvalidOperationException("Procedure MES.usp_FVN_UiScreen_Load trả về dữ liệu không hợp lệ.");

            var h = ds.Tables[0].Rows[0];
            var layout = new UiScreenLayout
            {
                ScreenCode = (string)h["ScreenCode"],
                Title = (string)h["Title"],
                SourceCode = (string)h["SourceCode"],
                ViewName = (string)h["ViewName"],
                ViewFunctionCode = (string)h["ViewFunctionCode"],
                SiteColumn = (string)h["SiteColumn"],
                DefaultSortColumn = h["DefaultSortColumn"] as string,
                DefaultSortDesc = (bool)h["DefaultSortDesc"],
                GroupColumn = h["GroupColumn"] as string,
                PageSize = (int)h["PageSize"],
                ConfigVersion = (int)h["ConfigVersion"]
            };
            foreach (DataRow r in ds.Tables[1].Rows)
                layout.Columns.Add(new UiColumnLayout
                {
                    RegionCode = (string)r["RegionCode"],
                    ColumnName = (string)r["ColumnName"],
                    Caption = (string)r["Caption"],
                    SequenceNo = (int)r["SequenceNo"],
                    Width = r["Width"] == DBNull.Value ? (int?)null : (int)r["Width"],
                    Align = (string)r["Align"],
                    DisplayFormat = r["DisplayFormat"] as string,
                    IsVisible = (bool)r["IsVisible"]
                });
            foreach (DataRow r in ds.Tables[2].Rows)
                layout.Children.Add(new UiChildLayout
                {
                    ChildCode = (string)r["ChildCode"],
                    TabCaption = (string)r["TabCaption"],
                    SourceCode = (string)r["SourceCode"],
                    ViewName = (string)r["ViewName"],
                    ViewFunctionCode = (string)r["ViewFunctionCode"],
                    SiteColumn = (string)r["SiteColumn"],
                    ParentColumn = (string)r["ParentColumn"],
                    ChildColumn = (string)r["ChildColumn"],
                    SortColumn = r["SortColumn"] as string,
                    SortDesc = (bool)r["SortDesc"],
                    SequenceNo = (int)r["SequenceNo"]
                });
            foreach (DataRow r in ds.Tables[3].Rows)
                layout.Filters.Add(new UiQuickFilter
                {
                    FilterCode = (string)r["FilterCode"],
                    Caption = (string)r["Caption"],
                    ColumnName = (string)r["ColumnName"],
                    OperatorCode = (string)r["OperatorCode"],
                    TokenCode = r["TokenCode"] as string,
                    LiteralValue = r["LiteralValue"] as string,
                    SequenceNo = (int)r["SequenceNo"],
                    IsDefault = (bool)r["IsDefault"]
                });

            var result = new UiScreenLoadResult { Layout = layout };
            foreach (var group in ds.Tables[4].Rows.Cast<DataRow>().GroupBy(r => (string)r["RegionCode"], StringComparer.OrdinalIgnoreCase))
                result.Catalog[group.Key] = ToColumns(group).ToList();
            return result;
        }

        public int Save(long actorUserId, string actorCode, UiScreenLayout layout, int expectedConfigVersion)
        {
            if (layout == null) throw new ArgumentNullException("layout");
            string columns = JsonConvert.SerializeObject(layout.Columns.Select(c => new
            {
                c.RegionCode,
                c.ColumnName,
                c.Caption,
                c.SequenceNo,
                c.Width,
                c.Align,
                c.DisplayFormat,
                c.IsVisible
            }));
            string children = JsonConvert.SerializeObject(layout.Children.Select(c => new
            {
                c.ChildCode,
                c.TabCaption,
                c.SourceCode,
                c.ParentColumn,
                c.ChildColumn,
                c.SortColumn,
                c.SortDesc,
                c.SequenceNo
            }));
            string filters = JsonConvert.SerializeObject(layout.Filters.Select(f => new
            {
                f.FilterCode,
                f.Caption,
                f.ColumnName,
                f.OperatorCode,
                f.TokenCode,
                f.LiteralValue,
                f.SequenceNo,
                f.IsDefault
            }));
            try
            {
                var table = Fill("MES.usp_FVN_UiScreen_Save", p =>
                {
                    p.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
                    p.Add("@ActorCode", SqlDbType.NVarChar, 100).Value = actorCode ?? string.Empty;
                    p.Add("@ScreenCode", SqlDbType.VarChar, 60).Value = layout.ScreenCode;
                    p.Add("@ExpectedConfigVersion", SqlDbType.Int).Value = expectedConfigVersion;
                    p.Add("@Title", SqlDbType.NVarChar, 160).Value = layout.Title;
                    p.Add("@DefaultSortColumn", SqlDbType.NVarChar, 128).Value = (object)layout.DefaultSortColumn ?? DBNull.Value;
                    p.Add("@DefaultSortDesc", SqlDbType.Bit).Value = layout.DefaultSortDesc;
                    p.Add("@GroupColumn", SqlDbType.NVarChar, 128).Value = (object)layout.GroupColumn ?? DBNull.Value;
                    p.Add("@PageSize", SqlDbType.Int).Value = layout.PageSize;
                    p.Add("@ColumnsJson", SqlDbType.NVarChar, -1).Value = columns;
                    p.Add("@ChildrenJson", SqlDbType.NVarChar, -1).Value = children;
                    p.Add("@FiltersJson", SqlDbType.NVarChar, -1).Value = filters;
                });
                return (int)table.Rows[0][0];
            }
            catch (SqlException ex) when (ex.Number == ConfigVersionConflict)
            {
                throw new UiConfigConflictException(ex.Message, ex);
            }
        }

        private static IEnumerable<UiSourceColumn> ToColumns(IEnumerable<DataRow> rows)
        {
            return rows.Select(r => new UiSourceColumn
            {
                ColumnName = (string)r["ColumnName"],
                TypeName = (string)r["TypeName"],
                TypeGroup = (string)r["TypeGroup"],
                IsNullable = (bool)r["IsNullable"]
            });
        }

        private DataTable Fill(string procedure, Action<SqlParameterCollection> parameters)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(procedure, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                parameters(cmd.Parameters);
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        private void Execute(string procedure, Action<SqlParameterCollection> parameters)
        {
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(procedure, cn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;
                parameters(cmd.Parameters);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Executes commands produced by <see cref="UiQueryBuilder"/> only. Read-only, parameterized.</summary>
    public sealed class SqlUiDataReader
    {
        private readonly string _connectionString;

        public SqlUiDataReader(string connectionString = null)
        {
            _connectionString = connectionString ?? ConfigurationManager.ConnectionStrings["FVN_PCTP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Thiếu connection string FVN_PCTP.");
        }

        public DataTable Query(UiSqlCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            using (var cn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(command.Sql, cn))
            {
                cmd.CommandTimeout = 60;
                foreach (var p in command.Parameters)
                    cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(table);
                return table;
            }
        }

        public long Count(UiSqlCommand command)
        {
            return Convert.ToInt64(Query(command).Rows[0][0]);
        }
    }
}