namespace Insperex.EventHorizon.Abstractions.Interfaces.Internal;

public interface ITopicMessage
{
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
}