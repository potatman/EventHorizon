using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
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
    private readonly IPulsarAdminRESTAPIClient _admin;
    private readonly ILogger<PulsarTopicAdmin<T>> _logger;
    private readonly PulsarNamespaceAttribute _pulsarAttribute;

    public PulsarTopicAdmin(IPulsarAdminRESTAPIClient admin, AttributeUtil attributeUtil, ILogger<PulsarTopicAdmin<T>> logger)
    {
        _admin = admin;
        _logger = logger;
        _pulsarAttribute = attributeUtil.GetOne<PulsarNamespaceAttribute>(typeof(T));
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        await RequireTenant(topic.Tenant, ct);
        await RequireNamespace(topic, ct);

        try
        {
            if (!topic.IsPersisted)
                await _admin.CreateNonPartitionedTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct);
            else
                await _admin.CreateNonPartitionedTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct);

            var sw = Stopwatch.StartNew();
            var duration = TimeSpan.FromSeconds(10).TotalMilliseconds;
            while (sw.ElapsedMilliseconds < duration)
            {
                var topics = await _admin.GetTopicsAsync(topic.Tenant, topic.Namespace, topic.IsPersisted? Mode.PERSISTENT : Mode.NON_PERSISTENT, false, ct);
                if (topics.Contains(topic.ToString()))
                    break;

                await Task.Delay(100);
            }
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

    private async Task RequireNamespace(PulsarTopic topic, CancellationToken ct)
    {
        // Ensure Namespace Exists
        var namespaces = await _admin.GetTenantNamespacesAsync(topic.Tenant, ct);
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
                await _admin.CreateNamespaceAsync(topic.Tenant, topic.Namespace, policies, ct);
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
        var tenants = await _admin.GetTenantsAsync(ct);
        if (!tenants.Contains(tenant))
        {
            var clusters = await _admin.GetClustersAsync(ct);
            var tenantInfo = new TenantInfo
            {
                AdminRoles = null, AllowedClusters = clusters
            };
            try
            {
                await _admin.CreateTenantAsync(tenant, tenantInfo, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }

    }
}
