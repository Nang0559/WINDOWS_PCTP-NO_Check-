using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Domain.Events
{
    public class HoanThanhYMVNCompletedEvent : DomainEvent
    {
        public DataTable Result { get; }
        public bool CoDuLieu => Result != null && Result.Rows.Count > 0;

        public HoanThanhYMVNCompletedEvent(DataTable result)
        {
            Result = result ?? new DataTable();
        }
    }
}
