using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class Sender<TState> where TState : IState
{
    private readonly SenderConfig _config;
    private readonly ILogger<Sender<TState>> _logger;
    private readonly SenderSubscriptionTracker _subscriptionTracker;
    private readonly IServiceProvider _provider;
    private readonly Dictionary<string, object> _publisherDict = new();
    private readonly Type _stateType = typeof(TState);
    private readonly string _commandTopic;
    private readonly string _requestTopic;

    public Sender(
        SenderSubscriptionTracker subscriptionTracker,
        IServiceProvider provider,
        SenderConfig config,
        ILogger<Sender<TState>> logger)
    {
        var formatter = provider.GetRequiredService<Formatter>();
        _commandTopic = formatter.GetTopic<Command>(_stateType);
        _requestTopic = formatter.GetTopic<Request>(_stateType);
        _subscriptionTracker = subscriptionTracker;
        _provider = provider;
        _config = config;
        _logger = logger;
    }

    public Task SendAsync(string streamId, params ICommand<TState>[] objs)
    {
        var commands = objs.Select(x => new Command(streamId, x)).ToArray();
        return SendAsync(commands);
    }

    public Task SendAsync(params Command[] commands)
    {
        return GetPublisher<Command>(_commandTopic).PublishAsync(commands);
    }

    public async Task<TR> SendAndReceiveAsync<TR>(string streamId, IRequest<TState, TR> obj)
        where TR : IResponse<TState>
    {
        var results = await SendAndReceiveAsync(streamId, [obj]);
        return results.First();
    }

    public async Task<TR[]> SendAndReceiveAsync<TR>(string streamId, IRequest<TState, TR>[] objs)
        where TR : IResponse<TState>
    {
        var requests = objs.Select(x => new Request(streamId, x)).ToArray();
        var res = await SendAndReceiveAsync(requests);
        return res.Select(x => JsonSerializer.Deserialize<TR>(x.Payload)).ToArray();
    }

    public async Task<Response[]> SendAndReceiveAsync(params Request[] requests)
    {
        // Ensure subscription is ready
        var topic = await _subscriptionTracker.TrackSubscription<TState>();

        // Sent SenderId to respond to
        foreach (var request in requests)
            request.ResponseTopic = topic;

        // Send requests
        var requestDict = requests.ToDictionary(x => x.Id);
        await GetPublisher<Request>(_requestTopic).PublishAsync(requests);

        // Wait for messages
        var sw = Stopwatch.StartNew();
        var responseDict = new Dictionary<string, Response>();
        while (responseDict.Count != requestDict.Count
               && sw.ElapsedMilliseconds < _config.Timeout.TotalMilliseconds)
        {
            var responses = _subscriptionTracker.GetResponses(requestDict.Values.ToArray(), _config.GetErrorResult);
            foreach (var response in responses)
                responseDict[response.Id] = response;
            await Task.Delay(10);
        }

        // Add Timed Out Results
        foreach (var request in requestDict.Values)
            if (!responseDict.ContainsKey(request.Id))
            {
                var error = "Request Timed Out";
                responseDict[request.Id] = new Response(request.Id, _subscriptionTracker.GetNodeId(), request.StreamId,
                    _config.GetErrorResult?.Invoke(request, HttpStatusCode.RequestTimeout, error), error, (int)HttpStatusCode.RequestTimeout);
            }

        var errors = responseDict
            .Where(x => x.Value.Error != null)
            .GroupBy(x => x.Value.Error)
            .ToArray();

        foreach (var group in errors)
            _logger.LogError("Sender - Response Error(s) {Count} => {Error}", group.Count(), group.Key);

        if (!errors.Any())
            _logger.LogInformation("Sender - All {Count} Response(s) Received in {Duration}", responseDict.Count, sw.ElapsedMilliseconds);

        return responseDict.Values.ToArray();
    }

    private Publisher<TMessage> GetPublisher<TMessage>(string topic)
        where TMessage : class, ITopicMessage
    {
        if (!_publisherDict.ContainsKey(topic))
            _publisherDict[topic] = _provider.GetRequiredService<StreamingClient>()
                .CreatePublisher<TMessage>()
                .AddCompression(_config.Compression)
                .AddTopic(topic)
                .Build();

        return _publisherDict[topic] as Publisher<TMessage>;
    }
}
