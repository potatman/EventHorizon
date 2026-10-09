using System;
using System.Linq;
using MongoDB.Bson;

namespace EventHorizon.EventStore.MongoDb.Indexes;

/// <summary>
/// A desired collection index. <see cref="Keys"/> uses the createIndexes key format, e.g.
/// <c>{ "State.Sku": 1 }</c> or <c>{ "State.Name": "text", "State.Description": "text" }</c>.
/// </summary>
internal sealed record MongoIndexSpec(string Name, BsonDocument Keys, TimeSpan? ExpireAfter = null)
{
    public bool IsText => Keys.Elements.Any(x => x.Value.IsString && x.Value.AsString == "text");
}
