using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.FuctionMain
{
    public static class SWLog
    {
        public static T Measure<T>(string label, Func<T> action)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                return action();
            }
            finally
            {
                sw.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[PERF] {label}: {sw.ElapsedMilliseconds}ms");
            }
        }

        public static void Measure(string label, Action action)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try { action(); }
            finally
            {
                sw.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[PERF] {label}: {sw.ElapsedMilliseconds}ms");
            }
        }
    }
}
