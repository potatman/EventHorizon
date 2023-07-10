using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Extensions;
using Insperex.EventHorizon.EventStreaming.Publishers;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class Sender
{
    private readonly SenderConfig _config;
    private readonly SenderSubscriptionTracker _subscriptionTracker;
    private readonly StreamingClient _streamingClient;
    private readonly Dictionary<string, object> _publisherDict = new();

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
        return GetPublisher<Command, T>(null).PublishAsync(commands);
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

    public async Task<Response[]> SendAndReceiveAsync<T>(params Request[] requests) where T : IState
    {
        // Ensure subscription is ready
        await _subscriptionTracker.TrackSubscription<T>();

        // Sent SenderId to respond to
        foreach (var request in requests)
            request.SenderId = _subscriptionTracker.GetSenderId();

        // Send requests
        var requestDict = requests.ToDictionary(x => x.Id);
        await GetPublisher<Request, T>(null).PublishAsync(requests);

        // Wait for messages
        var sw = Stopwatch.StartNew();
        var responseDict = new Dictionary<string, Response>();
        while (responseDict.Count != requestDict.Count
               && sw.ElapsedMilliseconds < _config.Timeout.TotalMilliseconds)
        {
            var responses = _subscriptionTracker.GetResponses(requestDict.Values.ToArray(), _config.GetErrorResult);
            foreach (var response in responses)
                responseDict[response.Id] = response;
            await Task.Delay(200);
        }

        // Add Timed Out Results
        foreach (var request in requestDict.Values)
            if (!responseDict.ContainsKey(request.Id))
            {
                var error = "Request Timed Out";
                responseDict[request.Id] = new Response(request.Id, _subscriptionTracker.GetSenderId(), request.StreamId,
                    _config.GetErrorResult?.Invoke((dynamic)request.GetPayload(), HttpStatusCode.RequestTimeout, error), error, (int)HttpStatusCode.RequestTimeout);
            }

        return responseDict.Values.ToArray();
    }

    private Publisher<TM> GetPublisher<TM, T>(string path) where TM : class, ITopicMessage, new()
    {
        var key = $"{typeof(TM).Name}-{path}";
        if (!_publisherDict.ContainsKey(key))
            _publisherDict[key] = _streamingClient.CreatePublisher<TM>().AddStream<T>(path).Build();

        return _publisherDict[key] as Publisher<TM>;
    }
}
