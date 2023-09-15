namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models;

public class PulsarTopic
{
    public bool IsPersisted { get; set; }
    public string Tenant { get; set; }
    public string Namespace { get; set; }
    public string Topic { get; set; }

    public string PersistentString => IsPersisted ? "persistent" : "non-persistent";

    public string ApiRoot => $"{PersistentString}/{Tenant}/{Namespace}/{Topic}";

    public override string ToString()
    {
        return $"{PersistentString}://{Tenant}/{Namespace}/{Topic}";
    }
}
