using System;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;
using NBench;

namespace Insperex.EventHorizon.EventStreaming.Benchmark;

public class Program
{
    static async Task<int> Main(string[] args)
    {
        var run = NBenchRunner.Run<Program>();

        // Note: cleans up data
        await PulsarSingleton.Instance.DisposeAsync();
        // GC.Collect();
        // GC.WaitForPendingFinalizers();

        return run;
    }
}
