using System;
using System.Linq;

namespace PCTP.Framework.Documents
{
    /// <summary>
    /// Strict, ASCII-only validators for every identifier that can reach SQL or a permission check.
    /// Nothing outside these validators may turn a string into an object name.
    /// </summary>
    internal static class DocumentIdentifiers
    {
        public static string Code(string value, string name)
        {
            return Normalize(value, name, false, true);
        }

        /// <summary>Function/permission codes such as MES.COMPONENT_ISSUE (dots allowed).</summary>
        public static string FunctionCode(string value, string name)
        {
            return Normalize(value, name, true, true);
        }

        /// <summary>Column / parameter identifier; case is preserved.</summary>
        public static string Identifier(string value, string name)
        {
            string v = Normalize(value, name, false, false);
            if (char.IsDigit(v[0])) throw new ArgumentException("Identifier must not start with a digit: " + name, name);
            return v;
        }

        /// <summary>Two-part schema.object name, returned bracket-quoted: [MES].[vw_X].</summary>
        public static string SqlObject(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(name + " is required.", name);
            var parts = value.Trim().Split('.');
            if (parts.Length != 2 || parts.Any(p => p.Length == 0 || p.Length > 128 || !p.All(IsWordChar)))
                throw new ArgumentException("SQL object must be a two-part schema-qualified identifier.", name);
            return "[" + parts[0] + "].[" + parts[1] + "]";
        }

        public static string Bracket(string identifier)
        {
            return "[" + identifier + "]";
        }

        private static string Normalize(string value, string name, bool allowDot, bool upper)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Invalid registered code: " + name, name);
            string v = value.Trim();
            if (v.Length > 100 || v.Any(c => !(IsWordChar(c) || (allowDot && c == '.'))))
                throw new ArgumentException("Invalid registered code: " + name, name);
            return upper ? v.ToUpperInvariant() : v;
        }

        private static bool IsWordChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
        }
    }
}
