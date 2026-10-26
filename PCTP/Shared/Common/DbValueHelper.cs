using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Common
{
    using System;
    using System.Data;

    public static class DbValueHelper
    {
        // ============================================================
        // VALUE -> SQL PARAMETER
        // ============================================================

        public static object DbValue(object value)
        {
            return value ?? DBNull.Value;
        }


        // ============================================================
        // OBJECT -> INT
        // ============================================================

        public static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;

            int result;

            return int.TryParse(
                value.ToString(),
                out result)
                ? result
                : 0;
        }


        // ============================================================
        // OBJECT -> INT?
        // ============================================================

        public static int? ToNullableInt(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            int result;

            return int.TryParse(
                value.ToString(),
                out result)
                ? (int?)result
                : null;
        }


        // ============================================================
        // OBJECT -> LONG
        // ============================================================

        public static long ToLong(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0L;

            long result;

            return long.TryParse(
                value.ToString(),
                out result)
                ? result
                : 0L;
        }


        // ============================================================
        // OBJECT -> DECIMAL
        // ============================================================

        public static decimal ToDecimal(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0m;

            decimal result;

            return decimal.TryParse(
                value.ToString(),
                out result)
                ? result
                : 0m;
        }


        // ============================================================
        // OBJECT -> STRING
        // ============================================================

        public static string ToString(object value)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            return value.ToString();
        }


        // ============================================================
        // OBJECT -> DATETIME?
        // ============================================================

        public static DateTime? ToDateTime(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is DateTime)
                return (DateTime)value;

            DateTime result;

            return DateTime.TryParse(
                value.ToString(),
                out result)
                ? (DateTime?)result
                : null;
        }


        // ============================================================
        // OBJECT -> DATETIME
        // ============================================================

        public static DateTime ToDateTimeValue(object value)
        {
            DateTime? result = ToDateTime(value);

            return result.HasValue
                ? result.Value
                : DateTime.MinValue;
        }


        // ============================================================
        // OBJECT -> BOOL
        // ============================================================

        public static bool ToBool(object value)
        {
            if (value == null || value == DBNull.Value)
                return false;

            if (value is bool)
                return (bool)value;

            bool result;

            return bool.TryParse(
                value.ToString(),
                out result)
                && result;
        }


        // ============================================================
        // OBJECT -> ENUM
        // ============================================================

        public static TEnum ToEnum<TEnum>(
            object value,
            TEnum defaultValue = default(TEnum))
            where TEnum : struct
        {
            if (value == null || value == DBNull.Value)
                return defaultValue;

            if (value is TEnum)
                return (TEnum)value;

            int intValue;

            if (int.TryParse(value.ToString(), out intValue))
            {
                return (TEnum)Enum.ToObject(
                    typeof(TEnum),
                    intValue);
            }

            TEnum enumValue;

            return Enum.TryParse<TEnum>(
                value.ToString(),
                true,
                out enumValue)
                ? enumValue
                : defaultValue;
        }


        // ============================================================
        // OBJECT -> ENUM?
        // ============================================================

        public static TEnum? ToNullableEnum<TEnum>(
            object value)
            where TEnum : struct
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is TEnum)
                return (TEnum)value;

            int intValue;

            if (int.TryParse(value.ToString(), out intValue))
            {
                return (TEnum)Enum.ToObject(
                    typeof(TEnum),
                    intValue);
            }

            TEnum enumValue;

            if (Enum.TryParse<TEnum>(
                value.ToString(),
                true,
                out enumValue))
            {
                return enumValue;
            }

            return null;
        }


        // ============================================================
        // DATAROW -> STRING
        // ============================================================

        public static string GetString(
            DataRow row,
            string columnName)
        {
            if (row == null)
                return string.Empty;

            if (row.Table == null ||
                !row.Table.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            return ToString(row[columnName]);
        }


        // ============================================================
        // DATAROW -> INT
        // ============================================================

        public static int GetInt(
            DataRow row,
            string columnName)
        {
            if (row == null)
                return 0;

            if (row.Table == null ||
                !row.Table.Columns.Contains(columnName))
            {
                return 0;
            }

            return ToInt(row[columnName]);
        }


        // ============================================================
        // DATAROW -> INT?
        // ============================================================

        public static int? GetNullableInt(
            DataRow row,
            string columnName)
        {
            if (row == null)
                return null;

            if (row.Table == null ||
                !row.Table.Columns.Contains(columnName))
            {
                return null;
            }

            return ToNullableInt(row[columnName]);
        }


        // ============================================================
        // DATAROW -> DATETIME?
        // ============================================================

        public static DateTime? GetNullableDateTime(
            DataRow row,
            string columnName)
        {
            if (row == null)
                return null;

            if (row.Table == null ||
                !row.Table.Columns.Contains(columnName))
            {
                return null;
            }

            return ToDateTime(row[columnName]);
        }


        // ============================================================
        // DATAROW -> DATETIME
        // ============================================================

        public static DateTime GetDateTime(
            DataRow row,
            string columnName)
        {
            if (row == null)
                return DateTime.MinValue;

            if (row.Table == null ||
                !row.Table.Columns.Contains(columnName))
            {
                return DateTime.MinValue;
            }

            return ToDateTimeValue(row[columnName]);
        }


        // ============================================================
        // DATAROW -> ENUM
        // ============================================================

        public static TEnum GetEnum<TEnum>(
            DataRow row,
            string columnName,
            TEnum defaultValue = default(TEnum))
            where TEnum : struct
        {
            if (row == null)
                return defaultValue;

            if (row.Table == null ||
                !row.Table.Columns.Contains(columnName))
            {
                return defaultValue;
            }

            return ToEnum(
                row[columnName],
                defaultValue);
        }


        // ============================================================
        // DATAROW -> ENUM?
        // ============================================================

        public static TEnum? GetNullableEnum<TEnum>(
            DataRow row,
            string columnName)
            where TEnum : struct
        {
            if (row == null)
                return null;

            if (row.Table == null ||
                !row.Table.Columns.Contains(columnName))
            {
                return null;
            }

            return ToNullableEnum<TEnum>(
                row[columnName]);
        }


        // ============================================================
        // ALIAS - GIỮ TƯƠNG THÍCH CODE CŨ
        // ============================================================

        public static int SafeInt(object value)
        {
            return ToInt(value);
        }


        public static string SafeString(object value)
        {
            return ToString(value);
        }


        public static DateTime SafeDate(object value)
        {
            return ToDateTimeValue(value);
        }
    }
}
