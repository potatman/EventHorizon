using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Util;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class Sender
{
    private readonly SenderConfig _config;
    private readonly ILogger<Sender> _logger;
    private readonly StreamingClient _streamingClient;
    private readonly string _senderId;

    public Sender(StreamingClient streamingClient, SenderConfig config, ILogger<Sender> logger)
    {
        _streamingClient = streamingClient;
        _config = config;
        _logger = logger;
        _senderId = NameUtil.AssemblyNameWithGuid;
    }

    public Task SendAsync<T>(string streamId, params ICommand<T>[] objs) where T : IState
    {
        var commands = objs.Select(x => new Command(streamId, x)).ToArray();
        return SendAsync<T>(commands);
    }

    public Task SendAsync<T>(params Command[] commands) where T : IState
    {
        return _streamingClient.CreatePublisher<Command>()
            .AddTopic<T>().Build()
            .PublishAsync(commands);
    }

    public async Task<TR> SendAndReceiveAsync<T, TR>(string streamId, IRequest<T, TR> obj)
        where T : IState
        where TR : IResponse<T>
    {
        var results = await SendAndReceiveAsync(streamId, new[] { obj });
        return results.First();
    }

    public async Task<TR[]> SendAndReceiveAsync<T, TR>(string streamId, IRequest<T, TR>[] objs)
        where T : IState
        where TR : IResponse<T>
    {
        var requests = objs.Select(x => new Request(streamId, x)).ToArray();
        var res = await SendAndReceiveAsync<T>(requests);
        return res.Select(x => JsonSerializer.Deserialize<TR>(x.Payload)).ToArray();
    }

    private async Task<Response[]> SendAndReceiveAsync<T>(Request[] requests) where T : IState
    {
        // Sent SenderId to respond to
        foreach (var request in requests)
            request.SenderId = _senderId;

        // Send requests
        await _streamingClient.CreatePublisher<Request>().AddTopic<T>().Build().PublishAsync(requests);

        // Setup 2
        var responseDict = new Dictionary<string, Response>();
        var requestDict = requests.ToDictionary(x => x.Id);

        // Wait Until Results or Count is Finished
        var sw = Stopwatch.StartNew();
        while (responseDict.Count != requestDict.Count
               && sw.ElapsedMilliseconds < _config.Timeout.TotalMilliseconds)
        {
            // Get Batch Results
            var resultReader = _streamingClient.CreateReader<Response>().AddTopic<T>(_senderId)
                .Keys(requests.Select(x => x.StreamId).ToArray()).IsBeginning(true).Build();
            var results = await resultReader.GetNextAsync(requests.Length);

            // Check Results
            foreach (var result in results)
            {
                _logger.LogInformation("Found Result");
                var requestId = result.Data.RequestId;
                responseDict[requestId] = result.Data.Status != AggregateStatus.Ok
                    ? new Response(result.Data.StreamId, requestId, _senderId,
                        _config.GetErrorResult(result.Data.Status, result.Data.Error)) { Status = result.Data.Status }
                    : result.Data;
            }
        }

        // Add Timed Out Results
        foreach (var request in requestDict.Values)
            if (!responseDict.ContainsKey(request.Id))
                responseDict[request.Id] = new Response(request.StreamId, request.Id, _senderId,
                    _config.GetErrorResult(AggregateStatus.CommandTimedOut, string.Empty)) { Status = AggregateStatus.CommandTimedOut};

        return responseDict.Values.ToArray();
    }
}