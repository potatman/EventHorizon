using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderSubscriptionTracker : IAsyncDisposable
{
    private readonly StreamingClient _streamingClient;
    private readonly Formatter _formatter;
    private readonly Dictionary<Type, Subscription<Response>> _subscriptionDict = new ();
    private readonly Dictionary<string, MessageContext<Response>> _responseDict = new ();
    private readonly string _nodeId;
    private bool _cleaning;

    public SenderSubscriptionTracker(StreamingClient streamingClient, Formatter formatter)
    {
        _streamingClient = streamingClient;
        _formatter = formatter;
        _nodeId = Guid.NewGuid().ToString()[..8];

        // Used for when process is stopped mid way
        AppDomain.CurrentDomain.ProcessExit += OnExit;
    }

    public string GetNodeId() => _nodeId;

    public async Task<string> TrackSubscription<TState>() where TState : IState
    {
        var type = typeof(TState);
        var topic = _formatter.GetTopic<Response>(AssemblyUtil.Assembly, type, _nodeId);

        if(_subscriptionDict.ContainsKey(type))
            return topic;

        var subscription = _streamingClient.CreateSubscription<Response>()
            .SubscriptionType(SubscriptionType.Exclusive)
            .OnBatch(async x =>
            {
                // Check Results
                foreach (var response in x.Messages)
                    _responseDict[response.Data.Id] = response;
            })
            .BatchSize(100000)
            .AddStateStream<TState>(AssemblyUtil.Assembly, _nodeId)
            .IsBeginning(true)
            .Build();

        _subscriptionDict[type] = subscription;

        await subscription.StartAsync();

        return topic;
    }

    public Response[] GetResponses(Request[] requests, Func<Request, HttpStatusCode, string, IResponse> configGetErrorResult)
    {
        var responses = new List<Response>();
        foreach (var request in requests)
        {
            if (_responseDict.TryGetValue(request.Id, out var value))
            {
                // Add Response, Make Custom if needed
                responses.Add(value.Data.Error != null
                    ? new Response(value.Data.Id, value.Data.Topic, value.Data.StreamId,
                        configGetErrorResult(request, (HttpStatusCode)value.Data.StatusCode, value.Data.Error), value.Data.Error, value.Data.StatusCode)
                    : value.Data);
            }
        }

        return responses.ToArray();
    }

    private async Task CleanupAsync()
    {
        _cleaning = true;
        foreach (var group in _subscriptionDict)
        {
            // Stop Subscription
            await group.Value.StopAsync();

            // Delete Topic
            await _streamingClient.GetAdmin<Response>().DeleteTopicAsync(group.Key);
        }
        _subscriptionDict.Clear();
    }

    private void OnExit(object sender, EventArgs e)
    {
        if(!_cleaning)
            CleanupAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        if(!_cleaning)
            await CleanupAsync();
    }
}
