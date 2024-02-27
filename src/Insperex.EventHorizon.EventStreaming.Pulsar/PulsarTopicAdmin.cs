using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Attributes;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Microsoft.Extensions.Logging;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicAdmin<T> : ITopicAdmin<T> where T : ITopicMessage
{
    private readonly IPulsarAdminRESTAPIClient _pulsarAdminClient;
    private readonly PulsarClientResolver _pulsarClientResolver;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILogger<PulsarTopicAdmin<T>> _logger;
    private readonly PulsarNamespaceAttribute _pulsarAttribute;

    public PulsarTopicAdmin(PulsarClientResolver pulsarClientResolver, AttributeUtil attributeUtil, ILogger<PulsarTopicAdmin<T>> logger)
    {
        _pulsarAdminClient = pulsarClientResolver.GetAdminClientAsync().GetAwaiter().GetResult();
        _pulsarClientResolver = pulsarClientResolver;
        _attributeUtil = attributeUtil;
        _logger = logger;
        _pulsarAttribute = attributeUtil.GetOne<PulsarNamespaceAttribute>(typeof(T));
    }

    public string GetTopic(Type type, string senderId = null)
    {
        var persistent = EventStreamingConstants.Persistent;
        var pulsarAttr = _attributeUtil.GetOne<PulsarNamespaceAttribute>(type);
        var attribute = _attributeUtil.GetAll<StreamAttribute>(type).First(x => x.SubType == null);

        var tenant = pulsarAttr?.Tenant ?? PulsarTopicConstants.DefaultTenant;
        var @namespace = !PulsarTopicConstants.MessageTypes.Contains(typeof(T))
            ? pulsarAttr?.Namespace ?? PulsarTopicConstants.DefaultNamespace
            : PulsarTopicConstants.MessageNamespace;

        var topic = senderId == null ? attribute.Topic : $"{attribute.Topic}-{senderId}";
        return $"{persistent}://{tenant}/{@namespace}/{topic}".Replace(PulsarTopicConstants.TypeKey, typeof(T).Name);
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        await RequireTenant(topic.Tenant, ct);
        await RequireNamespace(topic, ct);

        try
        {
            if (!topic.IsPersisted)
                await _pulsarAdminClient.CreateNonPartitionedTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct);
            else
                await _pulsarAdminClient.CreateNonPartitionedTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct);

            var sw = Stopwatch.StartNew();
            var duration = TimeSpan.FromSeconds(10).TotalMilliseconds;
            while (sw.ElapsedMilliseconds < duration)
            {
                var topics = await _pulsarAdminClient.GetTopicsAsync(topic.Tenant, topic.Namespace, topic.IsPersisted? Mode.PERSISTENT : Mode.NON_PERSISTENT, false, ct);
                if (topics.Contains(topic.ToString()))
                    break;

                await Task.Delay(100);
            }
        }
        catch (ApiException ex)
        {
            // 409 - Topic already exist
            if (ex.StatusCode > 300 && ex.StatusCode != 409)
                throw;
        }
    }

    public async Task DeleteTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        try
        {
            if (!topic.IsPersisted)
                await _pulsarAdminClient.DeleteTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct);
            else
                await _pulsarAdminClient.DeleteTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct);
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
        using var httpClient = _pulsarClientResolver.GetAdminHttpClient();

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
        // Ensure Namespace Exists
        var namespaces = await _pulsarAdminClient.GetTenantNamespacesAsync(topic.Tenant, ct);
        if (!namespaces.Contains($"{topic.Tenant}/{topic.Namespace}"))
        {
            var policies = new Policies();

            if (topic.Namespace == PulsarTopicConstants.MessageNamespace)
            {
                policies.Retention_policies = new RetentionPolicies
                {
                    RetentionTimeInMinutes = 10,
                    RetentionSizeInMB = -1
                };
            }
            else
            {
                policies.Retention_policies = new RetentionPolicies
                {
                    RetentionTimeInMinutes = _pulsarAttribute?.RetentionTimeInMinutes ?? -1,
                    RetentionSizeInMB = _pulsarAttribute?.RetentionSizeInMb ?? -1
                };
            }
            // policies.Compaction_threshold = 1000000;

            try
            {
                await _pulsarAdminClient.CreateNamespaceAsync(topic.Tenant, topic.Namespace, policies, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }


    }

    private async Task RequireTenant(string tenant, CancellationToken ct)
    {
        // Ensure Tenant Exists
        var tenants = await _pulsarAdminClient.GetTenantsAsync(ct);
        if (!tenants.Contains(tenant))
        {
            var clusters = await _pulsarAdminClient.GetClustersAsync(ct);
            var tenantInfo = new TenantInfo
            {
                AdminRoles = null, AllowedClusters = clusters
            };
            try
            {
                await _pulsarAdminClient.CreateTenantAsync(tenant, tenantInfo, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }

    }
}
