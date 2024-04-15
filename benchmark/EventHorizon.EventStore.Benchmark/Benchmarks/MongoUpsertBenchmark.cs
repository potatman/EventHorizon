using EventHorizon.EventStore.Benchmark.Base;
using EventHorizon.EventStore.Test.Util;

namespace EventHorizon.EventStore.Benchmark.Benchmarks;

public class MongoUpsertBenchmark : BaseUpsertBenchmark
{
    public MongoUpsertBenchmark() : base(HostTestUtil.GetMongoDbHost(null).Services)
    {
    }
}
