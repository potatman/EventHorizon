using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace EventHorizon.EventStreaming.Test.Util;

public static class WaitUtil
{
    public static async Task WaitForTrue(Func<bool> func, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (func() == false && stopwatch.ElapsedMilliseconds < timeout.TotalMilliseconds)
            await Task.Delay(200);
    }
}
