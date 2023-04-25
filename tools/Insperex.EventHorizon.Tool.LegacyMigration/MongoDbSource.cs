using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.Tool.LegacyMigration.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

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
                BatchSize = 10000
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
                    .Select(item =>
                    {
                        var streamId = item["StreamId"].AsString;
                        var type = item["Type"].AsString;
                        var payload = item["Payload"].AsBsonDocument.ToString();
                        var eventDateTime = item["EventDateTime"].AsBsonDateTime;
                        var sourceDateTime = item["SourceDateTime"].AsBsonDateTime;
                        // var obj = JsonSerializer.Deserialize<dynamic>(payload);
                        return new Event { StreamId = streamId, Type = type, Payload = payload };
                    })
                    .ToArray();
            }
            _currentBatch = Array.Empty<BsonDocument>();
        }

        private async Task<IAsyncCursor<BsonDocument>> GetCursor(CancellationToken ct)
        {
            var filter1 = Builders<Cursor>.Filter.Eq("_id", AssemblyUtil.AssemblyName);
            var cursor = await _cursors.Find(filter1).FirstOrDefaultAsync(ct);

            var filter2 = cursor == null ? Builders<BsonDocument>.Filter.Empty :
                Builders<BsonDocument>.Filter.Gt("EventDateTime", AssemblyUtil.AssemblyName);

            return await _client.GetDatabase(_bucketId).GetCollection<BsonDocument>(nameof(Event))
                .Find(filter2, _findOptions)
                .SortBy(x => x["EventDateTime"])
                .ThenBy(x => x["StreamId"])
                .ToCursorAsync(ct);

        }

        public async Task SaveState(CancellationToken ct)
        {
            var filter = Builders<Cursor>.Filter.Eq("_id", AssemblyUtil.AssemblyName);
            if (_currentBatch.Any())
            {
                var max = _currentBatch.Max(x => x["EventDateTime"].AsDateTime);
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
