using System;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly IPulsarAdminRESTAPIClient _admin;
    private readonly ILogger<PulsarTopicAdmin<T>> _logger;
    private readonly PulsarConfigAttribute _pulsarAttribute;

    public PulsarTopicAdmin(IPulsarAdminRESTAPIClient admin, AttributeUtil attributeUtil, ILogger<PulsarTopicAdmin<T>> logger)
    {
        _admin = admin;
        _logger = logger;
        _pulsarAttribute = attributeUtil.GetOne<PulsarConfigAttribute>(typeof(T));
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        await RequireNamespace(topic.Tenant, topic.Namespace, ct);

        // try
        // {
        //     await _admin.CreateNonPartitionedTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, new Dictionary<string, string>(), ct);
        // }
        // catch (ApiException ex)
        // {
        //     // 409 - Partitioned topic already exist
        //     if (ex.StatusCode != 409)
        //         throw;
        // }
    }

    public async Task DeleteTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        try
        {
            if (topic.IsPersisted)
                await _admin.DeleteTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct);
            else
                await _admin.UnloadTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, ct);
            _logger.LogInformation("Deleted Topic {Topic}", topic);
        }
        catch (ApiException ex)
        {
            // 404 - Namespace or topic does not exist
            if (ex.StatusCode != 404)
                throw;
        }
    }

    private async Task RequireNamespace(string tenant, string nameSpace, CancellationToken ct)
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

        // Ensure Namespace Exists
        var namespaces = await _admin.GetTenantNamespacesAsync(tenant, ct);
        if (!namespaces.Contains($"{tenant}/{nameSpace}"))
        {
            var policies = new Policies
            {
                // Note: pulsar will delete data with no subscriptions, if retention is not set to -1
                Retention_policies = new RetentionPolicies
                {
                    RetentionTimeInMinutes = _pulsarAttribute?.RetentionTimeInMinutes ?? -1,
                    RetentionSizeInMB = _pulsarAttribute?.RetentionSizeInMb ?? -1
                }
            };
            try
            {
                await _admin.CreateNamespaceAsync(tenant, nameSpace, policies, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }
    }
}
