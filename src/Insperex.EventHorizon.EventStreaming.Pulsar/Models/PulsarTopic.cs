namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models;

public class PulsarTopic
{
    public bool IsPersisted { get; set; }
    public string Tenant { get; set; }
    public string Namespace { get; set; }
    public string Topic { get; set; }

    public override string ToString()
    {
        var root = IsPersisted ? "persistent" : "non-persistent";
        return $"{root}://{Tenant}/{Namespace}/{Topic}";
    }
}