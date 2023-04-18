using Insperex.EventHorizon.EventStore.Benchmark.Base;
using Insperex.EventHorizon.EventStore.Test.Util;

namespace Insperex.EventHorizon.EventStore.Benchmark.Benchmarks;

public class MongoInsertBenchmark : BaseInsertBenchmark
{
    public MongoInsertBenchmark() : base(HostTestUtil.GetMongoDbHost(null).Services)
    {
    }
}