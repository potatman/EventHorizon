using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Insperex.EventHorizon.Tool.LegacyMigration
{
    public class MigrationHostedService : BackgroundService
    {
        private readonly IMongoClient _mongoClient;
        private readonly StreamingClient _streamingClient;
        private readonly IStreamFactory _streamFactory;
        private readonly string _bucketId;
        private readonly string _topic;

        public MigrationHostedService(IMongoClient mongoClient, StreamingClient streamingClient, IStreamFactory streamFactory)
        {
            _mongoClient = mongoClient;
            _streamingClient = streamingClient;
            _streamFactory = streamFactory;
            _bucketId = "tec_event_firm";
            _topic = $"persistent://legacy/events/{_bucketId}";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var filter = Builders<BsonDocument>.Filter.Empty;

            // Delete Existing Topic
            await _streamFactory.CreateAdmin().DeleteTopicAsync(_topic, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            using var publisher = _streamingClient.CreatePublisher<Event>().AddTopic(_topic).Build();

            while (true)
            {
                try
                {
                    var cursor = await GetCursor(filter);
                    while (await cursor.MoveNextAsync(stoppingToken))
                    {
                        var events = new List<Event>();
                        var batch = cursor.Current.ToArray();
                        foreach (var item  in batch)
                        {
                            var streamId = item["StreamId"].AsString;
                            var type = item["Type"].AsString;
                            var payload = item["Payload"].AsBsonDocument.ToString();
                            var eventDateTime = item["EventDateTime"].AsBsonDateTime;
                            var sourceDateTime = item["SourceDateTime"].AsBsonDateTime;
                            // var obj = JsonSerializer.Deserialize<dynamic>(payload);

                            events.Add(new Event { StreamId = streamId, Type = type, Payload = payload });
                        }

                        await publisher.PublishAsync(events.ToArray());
                    }

                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private Task<IAsyncCursor<BsonDocument>> GetCursor(FilterDefinition<BsonDocument> filter)
        {
            var options = new FindOptions
            {
                BatchSize = 10000
            };

            return _mongoClient.GetDatabase(_bucketId).GetCollection<BsonDocument>(nameof(Event))
                .Find(filter, options)
                .SortBy(x => x["EventDateTime"])
                .ThenBy(x => x["StreamId"])
                .ToCursorAsync();
        }
    }
}
