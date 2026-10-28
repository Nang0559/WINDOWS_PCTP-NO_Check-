using System;
using System.Data.Common;

namespace PCTP.ClassSQL
{
    /// <summary>
    /// Test-only redirection of the application's SQL connection strings to a disposable fixture database.
    ///
    /// Normal launches are unaffected: when PCTP_QC_SQL_CONNECTION is not set, Resolve returns the
    /// compiled-in default exactly as before. When it IS set the override is fail-closed:
    ///   * PCTP_QC_ISOLATED_FIXTURE must be "1" (explicit opt-in, set by the CI runner scripts);
    ///   * the server must be local to the machine (localhost, 127.0.0.1, ., (local), (localdb));
    ///   * the database name must start with PCTP_QC_ (never B7R2_FCC);
    ///   * the text must not mention DB57 (192.168.200.57) or production (192.168.200.14 / BRAVO).
    /// Any violation throws, so the application stops at startup instead of silently falling back to DB57.
    /// </summary>
    internal static class QcSqlOverride
    {
        public const string ConnectionEnvVar = "PCTP_QC_SQL_CONNECTION";
        public const string FixtureConfirmEnvVar = "PCTP_QC_ISOLATED_FIXTURE";

        private static readonly string[] ServerKeys = { "data source", "server", "address", "addr", "network address" };
        private static readonly string[] DatabaseKeys = { "initial catalog", "database" };
        private static readonly string[] LocalHosts = { "localhost", "127.0.0.1", ".", "(local)", "(localdb)", "::1" };

        public static string Resolve(string compiledDefault)
        {
            return Resolve(compiledDefault,
                Environment.GetEnvironmentVariable(ConnectionEnvVar),
                Environment.GetEnvironmentVariable(FixtureConfirmEnvVar));
        }

        internal static string Resolve(string compiledDefault, string overrideConnection, string fixtureConfirm)
        {
            if (string.IsNullOrWhiteSpace(overrideConnection))
                return compiledDefault;

            string reason = Validate(overrideConnection, fixtureConfirm);
            if (reason != null)
                throw new InvalidOperationException(
                    "PCTP_QC_SQL_CONNECTION rejected (" + reason + "). The QC override only accepts a local PCTP_QC_* fixture database.");
            return overrideConnection;
        }

        /// <summary>Returns null when the connection string is an acceptable isolated fixture, otherwise the reason.</summary>
        internal static string Validate(string connectionString, string fixtureConfirm)
        {
            if (!string.Equals((fixtureConfirm ?? string.Empty).Trim(), "1", StringComparison.Ordinal))
                return FixtureConfirmEnvVar + " is not 1";

            string lower = connectionString.ToLowerInvariant();
            if (lower.Contains("192.168.200.57") || lower.Contains("192.168.200.14") || lower.Contains("bravo")
                || lower.Contains("b7r2_fcc"))
                return "mentions DB57 / production / B7R2_FCC";

            var builder = new DbConnectionStringBuilder();
            try { builder.ConnectionString = connectionString; }
            catch (ArgumentException) { return "not a parsable connection string"; }

            string server = FirstValue(builder, ServerKeys);
            string database = FirstValue(builder, DatabaseKeys);
            if (string.IsNullOrWhiteSpace(server)) return "no server";
            if (string.IsNullOrWhiteSpace(database)) return "no database";
            if (builder.ContainsKey("attachdbfilename") || builder.ContainsKey("failover partner"))
                return "AttachDbFilename / Failover Partner not allowed";

            if (!IsLocalHost(server)) return "server '" + server + "' is not local";
            if (!database.StartsWith("PCTP_QC_", StringComparison.OrdinalIgnoreCase))
                return "database '" + database + "' does not start with PCTP_QC_";
            return null;
        }

        private static string FirstValue(DbConnectionStringBuilder b, string[] keys)
        {
            foreach (string k in keys)
            {
                object v;
                if (b.TryGetValue(k, out v) && v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                    return v.ToString().Trim();
            }
            return null;
        }

        private static bool IsLocalHost(string server)
        {
            string host = server.Trim();
            if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) host = host.Substring(4);
            int cut = host.IndexOfAny(new[] { '\\', ',' });
            if (cut >= 0) host = host.Substring(0, cut);
            host = host.Trim();
            foreach (string allowed in LocalHosts)
                if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
