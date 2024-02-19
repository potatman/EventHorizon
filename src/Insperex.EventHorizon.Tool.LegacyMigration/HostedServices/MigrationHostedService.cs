using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStore.MongoDb.Models;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly ILogger<MigrationHostedService> _logger;
        private static int _count;

        public MigrationHostedService(IOptions<MongoConfig> mongoOptions, StreamingClient streamingClient, IStreamFactory streamFactory, ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _mongoClient = new MongoClient(mongoOptions.Value.ConnectionString);
            _streamingClient = streamingClient;
            _streamFactory = streamFactory;
            _loggerFactory = loggerFactory;
            _bucketToTopic = configuration.GetSection("Migration").GetChildren().ToDictionary(x => x.Key, x => x.Value);
            _logger = _loggerFactory.CreateLogger<MigrationHostedService>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Used to Start Over
            if(false)
                foreach (var item in _bucketToTopic)
                    await ResetAsync(item.Key, item.Value, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var kvp in _bucketToTopic)
                        await RunAsync(kvp.Key, kvp.Value, stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private async Task ResetAsync(string bucketId, string topic, CancellationToken ct)
        {
            var dataSource = new MongoDbSource(_mongoClient, bucketId, _loggerFactory.CreateLogger<MongoDbSource>());

            // TEMP: Delete Existing Topic
            await _streamFactory.CreateAdmin<Event>().DeleteTopicAsync(topic, ct);
            await dataSource.DeleteState(ct);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        private async Task RunAsync(string bucketId, string topic, CancellationToken ct)
        {
            try
            {
                await _streamFactory.CreateAdmin<Event>().RequireTopicAsync(topic, ct);
                _logger.LogInformation("{bucketId} Starting {topic}", bucketId, topic);
                var dataSource = new MongoDbSource(_mongoClient, bucketId, _loggerFactory.CreateLogger<MongoDbSource>());
                await using var publisher = _streamingClient.CreatePublisher<Event>().AddTopic(topic).Build();

                if (!await dataSource.AnyAsync(ct))
                    return;

                await foreach (var item in dataSource.GetAsyncEnumerator(ct))
                {
                    await publisher.PublishAsync(item);
                    await dataSource.SaveState(ct);
                    _count += item.Length;
                    _logger.LogInformation("{bucketId} Total Sent: {Count}", bucketId, _count);
                }
                _logger.LogInformation("{bucketId} Stopping {topic}", bucketId, topic);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }

            // var channel = Channel.CreateBounded<Event>(new BoundedChannelOptions(1000000) { FullMode = BoundedChannelFullMode.Wait });
            // var task1 = LoadChannel(bucketId, channel.Writer, ct);
            // var task2 = WriteChannel(channel.Reader, publisher);
            // await Task.WhenAll(task1, task2);
        }

        private async Task LoadChannel(string bucketId, ChannelWriter<Event> writer, CancellationToken ct)
        {
            var dataSource = new MongoDbSource(_mongoClient, bucketId, _loggerFactory.CreateLogger<MongoDbSource>());
            await foreach (var batch in dataSource.GetAsyncEnumerator(ct))
            {
                foreach (var item in batch)
                    await writer.WriteAsync(item, ct);
                await dataSource.SaveState(ct);
            }
        }

        private async Task WriteChannel(string bucketId, ChannelReader<Event> reader, Publisher<Event> publisher)
        {
            var size = 10000;
            while (true)
            {
                var list = new List<Event>();
                for (var i = 0; i < size; i++)
                {
                    var item = await reader.ReadAsync();
                    list.Add(item);
                }

                await publisher.PublishAsync(list.ToArray());
                _count += list.Count;
                _logger.LogInformation("{bucketId} Total Sent: {Count}", bucketId, _count);
            }
        }
    }
}
