using EventHorizon.EventStore.Benchmark.Base;
using EventHorizon.EventStore.Test.Util;

namespace EventHorizon.EventStore.Benchmark.Benchmarks;

public class IgniteInsertBenchmark : BaseInsertBenchmark
{
    public IgniteInsertBenchmark() : base(HostTestUtil.GetIgniteHost(null).Services)
    {
    }
}
