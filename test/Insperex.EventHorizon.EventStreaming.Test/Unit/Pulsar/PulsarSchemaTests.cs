using System;
using System.Collections.Generic;
using Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;
using Pulsar.Client.Api;
using Xunit;

namespace Insperex.EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class PulsarSchemaTests
{
    [Fact]
    public void CreateStreamStateSchema()
    {
        var schema = Schema.JSON<StreamState>();
        Assert.NotNull(schema);
    }
}
