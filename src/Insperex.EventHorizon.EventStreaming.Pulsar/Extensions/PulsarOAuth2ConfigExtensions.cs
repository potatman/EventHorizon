using System;
using System.Text;
using Newtonsoft.Json;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;

public static class PulsarOAuth2ConfigExtensions
{
    public static PulsarOAuth2Config FromBase64EncodedUri(Uri uri)
    {
        var parts = uri.ToString().Split(',');
        if (parts.Length != 2)
            throw new ArgumentException("Invalid base64 encoded uri");

        return JsonConvert.DeserializeObject<PulsarOAuth2Config>(
            Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])));
    }

    public static Uri ToBase64EncodedUri(this PulsarOAuth2Config config)
    {
        var json = JsonConvert.SerializeObject(config);
        var bytes = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return new Uri($"data:application/json;base64,{bytes}");
    }
}

