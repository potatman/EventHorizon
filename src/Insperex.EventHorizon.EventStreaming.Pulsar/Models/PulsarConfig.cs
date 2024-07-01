using Newtonsoft.Json;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models;

public class PulsarConfig
{
    public string ServiceUrl { get; set; }
    public string AdminUrl { get; set; }

    public PulsarOAuth2Config OAuth2 { get; set; }
}

public class PulsarOAuth2Config
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("client_id")]
    public string ClientId { get; set; }

    [JsonProperty("client_secret")]
    public string ClientSecret { get; set; }

    [JsonProperty("client_email")]
    public string ClientEmail { get; set; }

    [JsonProperty("issuer_url")]
    public string IssuerUrl { get; set; }

    [JsonProperty("audience")]
    public string Audience { get; set; }

    [JsonProperty("token_address")]
    public string TokenAddress { get; set; }

    [JsonProperty("grant_type")]
    public string GrantType { get; set; }
}
