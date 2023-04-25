using System;
using Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;
using NBench;

namespace Insperex.EventHorizon.EventStreaming.Benchmark;

public class Program
{
    static int Main(string[] args)
    {
        var run = NBenchRunner.Run<Program>();

        // Note: cleans up data
        PulsarSingleton.Instance.Dispose();
        // GC.Collect();
        // GC.WaitForPendingFinalizers();

        return run;
    }
}
