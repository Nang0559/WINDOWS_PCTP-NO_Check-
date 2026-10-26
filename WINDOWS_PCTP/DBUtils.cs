using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace Tutorial.SqlConn
{
    class DBUtils
    {
        public static SqlConnection GetDBConnection()
        {
            string datasource = @"192.168.200.57,1433";

            string database = "B7R2_FCC";
            string username = "sa";
            string password = "fccbrv";

            return DBSQLServerUtils.GetDBConnection(datasource, database, username, password);
        }
    }

}