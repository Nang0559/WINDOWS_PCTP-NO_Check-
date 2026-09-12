using PCTP.Domain.Interfaces;
using PCTP.VIEWSTOCK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Modules.GiaoHangKhach.Services
{
    public interface IOrderLoadStrategyFactory
    {
        IOrderLoadStrategy GetStrategy(OrderLoadContext cfg);
    }
}
