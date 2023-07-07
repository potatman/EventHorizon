using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Util;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderSubscriptionTracker : IAsyncDisposable
{
    private readonly StreamingClient _streamingClient;
    private Subscription<Response> _subscription;
    private readonly Dictionary<string, MessageContext<Response>> _responseDict = new ();
    private readonly string _senderId;
    private bool _cleaning;

    public SenderSubscriptionTracker(StreamingClient streamingClient)
    {
        _streamingClient = streamingClient;
        _senderId = NameUtil.AssemblyNameWithGuid;
        // Used for when process is stopped mid way
        AppDomain.CurrentDomain.ProcessExit += OnExit;
    }

    public string GetSenderId() => _senderId;

    public async Task TrackSubscription<T>() where T : IState
    {
        if(_subscription != null)
            return;

        _subscription = _streamingClient.CreateSubscription<Response>()
            .SubscriptionType(SubscriptionType.Exclusive)
            .OnBatch(x =>
            {
                // Check Results
                foreach (var response in x.Messages)
                    _responseDict[response.Data.RequestId] = response;
                return Task.CompletedTask;
            })
            .AddStream<T>(_senderId)
            .Build();

        await _subscription.StartAsync();
    }

    public Response[] GetResponses(string[] responseIds, Func<HttpStatusCode, string, IResponse> configGetErrorResult)
    {
        var responses = new List<Response>();
        foreach (var responseId in responseIds)
            if (_responseDict.TryGetValue(responseId, out var value))
            {
                // Add Response, Make Custom if needed
                responses.Add(value.Data.Error != null
                    ? new Response(value.Data.StreamId, value.Data.RequestId, _senderId,
                        configGetErrorResult((HttpStatusCode)value.Data.StatusCode, value.Data.Error))
                    : value.Data);
            }

        return responses.ToArray();
    }

    private async Task CleanupAsync()
    {
        _cleaning = true;
        await _subscription.DeleteTopicsAsync();
        await _subscription.StopAsync();
        _subscription = null;
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
