using System;
using System.Net;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models.TopicMessages;

namespace EventHorizon.EventSourcing.Senders;

public class SenderConfig
{
    public TimeSpan Timeout { get; set; }
    public Func<Request, HttpStatusCode, string, IResponse> GetErrorResult { get; set; }
}
