using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Insperex.EventHorizon.Tool.LegacyMigration
{
    public class MigrationHostedService : BackgroundService
    {
        private readonly IMongoClient _mongoClient;
        private readonly StreamingClient _streamingClient;
        private readonly IStreamFactory _streamFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _bucketId;
        private readonly string _topic;
        private readonly IDataSource _dataSource;

        public MigrationHostedService(IMongoClient mongoClient, StreamingClient streamingClient, IStreamFactory streamFactory, ILoggerFactory loggerFactory)
        {
            _mongoClient = mongoClient;
            _streamingClient = streamingClient;
            _streamFactory = streamFactory;
            _loggerFactory = loggerFactory;
            _bucketId = "tec_event_firm";
            _topic = $"persistent://legacy/events/{_bucketId}";
            _dataSource = new MongoDbSource(_mongoClient, _bucketId, _loggerFactory.CreateLogger<MongoDbSource>());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // TEMP: Delete Existing Topic
            await _streamFactory.CreateAdmin().DeleteTopicAsync(_topic, stoppingToken);
            await _dataSource.DeleteState(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

            while (true)
            {
                if (!await _dataSource.AnyAsync(stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                using var publisher = _streamingClient.CreatePublisher<Event>().AddTopic(_topic).Build();
                await foreach (var item in _dataSource.GetAsyncEnumerator(stoppingToken))
                {
                    await publisher.PublishAsync(item);
                    await _dataSource.SaveState(stoppingToken);
                }
            }
        }
    }
}
