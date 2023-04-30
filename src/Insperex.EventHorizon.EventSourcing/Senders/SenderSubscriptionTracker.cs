using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Util;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderSubscriptionTracker : IDisposable
{
    private readonly ITopicResolver _topicResolver;
    private readonly StreamingClient _streamingClient;
    private readonly Dictionary<Type, Subscription<Response>> _subscriptionDict = new ();
    private readonly Dictionary<string, MessageContext<Response>> _responseDict = new ();
    private readonly List<string> _responseTopics = new();
    private readonly string _senderId;

    public SenderSubscriptionTracker(ITopicResolver topicResolver, StreamingClient streamingClient)
    {
        _topicResolver = topicResolver;
        _streamingClient = streamingClient;
        _senderId = NameUtil.AssemblyNameWithGuid;
        // Used for when process is stopped mid way
        AppDomain.CurrentDomain.ProcessExit += OnExit;
    }

    public string GetSenderId() => _senderId;

    public async Task TrackSubscription<T>() where T : IState
    {
        var type = typeof(T);
        if(_subscriptionDict.ContainsKey(type))
            return;

        _responseTopics.AddRange(_topicResolver.GetTopics<Response>(typeof(T), _senderId));
        var subscription = await _streamingClient.CreateSubscription<Response>()
            .OnBatch(x =>
            {
                // Check Results
                foreach (var response in x.Messages)
                    _responseDict[response.Data.RequestId] = response;
                return Task.CompletedTask;
            })
            .AddStateTopic<T>(_senderId)
            .IsBeginning(true)
            .Build()
            .StartAsync();

        _subscriptionDict[type] = subscription;
    }

    public Response[] GetResponses(string[] responseIds, Func<AggregateStatus, string, IResponse> configGetErrorResult)
    {
        var responses = new List<Response>();
        foreach (var responseId in responseIds)
            if (_responseDict.TryGetValue(responseId, out var value))
            {
                // Add Response, Make Custom if needed
                responses.Add(value.Data.Status != AggregateStatus.Ok
                    ? new Response(value.Data.StreamId, value.Data.RequestId, _senderId,
                        configGetErrorResult(value.Data.Status, value.Data.Error)) { Status = value.Data.Status }
                    : value.Data);
            }

        return responses.ToArray();
    }

    public void Dispose()
    {
        OnExit(null, null);
    }

    private void OnExit(object sender, EventArgs e)
    {
        foreach (var group in _subscriptionDict)
        {
            // Stop Subscription
            group.Value.StopAsync().Wait();

            // Delete Topic
            _streamingClient.GetAdmin<Response>().DeleteTopicAsync(group.Key, _senderId).Wait();
        }
    }
}
