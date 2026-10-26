using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.Enums
{
    public enum IfsOrderQueryMode
    {
        ByFactoryAndHour,   // dùng GetCustomerOrderJoin (nhà máy + giờ)
        ByDock              // dùng GetCustomerOrderJoin... theo dock (hiện là "YMVN")
    }
}
