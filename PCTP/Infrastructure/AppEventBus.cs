using PCTP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Infrastructure
{
    public static class AppEventBus
    {
        public static readonly IEventBus Instance = new InProcessEventBus();
    }
}
