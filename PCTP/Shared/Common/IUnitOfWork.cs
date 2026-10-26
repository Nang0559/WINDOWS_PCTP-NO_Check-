using PCTP.ClassSQL;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Common
{
    public interface IUnitOfWork : IDisposable
    {
        SqlConnection Connection { get; }
        SqlTransaction Transaction { get; }
        bool IsInTransaction { get; }
        void Begin();
        void Commit();
        void Rollback();
    }
}
