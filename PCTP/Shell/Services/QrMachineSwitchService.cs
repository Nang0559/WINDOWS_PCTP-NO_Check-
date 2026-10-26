using System;
using System.Data.SqlClient;

namespace PCTP.Shell.Services
{
    /// <summary>
    /// Owns the legacy QR-machine assignment update.
    /// Main_APP only handles user confirmation and application restart.
    /// </summary>
    internal sealed class QrMachineSwitchService
    {
        private readonly ClassSQL.SQLPROVIDER _sql;

        internal QrMachineSwitchService()
        {
            _sql = new ClassSQL.SQLPROVIDER();
        }

        internal void Switch(string currentMachine, string targetMachine)
        {
            if (string.IsNullOrWhiteSpace(targetMachine))
                throw new ArgumentException("targetMachine");

            string current = (currentMachine ?? string.Empty).Trim();
            string target = targetMachine.Trim();

            if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
                return;

            string history = string.Format(
                "{0} --> {1} : {2}",
                current,
                target,
                DateTime.Now);

            // tbl_QR_MAY_DOCQR.LichSu is historically limited on some deployments.
            // Keep the audit text within the legacy column size.
            if (history.Length > 100)
                history = history.Substring(0, 100);

            using (var conn = _sql.BeginTransaction(_sql.B7R2_FCCdb, out SqlTransaction tran))
            {
                try
                {
                    // Only one machine may own TT=1.
                    _sql.ExecuteNonQuery(
                        conn,
                        tran,
                        "UPDATE tbl_QR_MAY_DOCQR SET TT = 0 WHERE TT = 1");

                    // The target machine may already have a row with TT=0.
                    // The old implementation always INSERTed, which could fail
                    // on a PK/UNIQUE constraint and leave the switch unchanged.
                    object existsObj = _sql.ExecuteScalar(
                        conn,
                        tran,
                        "SELECT COUNT(*) FROM tbl_QR_MAY_DOCQR WHERE TenMay = @tenMay",
                        new[] { new SqlParameter("@tenMay", target) });

                    bool exists = Convert.ToInt32(existsObj) > 0;

                    if (exists)
                    {
                        _sql.ExecuteNonQuery(
                            conn,
                            tran,
                            @"UPDATE tbl_QR_MAY_DOCQR
                              SET TT = 1,
                                  LichSu = @lichSu
                              WHERE TenMay = @tenMay",
                            new SqlParameter("@lichSu", history),
                            new SqlParameter("@tenMay", target));
                    }
                    else
                    {
                        _sql.ExecuteNonQuery(
                            conn,
                            tran,
                            @"INSERT INTO tbl_QR_MAY_DOCQR (TenMay, LichSu, TT)
                              VALUES (@tenMay, @lichSu, 1)",
                            new SqlParameter("@tenMay", target),
                            new SqlParameter("@lichSu", history));
                    }

                    tran.Commit();
                }
                catch
                {
                    try
                    {
                        tran.Rollback();
                    }
                    catch
                    {
                        // Preserve the original database exception.
                    }

                    throw;
                }
            }
        }
    }
}
