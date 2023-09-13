using Newtonsoft.Json;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models
{
    public class PulsarOAuthData
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
    }
}
