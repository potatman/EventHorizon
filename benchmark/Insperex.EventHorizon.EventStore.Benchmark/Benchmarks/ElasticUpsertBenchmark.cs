using Insperex.EventHorizon.EventStore.Benchmark.Base;
using Insperex.EventHorizon.EventStore.Test.Util;

namespace Insperex.EventHorizon.EventStore.Benchmark.Benchmarks;

public class ElasticUpsertBenchmark : BaseUpsertBenchmark
{
    public ElasticUpsertBenchmark() : base(HostTestUtil.GetElasticHost(null).Services)
    {
    }
}
