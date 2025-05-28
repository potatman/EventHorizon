using EventHorizon.EventStore.Benchmark.Base;
using EventHorizon.EventStore.Test.Util;

namespace EventHorizon.EventStore.Benchmark.Benchmarks;

public class MongoInsertBenchmark : BaseInsertBenchmark
{
    public MongoInsertBenchmark() : base(HostTestUtil.GetMongoDbHost(null).Services)
    {
    }
}
