using System;

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class PublisherConfig
{
    public string Topic { get; set; }
    public bool IsGuaranteed { get; set; }
    public TimeSpan SendTimeout { get; set; }
    public int BatchSize { get; set; }
}
