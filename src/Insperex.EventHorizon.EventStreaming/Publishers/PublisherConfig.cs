using System;
using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class PublisherConfig
{
    public string Topic { get; set; }
    public Dictionary<string, Type> TypeDict { get; set; }
    public bool IsOrderGuaranteed { get; set; }
    public bool IsGuaranteed { get; set; }
    public TimeSpan SendTimeout { get; set; }
    public int BatchSize { get; set; }
    public Compression? CompressionType { get; set; }
}
