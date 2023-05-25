using System;

namespace Insperex.EventHorizon.EventStreaming.Readers;

public class ReaderConfig
{
    public string Topic { get; set; }
    public string[] Keys { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool? IsBeginning { get; set; }
}