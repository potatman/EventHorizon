using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Util;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class Sender
{
    private readonly SenderConfig _config;
    private readonly SenderSubscriptionTracker _subscriptionTracker;
    private readonly StreamingClient _streamingClient;

    public Sender(SenderSubscriptionTracker subscriptionTracker, StreamingClient streamingClient, SenderConfig config)
    {
        _subscriptionTracker = subscriptionTracker;
        _streamingClient = streamingClient;
        _config = config;
    }

    public Task SendAsync<T>(string streamId, params ICommand<T>[] objs) where T : IState
    {
        var commands = objs.Select(x => new Command(streamId, x)).ToArray();
        return SendAsync<T>(commands);
    }

    public Task SendAsync<T>(params Command[] commands) where T : IState
    {
        return _streamingClient.CreatePublisher<Command>()
            .AddStream<T>().Build()
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
        // Ensure subscription is ready
        await _subscriptionTracker.TrackSubscription<T>();

        // Sent SenderId to respond to
        foreach (var request in requests)
            request.SenderId = _subscriptionTracker.GetSenderId();

        // Send requests
        var requestDict = requests.ToDictionary(x => x.Id);
        await _streamingClient.CreatePublisher<Request>().AddStream<T>().Build().PublishAsync(requests);

        // Wait for messages
        var sw = Stopwatch.StartNew();
        var responseDict = new Dictionary<string, Response>();
        var requestIds = requestDict.Values.Select(x => x.Id).ToArray();
        while (responseDict.Count != requestDict.Count
               && sw.ElapsedMilliseconds < _config.Timeout.TotalMilliseconds)
        {
            var responses = _subscriptionTracker.GetResponses(requestIds, _config.GetErrorResult);
            foreach (var response in responses)
                responseDict[response.RequestId] = response;
            await Task.Delay(200);
        }

        // Add Timed Out Results
        foreach (var request in requestDict.Values)
            if (!responseDict.ContainsKey(request.Id))
                responseDict[request.Id] = new Response(request.StreamId, request.Id, _subscriptionTracker.GetSenderId(),
                    _config.GetErrorResult(AggregateStatus.CommandTimedOut, string.Empty)) { Status = AggregateStatus.CommandTimedOut};

        return responseDict.Values.ToArray();
    }

}
