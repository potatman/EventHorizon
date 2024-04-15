using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Pulsar.Utils;
using Microsoft.Extensions.Logging;
using SharpPulsar.Admin.v2;

namespace EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicAdmin<TMessage> : ITopicAdmin<TMessage>
    where TMessage : ITopicMessage
{
    private readonly IPulsarAdminRESTAPIClient _pulsarAdminClient;
    private readonly PulsarClientResolver _pulsarClientResolver;
    private readonly ILogger<PulsarTopicAdmin<TMessage>> _logger;

    public PulsarTopicAdmin(PulsarClientResolver pulsarClientResolver, ILogger<PulsarTopicAdmin<TMessage>> logger)
    {
        _pulsarAdminClient = pulsarClientResolver.GetAdminClientAsync().GetAwaiter().GetResult();
        _pulsarClientResolver = pulsarClientResolver;
        _logger = logger;
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        await RequireTenant(topic.Tenant, ct).ConfigureAwait(false);
        await RequireNamespace(topic, ct).ConfigureAwait(false);

        try
        {
            if (!topic.IsPersisted)
                await _pulsarAdminClient.CreateNonPartitionedTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct).ConfigureAwait(false);
            else
                await _pulsarAdminClient.CreateNonPartitionedTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct).ConfigureAwait(false);

            var sw = Stopwatch.StartNew();
            var duration = TimeSpan.FromSeconds(10).TotalMilliseconds;
            while (sw.ElapsedMilliseconds < duration)
            {
                var topics = await _pulsarAdminClient.GetTopicsAsync(topic.Tenant, topic.Namespace, topic.IsPersisted? Mode.PERSISTENT : Mode.NON_PERSISTENT, false, ct).ConfigureAwait(false);
                if (topics.Contains(topic.ToString()))
                    break;

                await Task.Delay(100).ConfigureAwait(false);
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
                await _pulsarAdminClient.DeleteTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct).ConfigureAwait(false);
            else
                await _pulsarAdminClient.DeleteTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct).ConfigureAwait(false);
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

        var response = await httpClient.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonDocument.Parse(responseBody).RootElement;
    }

    public async Task<PulsarKeyHashRanges> GetTopicConsumerKeyHashRangesAsync(string topic, string subscriptionName,
        string consumerName, CancellationToken ct)
    {
        const int attempts = 20;

        int attempt = 0;

        do
        {
            var keyHashRanges = await TryTopicConsumerKeyHashRangesAsync(topic, subscriptionName, consumerName, ct).ConfigureAwait(false);
            if (keyHashRanges != null) return keyHashRanges;

            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        } while (++attempt < attempts);

        return null;
    }

    private async Task<PulsarKeyHashRanges> TryTopicConsumerKeyHashRangesAsync(string topic, string subscriptionName, string consumerName,
        CancellationToken ct)
    {
        var stats = await GetTopicStatsJson(topic, ct, subscriptionBacklogSize: false).ConfigureAwait(false);

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
        var namespaces = await _pulsarAdminClient.GetTenantNamespacesAsync(topic.Tenant, ct).ConfigureAwait(false);
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
                    RetentionTimeInMinutes = -1,
                    RetentionSizeInMB = -1
                };
            }
            // policies.Compaction_threshold = 1000000;

            try
            {
                await _pulsarAdminClient.CreateNamespaceAsync(topic.Tenant, topic.Namespace, policies, ct).ConfigureAwait(false);
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
        var tenants = await _pulsarAdminClient.GetTenantsAsync(ct).ConfigureAwait(false);
        if (!tenants.Contains(tenant))
        {
            var clusters = await _pulsarAdminClient.GetClustersAsync(ct).ConfigureAwait(false);
            var tenantInfo = new TenantInfo
            {
                AdminRoles = null, AllowedClusters = clusters
            };
            try
            {
                await _pulsarAdminClient.CreateTenantAsync(tenant, tenantInfo, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }

    }
}
