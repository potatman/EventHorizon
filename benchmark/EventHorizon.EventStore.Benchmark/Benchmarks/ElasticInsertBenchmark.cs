using EventHorizon.EventStore.Benchmark.Base;
using EventHorizon.EventStore.Test.Util;

namespace EventHorizon.EventStore.Benchmark.Benchmarks;

public class ElasticInsertBenchmark : BaseInsertBenchmark
{
    public ElasticInsertBenchmark() : base(HostTestUtil.GetElasticHost(null).Services)
    {
    }
}
