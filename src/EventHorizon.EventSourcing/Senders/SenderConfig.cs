using System;
using System.Net;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Serialization.Compression;

namespace EventHorizon.EventSourcing.Senders;

public class SenderConfig
{
    public TimeSpan Timeout { get; set; }
    public Compression? Compression { get; set; }
    public Func<Request, HttpStatusCode, string, IResponse> GetErrorResult { get; set; }
}
