using System;

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class PublisherConfig
{
    public string Topic { get; set; }
    public TimeSpan SendTimeout { get; set; }
}