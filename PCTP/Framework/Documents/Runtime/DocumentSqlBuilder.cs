using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PCTP.Framework.Documents
{
    public sealed class DocumentSqlParameter
    {
        public string Name { get; set; }
        public SqlDbType DbType { get; set; }
        public int Size { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public object Value { get; set; }
    }

    public sealed class DocumentSqlStatement
    {
        public string Sql { get; set; }
        public List<DocumentSqlParameter> Parameters { get; private set; }
        public DocumentSqlStatement() { Parameters = new List<DocumentSqlParameter>(); }
    }

    /// <summary>
    /// Pure SQL text builder for document reads. Every identifier comes from the trusted definition
    /// (validated at registration) and every value is a parameter, so it has no connection and can be unit-tested.
    /// </summary>
    public static class DocumentSqlBuilder
    {
        public const string SiteParameter = "Site";
        public const string KeyParameter = "Key";

        public static DocumentSqlStatement BuildList(DocumentFormDefinition document, DocumentListQuery query,
            DocumentDataScope scope, bool countOnly)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (query == null) throw new ArgumentNullException("query");
            if (query.PageIndex < 0) throw new ArgumentException("PageIndex must not be negative.", "query");
            if (query.PageSize < 1 || query.PageSize > DocumentListQuery.MaxPageSize)
                throw new ArgumentException("PageSize must be between 1 and " + DocumentListQuery.MaxPageSize + ".", "query");
            if (query.Filters.Count > DocumentListQuery.MaxFilters)
                throw new ArgumentException("Too many filters.", "query");

            var statement = new DocumentSqlStatement();
            AddSite(statement);
            var where = new StringBuilder(Q(document.SiteField) + "=@" + SiteParameter);
            AppendDepartmentFilter(document, scope, statement, where, null);

            int index = 0;
            foreach (var filter in query.Filters)
                where.Append(" AND ").Append(BuildFilter(document, filter, statement, index++));

            if (countOnly)
            {
                statement.Sql = "SELECT COUNT(1) FROM " + document.HeaderView + " WHERE " + where;
                return statement;
            }

            string orderField = document.KeyField;
            if (!string.IsNullOrWhiteSpace(query.OrderByField))
            {
                var field = document.FindField(DocumentRegion.Header, query.OrderByField);
                if (field == null) throw new ArgumentException("Order field is not registered: " + query.OrderByField, "query");
                orderField = field.FieldCode;
            }
            string direction = query.Descending ? " DESC" : " ASC";
            string order = Q(orderField) + direction;
            if (!string.Equals(orderField, document.KeyField, StringComparison.OrdinalIgnoreCase))
                order += "," + Q(document.KeyField) + direction; // stable paging

            statement.Parameters.Add(new DocumentSqlParameter { Name = "Skip", DbType = SqlDbType.Int, Value = query.PageIndex * query.PageSize });
            statement.Parameters.Add(new DocumentSqlParameter { Name = "Take", DbType = SqlDbType.Int, Value = query.PageSize });
            statement.Sql = "SELECT " + Columns(document, DocumentRegion.Header) + " FROM " + document.HeaderView +
                " WHERE " + where + " ORDER BY " + order + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
            return statement;
        }

        public static DocumentSqlStatement BuildHeaderByKey(DocumentFormDefinition document, object key, DocumentDataScope scope)
        {
            var statement = new DocumentSqlStatement();
            AddSite(statement);
            AddKey(statement, key);
            var where = new StringBuilder(Q(document.SiteField) + "=@" + SiteParameter +
                " AND " + Q(document.KeyField) + "=@" + KeyParameter);
            AppendDepartmentFilter(document, scope, statement, where, null);
            statement.Sql = "SELECT " + Columns(document, DocumentRegion.Header) + " FROM " + document.HeaderView + " WHERE " + where;
            return statement;
        }

        public static DocumentSqlStatement BuildDetailByKey(DocumentFormDefinition document, object key, DocumentDataScope scope)
        {
            var statement = new DocumentSqlStatement();
            AddSite(statement);
            AddKey(statement, key);
            if (!NeedsDepartmentFilter(document, scope))
            {
                statement.Sql = "SELECT " + Columns(document, DocumentRegion.Detail) + " FROM " + document.DetailView +
                    " WHERE " + Q(document.SiteField) + "=@" + SiteParameter + " AND " + Q(document.KeyField) + "=@" + KeyParameter;
                return statement;
            }

            // The detail view has no department column; scope it through the owning header row.
            var departments = new StringBuilder();
            AppendDepartmentFilter(document, scope, statement, departments, "h");
            statement.Sql = "SELECT " + Columns(document, DocumentRegion.Detail, "d") + " FROM " + document.DetailView + " d" +
                " WHERE d." + Q(document.SiteField) + "=@" + SiteParameter + " AND d." + Q(document.KeyField) + "=@" + KeyParameter +
                " AND EXISTS (SELECT 1 FROM " + document.HeaderView + " h" +
                " WHERE h." + Q(document.KeyField) + "=d." + Q(document.KeyField) +
                " AND h." + Q(document.SiteField) + "=d." + Q(document.SiteField) + departments + ")";
            return statement;
        }

        public static bool NeedsDepartmentFilter(DocumentFormDefinition document, DocumentDataScope scope)
        {
            return document.DepartmentField != null && (scope == null || !scope.IsAllDepartments);
        }

        private static void AppendDepartmentFilter(DocumentFormDefinition document, DocumentDataScope scope,
            DocumentSqlStatement statement, StringBuilder where, string alias)
        {
            if (!NeedsDepartmentFilter(document, scope)) return;
            // Fail closed: a document with a DepartmentField and no granted department returns nothing at all.
            if (scope == null || scope.Departments.Count == 0)
                throw new UnauthorizedAccessException("Chưa được cấp phạm vi phòng ban cho chứng từ " + document.ModuleCode + ".");
            var names = new List<string>();
            int i = 0;
            foreach (string department in scope.Departments.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string name = "D" + i++;
                names.Add("@" + name);
                statement.Parameters.Add(new DocumentSqlParameter { Name = name, DbType = SqlDbType.NVarChar, Size = 50, Value = department });
            }
            if (where.Length > 0 || alias != null) where.Append(" AND ");
            where.Append(alias == null ? string.Empty : alias + ".").Append(Q(document.DepartmentField))
                .Append(" IN (").Append(string.Join(",", names)).Append(")");
        }

        private static string BuildFilter(DocumentFormDefinition document, DocumentFilter filter,
            DocumentSqlStatement statement, int index)
        {
            if (filter == null || string.IsNullOrWhiteSpace(filter.FieldCode))
                throw new ArgumentException("Filter field is required.");
            var field = document.FindField(DocumentRegion.Header, filter.FieldCode);
            if (field == null) throw new ArgumentException("Filter field is not registered: " + filter.FieldCode);
            if (filter.Value == null || filter.Value == DBNull.Value)
                throw new ArgumentException("Filter value is required for " + field.FieldCode + ".");
            string name = "F" + index;
            string column = Q(field.FieldCode);

            switch (field.Type)
            {
                case DocumentFieldType.Number:
                    RequireOp(field, filter.Op, DocumentFilterOp.Equals, DocumentFilterOp.GreaterOrEqual, DocumentFilterOp.LessOrEqual);
                    AddTyped(statement, name, SqlDbType.Decimal, 38, 10, ToDecimal(filter.Value, field));
                    break;
                case DocumentFieldType.Date:
                case DocumentFieldType.DateTime:
                    RequireOp(field, filter.Op, DocumentFilterOp.Equals, DocumentFilterOp.GreaterOrEqual, DocumentFilterOp.LessOrEqual);
                    AddTyped(statement, name, SqlDbType.DateTime2, 0, 0, ToDate(filter.Value, field));
                    break;
                case DocumentFieldType.Check:
                    RequireOp(field, filter.Op, DocumentFilterOp.Equals);
                    AddTyped(statement, name, SqlDbType.Bit, 0, 0, Convert.ToBoolean(filter.Value, CultureInfo.InvariantCulture));
                    break;
                case DocumentFieldType.Memo:
                    throw new ArgumentException("Field " + field.FieldCode + " cannot be filtered.");
                default:
                    RequireOp(field, filter.Op, DocumentFilterOp.Equals, DocumentFilterOp.Contains);
                    string text = Convert.ToString(filter.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (text.Length == 0 || text.Length > 200) throw new ArgumentException("Filter text length is invalid for " + field.FieldCode + ".");
                    if (filter.Op == DocumentFilterOp.Contains) text = "%" + EscapeLike(text) + "%";
                    AddTyped(statement, name, SqlDbType.NVarChar, 400, 0, text);
                    break;
            }

            switch (filter.Op)
            {
                case DocumentFilterOp.Equals: return column + "=@" + name;
                case DocumentFilterOp.Contains: return column + " LIKE @" + name + " ESCAPE '\\'";
                case DocumentFilterOp.GreaterOrEqual: return column + ">=@" + name;
                default: return column + "<=@" + name;
            }
        }

        private static void RequireOp(DocumentFieldDefinition field, DocumentFilterOp op, params DocumentFilterOp[] allowed)
        {
            if (!allowed.Contains(op))
                throw new ArgumentException("Operator " + op + " is not allowed for field " + field.FieldCode + ".");
        }

        public static string EscapeLike(string text)
        {
            return text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
        }

        private static decimal ToDecimal(object value, DocumentFieldDefinition field)
        {
            try { return Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            { throw new ArgumentException("Filter value is not a number for " + field.FieldCode + ".", ex); }
        }

        private static DateTime ToDate(object value, DocumentFieldDefinition field)
        {
            try { return Convert.ToDateTime(value, CultureInfo.InvariantCulture); }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException)
            { throw new ArgumentException("Filter value is not a date for " + field.FieldCode + ".", ex); }
        }

        private static void AddTyped(DocumentSqlStatement statement, string name, SqlDbType type, int sizeOrPrecision, byte scale, object value)
        {
            var parameter = new DocumentSqlParameter { Name = name, DbType = type, Value = value };
            if (type == SqlDbType.Decimal) { parameter.Precision = (byte)sizeOrPrecision; parameter.Scale = scale; }
            else parameter.Size = sizeOrPrecision;
            statement.Parameters.Add(parameter);
        }

        private static void AddSite(DocumentSqlStatement statement)
        {
            statement.Parameters.Add(new DocumentSqlParameter { Name = SiteParameter, DbType = SqlDbType.NVarChar, Size = 30 });
        }

        private static void AddKey(DocumentSqlStatement statement, object key)
        {
            if (key == null || key == DBNull.Value) throw new ArgumentException("Khóa chứng từ là bắt buộc.", "key");
            statement.Parameters.Add(new DocumentSqlParameter { Name = KeyParameter, DbType = SqlDbType.Variant /* provider infers the type from the CLR value */, Value = key });
        }

        private static string Columns(DocumentFormDefinition document, DocumentRegion region, string alias = null)
        {
            string prefix = alias == null ? string.Empty : alias + ".";
            return string.Join(",", document.FieldsOf(region).Select(f => prefix + Q(f.FieldCode)));
        }

        private static string Q(string identifier)
        {
            return DocumentIdentifiers.Bracket(identifier);
        }
    }
}
