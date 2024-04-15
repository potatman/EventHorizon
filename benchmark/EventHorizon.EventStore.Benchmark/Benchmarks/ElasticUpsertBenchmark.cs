using EventHorizon.EventStore.Benchmark.Base;
using EventHorizon.EventStore.Test.Util;

namespace EventHorizon.EventStore.Benchmark.Benchmarks;

public class ElasticUpsertBenchmark : BaseUpsertBenchmark
{
    public ElasticUpsertBenchmark() : base(HostTestUtil.GetElasticHost(null).Services)
    {
    }
}
