namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models;

public class PulsarConfig
{
    public string ServiceUrl { get; set; }
    public string AdminUrl { get; set; }

    public PulsarOAuth2Config OAuth2 { get; set; }
}

public class PulsarOAuth2Config
{
    public string File { get; set; }
    public string Audience { get; set; }
    public string TokenAddress { get; set; }
    public string GrantType { get; set; }
}
