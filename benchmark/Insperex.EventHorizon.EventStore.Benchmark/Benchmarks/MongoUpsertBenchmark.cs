using Insperex.EventHorizon.EventStore.Benchmark.Base;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Test.Util;

namespace Insperex.EventHorizon.EventStore.Benchmark.Benchmarks;

public class MongoUpsertBenchmark : BaseUpsertBenchmark
{
    public MongoUpsertBenchmark() : base(HostTestUtil.GetMongoDbHost(null).Services)
    {
    }
}
