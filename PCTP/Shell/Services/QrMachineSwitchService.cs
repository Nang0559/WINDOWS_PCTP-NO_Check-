using System;

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

            string current = currentMachine ?? string.Empty;
            string target = targetMachine.Trim();
            string history = string.Format("{0} --> {1} : {2}", current, target, DateTime.Now);

            string safeHistory = history.Replace("'", "''");
            string safeTarget = target.Replace("'", "''");

            string updateSql =
                "update tbl_QR_MAY_DOCQR set LichSu = '" + safeHistory + "', TT = 0 where TT = 1";
            string insertSql =
                "insert into tbl_QR_MAY_DOCQR(TenMay,LichSu,TT) values ('" + safeTarget + "','KO',1)";

            _sql.LoadData1(_sql.B7R2_FCCdb, updateSql);
            _sql.LoadData1(_sql.B7R2_FCCdb, insertSql);
        }
    }
}
