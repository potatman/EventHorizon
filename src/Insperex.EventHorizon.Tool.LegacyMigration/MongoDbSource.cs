using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.Abstractions.Serialization;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.Tool.LegacyMigration.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Insperex.EventHorizon.Tool.LegacyMigration
{
    public class MongoDbSource
    {
        private readonly IMongoClient _client;
        private readonly string _bucketId;
        private readonly ILogger<MongoDbSource> _logger;
        private readonly FindOptions _findOptions;
        private readonly IMongoCollection<Cursor> _cursors;
        private BsonDocument[] _currentBatch;

        public MongoDbSource(IMongoClient client, string bucketId, ILogger<MongoDbSource> logger)
        {
            _client = client;
            _bucketId = bucketId;
            _logger = logger;
            _findOptions = new FindOptions
            {
                AllowPartialResults = false,
                BatchSize = 20000
            };
            _cursors = _client.GetDatabase(_bucketId).GetCollection<Cursor>(nameof(Cursor));
        }

        public async Task<bool> AnyAsync(CancellationToken ct)
        {
            var asyncCursor = await GetCursor(ct);
            await asyncCursor.MoveNextAsync(ct);
            return asyncCursor.Current.Any();
        }

        public async IAsyncEnumerable<Event[]> GetAsyncEnumerator([EnumeratorCancellation] CancellationToken ct = default)
        {
            var asyncCursor = await GetCursor(ct);
            while (await asyncCursor.MoveNextAsync(ct))
            {
                _currentBatch = asyncCursor.Current.ToArray();
                _logger.LogInformation("{BucketId} found batch {Count}", _bucketId, _currentBatch.Length);

                // Return Mapped Batch
                yield return _currentBatch
                    .AsParallel()
                    .Select(item =>
                    {
                        var streamId = item["StreamId"].AsString;
                        var type = item["Type"].AsString;

                        // Convert Payload to Json
                        var dotnetValue = BsonTypeMapper.MapToDotNetValue(item["Payload"]);
                        var payload = SerializationConstants.Serializer.Serialize(dotnetValue);

                        // Handle TradeHistory
                        var tradeHistoryBucketIds = new[] { "tec_raw_esp_trade", "tec_raw_msrb_trade", "tec_raw_finra_trade" };
                        if (tradeHistoryBucketIds.Contains(_bucketId))
                            return GetTradeHistoryEvent(streamId, type, payload);

                        return GetEvent(streamId, type, payload);
                    })
                    .Where(x => x != null)
                    .ToArray();
            }
            _currentBatch = Array.Empty<BsonDocument>();
        }

        private static Event GetEvent(string streamId, string type, string payload)
        {
            // Remove _t
            var obj = SerializationConstants.Serializer.Deserialize<JObject>(payload);
            obj.Property("_t")?.Remove();
            var json = SerializationConstants.Serializer.Serialize(obj);

            return new Event { StreamId = streamId, Type = type, Payload = json };
        }

        private static Event GetTradeHistoryEvent(string streamId, string type, string payload)
        {
            // Remove _t
            var obj = SerializationConstants.Serializer.Deserialize<JObject>(payload);
            var propertyNames = obj.Properties().Select(x => x.Name).ToArray();
            foreach (var propertyName in propertyNames)
                if(propertyName != "RawMessage" && propertyName != "MessageSource")
                    obj.Property(propertyName)?.Remove();
            var json = SerializationConstants.Serializer.Serialize(obj);

            if (type == "EspSaveRawMessageEvent") type = "EspRawEvent";
            if (type == "FinraTraceSaveRawMessageEvent") type = "FinraRawEvent";
            if (type == "MsrbSaveRawMessageEvent") type = "MsrbRawEvent";

            return new Event { StreamId = streamId, Type = type, Payload = json };
        }

        private async Task<IAsyncCursor<BsonDocument>> GetCursor(CancellationToken ct)
        {
            var filter1 = Builders<Cursor>.Filter.Eq("_id", AssemblyUtil.AssemblyName);
            var cursor = await _cursors.Find(filter1).FirstOrDefaultAsync(ct);

            var filter2 = cursor == null ? Builders<BsonDocument>.Filter.Empty :
                Builders<BsonDocument>.Filter.Gt("EventDateTime", cursor.EventDateTime);

            var session = await _client.StartSessionAsync(cancellationToken: ct);
            var query = session.Client.GetDatabase(_bucketId).GetCollection<BsonDocument>(nameof(Event))
                .WithReadPreference(ReadPreference.Nearest)
                .Find(filter2, _findOptions)
                .SortBy(x => x["EventDateTime"]);
            return await query.ToCursorAsync(ct);
        }

        public async Task SaveState(CancellationToken ct)
        {
            var filter = Builders<Cursor>.Filter.Eq("_id", AssemblyUtil.AssemblyName);
            if (_currentBatch.Any())
            {
                var max = _currentBatch.Max(x => x["EventDateTime"].ToUniversalTime());
                var cursor = new Cursor
                {
                    Id = AssemblyUtil.AssemblyName,
                    IsActive = _currentBatch.Any(),
                    IsPaused = false,
                    Types = null,
                    EventDateTime = max,
                    UpdatedDateTime = DateTime.UtcNow
                };
                await _cursors.ReplaceOneAsync(filter, cursor, new ReplaceOptions{ IsUpsert = true }, cancellationToken: ct);
            }
            else
            {
                var update = Builders<Cursor>.Update.Set("IsActive", false);
                await _cursors.UpdateOneAsync(filter, update, cancellationToken: ct);
            }
        }

        public async Task DeleteState(CancellationToken ct)
        {
            var filter = Builders<Cursor>.Filter.Eq("_id", AssemblyUtil.AssemblyName);
            await _cursors.DeleteOneAsync(filter, ct);
        }
    }
}
