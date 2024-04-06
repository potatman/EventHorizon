using System;
using System.Net;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderConfig
{
    public TimeSpan Timeout { get; set; }
    public Compression? Compression { get; set; }
    public Func<Request, HttpStatusCode, string, IResponse> GetErrorResult { get; set; }
}
