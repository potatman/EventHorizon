using Insperex.EventHorizon.EventStore.Benchmark.Base;
using Insperex.EventHorizon.EventStore.Test.Util;

namespace Insperex.EventHorizon.EventStore.Benchmark.Benchmarks;

public class IgniteInsertBenchmark : BaseInsertBenchmark
{
    public IgniteInsertBenchmark() : base(HostTestUtil.GetIgniteHost(null).Services)
    {
    }
}