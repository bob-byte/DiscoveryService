using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.Interfaces.Extensions
{
    public static class TimerExtensions
    {
        public static void Stop( this Timer timer ) =>
            timer.Change( dueTime: Timeout.Infinite, period: Timeout.Infinite );
    }
}
