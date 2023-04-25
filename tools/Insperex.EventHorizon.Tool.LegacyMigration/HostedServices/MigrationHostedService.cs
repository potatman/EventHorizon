using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Insperex.EventHorizon.Tool.LegacyMigration.HostedServices
{
    public class MigrationHostedService : BackgroundService
    {
        private readonly IMongoClient _mongoClient;
        private readonly StreamingClient _streamingClient;
        private readonly IStreamFactory _streamFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<string, string> _bucketToTopic;

        public MigrationHostedService(IMongoClient mongoClient, StreamingClient streamingClient, IStreamFactory streamFactory, ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _mongoClient = mongoClient;
            _streamingClient = streamingClient;
            _streamFactory = streamFactory;
            _loggerFactory = loggerFactory;
            _bucketToTopic = configuration.GetSection("Migration").GetChildren().ToDictionary(x => x.Key, x => x.Value);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Used to Start Over
            foreach (var item in _bucketToTopic)
                await ResetAsync(item.Key, item.Value, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var results = _bucketToTopic.AsParallel()
                    .Select(item => RunAsync(item.Key, item.Value, stoppingToken))
                    .ToArray();

                await Task.WhenAll(results);
            }
        }

        private async Task ResetAsync(string bucketId, string topic, CancellationToken ct)
        {
            var dataSource = new MongoDbSource(_mongoClient, bucketId, _loggerFactory.CreateLogger<MongoDbSource>());
            using var publisher = _streamingClient.CreatePublisher<Event>().AddTopic(topic).Build();

            // TEMP: Delete Existing Topic
            await _streamFactory.CreateAdmin().DeleteTopicAsync(topic, ct);
            await dataSource.DeleteState(ct);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        private async Task RunAsync(string bucketId, string topic, CancellationToken ct)
        {
            var dataSource = new MongoDbSource(_mongoClient, bucketId, _loggerFactory.CreateLogger<MongoDbSource>());
            using var publisher = _streamingClient.CreatePublisher<Event>().AddTopic(topic).Build();

            if (!await dataSource.AnyAsync(ct))
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return;
            }

            await foreach (var item in dataSource.GetAsyncEnumerator(ct))
            {
                await publisher.PublishAsync(item);
                await dataSource.SaveState(ct);
            }
        }
    }
}
