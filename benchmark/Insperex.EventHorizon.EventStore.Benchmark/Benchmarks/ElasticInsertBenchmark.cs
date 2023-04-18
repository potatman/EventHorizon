using Insperex.EventHorizon.EventStore.Benchmark.Base;
using Insperex.EventHorizon.EventStore.Test.Util;

namespace Insperex.EventHorizon.EventStore.Benchmark.Benchmarks;

public class ElasticInsertBenchmark : BaseInsertBenchmark
{
    public ElasticInsertBenchmark() : base(HostTestUtil.GetElasticHost(null).Services)
    {
    }
}