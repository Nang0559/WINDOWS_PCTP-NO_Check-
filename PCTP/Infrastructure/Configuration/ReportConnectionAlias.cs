using System;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace PCTP.Infrastructure.Configuration
{
    /// <summary>
    /// B7R2_FCCdb in PCTP.Database.config is the only SQL Server declaration of the application.
    /// The DevExpress report designers (IN_GHP_LOT, IN_GHP_LOT_YMVN, PHIEUGIAOHANG_EXP, NG_DETAIL) bind their
    /// SqlDataSource by the fixed name "accfast\bravo.B7R2_FCC.dbo", which needs the XPO connection-string
    /// dialect. This class derives that XPO string from B7R2_FCCdb at start-up, so the alias can never point
    /// at a different server than the canonical connection.
    /// Call Apply() once, right after RuntimeConfigurationBootstrap.Initialize().
    /// </summary>
    internal static class ReportConnectionAlias
    {
        internal const string CanonicalName = "B7R2_FCCdb";
        internal const string AliasName = "accfast\\bravo.B7R2_FCC.dbo";

        private static readonly object Gate = new object();
        private static bool _applied;

        internal static void Apply()
        {
            lock (Gate)
            {
                if (_applied) return;
                _applied = true;

                ConnectionStringSettings canonical = ConfigurationManager.ConnectionStrings[CanonicalName];
                ConnectionStringSettings alias = ConfigurationManager.ConnectionStrings[AliasName];
                if (canonical == null || alias == null || string.IsNullOrWhiteSpace(canonical.ConnectionString))
                    return;
                // Template placeholders: SQLPROVIDER reports that clearly; do not derive from them.
                if (canonical.ConnectionString.IndexOf("YOUR_SQL_", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                string xpo = ToXpo(canonical.ConnectionString);
                if (xpo == null) return;

                FieldInfo readOnly = typeof(ConfigurationElement).GetField(
                    "_bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                if (readOnly != null) readOnly.SetValue(alias, false);
                alias.ConnectionString = xpo;
                if (readOnly != null) readOnly.SetValue(alias, true);
            }
        }

        internal static string ToXpo(string sqlClientConnectionString)
        {
            var src = new DbConnectionStringBuilder { ConnectionString = sqlClientConnectionString };
            string server = Get(src, "data source", "server", "address", "addr", "network address");
            string catalog = Get(src, "initial catalog", "database");
            if (server == null || catalog == null) return null;

            var xpo = new DbConnectionStringBuilder();
            xpo["XpoProvider"] = "MSSqlServer";
            xpo["data source"] = server;
            string user = Get(src, "user id", "uid", "user");
            if (user != null)
            {
                xpo["user id"] = user;
                xpo["password"] = Get(src, "password", "pwd") ?? string.Empty;
            }
            else
            {
                xpo["Integrated Security"] = "SSPI";
            }
            xpo["initial catalog"] = catalog;
            xpo["Persist Security Info"] = "true";
            return xpo.ConnectionString;
        }

        private static string Get(DbConnectionStringBuilder b, params string[] names)
        {
            foreach (string name in names)
            {
                string key = b.Keys.Cast<string>().FirstOrDefault(
                    k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
                object v;
                if (key != null && b.TryGetValue(key, out v) && v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                    return v.ToString().Trim();
            }
            return null;
        }
    }
}
