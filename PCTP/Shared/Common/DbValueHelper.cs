using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Common
{
    public static class DbValueHelper
    {
        public static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;

            return int.TryParse(
                value.ToString(),
                out int result)
                ? result
                : 0;
        }

        public static string ToString(object value)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            return value.ToString();
        }

        public static DateTime? ToDateTime(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            return DateTime.TryParse(
                value.ToString(),
                out DateTime result)
                ? result
                : (DateTime?)null;
        }

        public static bool ToBool(object value)
        {
            if (value == null || value == DBNull.Value)
                return false;

            if (value is bool boolValue)
                return boolValue;

            return bool.TryParse(
                value.ToString(),
                out bool result)
                && result;
        }

        public static decimal ToDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0m;

            return decimal.TryParse(
                value.ToString(),
                out decimal result)
                ? result
                : 0m;
        }

        public static long ToLong(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0L;

            return long.TryParse(
                value.ToString(),
                out long result)
                ? result
                : 0L;
        }

        public static object DbValue(object value)
        {
            return value ?? DBNull.Value;
        }
        public static string GetString(
        DataRow row,
        string columnName)
        {
            if (row == null)
                return "";

            if (!row.Table.Columns.Contains(columnName))
                return "";

            if (row[columnName] == DBNull.Value)
                return "";

            return row[columnName]?.ToString() ?? "";
        }
        public static int SafeInt(object value) { if (value == null || value == DBNull.Value) { return 0; } return int.TryParse(value.ToString(), out int result) ? result : 0; }
        public static string SafeString(object value) { if (value == null || value == DBNull.Value) { return string.Empty; } return value.ToString(); }
        public static DateTime SafeDate(
           object value)
        {
            if (value == null ||
                value == DBNull.Value)
            {
                return DateTime.MinValue;
            }

            if (value is DateTime dateTime)
                return dateTime;

            return DateTime.TryParse(
                value.ToString(),
                out DateTime parsed)
                    ? parsed
                    : DateTime.MinValue;
        }
    }
}
