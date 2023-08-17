using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Attributes;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Microsoft.Extensions.Logging;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicAdmin<T> : ITopicAdmin<T> where T : ITopicMessage
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly IPulsarAdminRESTAPIClient _admin;
    private readonly ILogger<PulsarTopicAdmin<T>> _logger;
    private readonly PulsarNamespaceAttribute _pulsarAttribute;

    public PulsarTopicAdmin(PulsarClientResolver clientResolver, AttributeUtil attributeUtil, ILogger<PulsarTopicAdmin<T>> logger)
    {
        _clientResolver = clientResolver;
        _admin = _clientResolver.GetAdminClient();
        _logger = logger;
        _pulsarAttribute = attributeUtil.GetOne<PulsarNamespaceAttribute>(typeof(T));
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        await RequireNamespace(topic, ct);

        try
        {
            if (!topic.IsPersisted)
                await _admin.CreateNonPartitionedTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct);
            else
                await _admin.CreateNonPartitionedTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct);
        }
        catch (ApiException ex)
        {
            // 409 - Topic already exist
            if (ex.StatusCode != 409)
                throw;
        }
    }

    public async Task DeleteTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        try
        {
            if (!topic.IsPersisted)
                await _admin.DeleteTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct);
            else
                await _admin.DeleteTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct);
            _logger.LogInformation("Deleted Topic {Topic}", topic);
        }
        catch (ApiException ex)
        {
            // 404 - Namespace or topic does not exist
            if (ex.StatusCode != 404)
                throw;
        }
    }

    private async Task<JsonElement> GetTopicStatsJson(string str, CancellationToken ct,
        bool authoritative = false, bool getPreciseBacklog = false,
        bool subscriptionBacklogSize = true, bool getEarliestTimeInBacklog = false)
    {
        using var httpClient = _clientResolver.GetAdminHttpClient();

        var topic = PulsarTopicParser.Parse(str);
        var parameters = new string[]
        {
            $"authoritative={authoritative.ToString().ToLowerInvariant()}",
            $"getPreciseBacklog={getPreciseBacklog.ToString().ToLowerInvariant()}",
            $"subscriptionBacklogSize={subscriptionBacklogSize.ToString().ToLowerInvariant()}",
            $"getEarliestTimeInBacklog={getEarliestTimeInBacklog.ToString().ToLowerInvariant()}",
        };
        var url = $"{topic.ApiRoot}/stats?{string.Join('&', parameters)}";

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(responseBody).RootElement;
    }

    public async Task<PulsarKeyHashRanges> GetTopicConsumerKeyHashRanges(string topic, string subscriptionName,
        string consumerName, CancellationToken ct)
    {
        const int attempts = 20;

        int attempt = 0;

        do
        {
            var keyHashRanges = await TryTopicConsumerKeyHashRanges(topic, subscriptionName, consumerName, ct);
            if (keyHashRanges != null) return keyHashRanges;

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        } while (++attempt < attempts);

        return null;
    }

    private async Task<PulsarKeyHashRanges> TryTopicConsumerKeyHashRanges(string topic, string subscriptionName, string consumerName,
        CancellationToken ct)
    {
        var stats = await GetTopicStatsJson(topic, ct, subscriptionBacklogSize: false);

        var subscriptionsRoot = stats.GetProperty("subscriptions");
        var subscriptions = subscriptionsRoot.EnumerateObject().Select(p => p.Name).ToArray();
        var fullSubscriptionName = subscriptions.FirstOrDefault(s => s.Contains(subscriptionName));
        if (!string.IsNullOrEmpty(fullSubscriptionName))
        {
            var consumers = subscriptionsRoot
                .GetProperty(fullSubscriptionName)
                .GetProperty("consumers")
                .EnumerateArray();

            foreach (var consumer in consumers)
            {
                if (consumer.TryGetProperty("consumerName", out var consumerNameProp) &&
                    consumerNameProp.GetString() == consumerName)
                {
                    if (consumer.TryGetProperty("keyHashRanges", out var keyHashRangesProp))
                    {
                        var jsonRanges = keyHashRangesProp.EnumerateArray().ToArray();
                        return BuildKeyHashRanges(jsonRanges);
                    }

                    return new PulsarKeyHashRanges(); // No ranges found - this instance will match all keys.
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Create an instance of <see cref="PulsarKeyHashRanges"/> from the JSON output
    /// of the stats query in the Pulsar Admin API./>
    /// </summary>
    /// <param name="keyHashRanges">
    /// JSON elements - each being string containing serialized JSON array of two numbers.
    /// (e.g. "[445382,445383]"
    /// </param>
    /// <returns>New instance of <see cref="PulsarKeyHashRanges"/>.</returns>
    private static PulsarKeyHashRanges BuildKeyHashRanges(JsonElement[] keyHashRanges)
    {
        ArgumentNullException.ThrowIfNull(keyHashRanges);

        var ranges = keyHashRanges
            .Select(r => JsonValue.Parse(r.GetString()).AsArray().ToArray())
            .Select(r => (r[0].GetValue<int>(), r[1].GetValue<int>()))
            .ToArray();

        return new PulsarKeyHashRanges {Ranges = ranges};
    }

    private async Task RequireNamespace(PulsarTopic topic, CancellationToken ct)
    {
        // Ensure Tenant Exists
        var tenants = await _admin.GetTenantsAsync(ct);
        if (!tenants.Contains(topic.Tenant))
        {
            var clusters = await _admin.GetClustersAsync(ct);
            var tenantInfo = new TenantInfo
            {
                AdminRoles = null, AllowedClusters = clusters
            };
            try
            {
                await _admin.CreateTenantAsync(topic.Tenant, tenantInfo, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }

        // Ensure Namespace Exists
        var namespaces = await _admin.GetTenantNamespacesAsync(topic.Tenant, ct);
        if (!namespaces.Contains($"{topic.Tenant}/{topic.Namespace}"))
        {
            var policies = new Policies();

            // if (topic.IsPersisted)
            {
                policies.Retention_policies = new RetentionPolicies
                {
                    // Note: pulsar will delete data with no subscriptions, if retention is not set to -1
                    RetentionTimeInMinutes = _pulsarAttribute?.RetentionTimeInMinutes ?? -1,
                    RetentionSizeInMB = _pulsarAttribute?.RetentionSizeInMb ?? -1
                };
            }

            try
            {
                await _admin.CreateNamespaceAsync(topic.Tenant, topic.Namespace, policies, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }
    }
}
