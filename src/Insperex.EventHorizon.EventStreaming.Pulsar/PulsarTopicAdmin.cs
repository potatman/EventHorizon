using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Microsoft.Extensions.Logging;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicAdmin : ITopicAdmin
{
    private readonly IPulsarAdminRESTAPIClient _admin;
    private readonly ILogger<PulsarTopicAdmin> _logger;

    public PulsarTopicAdmin(IPulsarAdminRESTAPIClient admin, ILogger<PulsarTopicAdmin> logger)
    {
        _admin = admin;
        _logger = logger;
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        var topic = PulsarTopicParser.Parse(str);
        await RequireNamespace(topic.Tenant, topic.Namespace, -1, -1, ct);

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

    private async Task RequireNamespace(string tenant, string nameSpace, int? retentionInMb, int? retentionInMinutes, CancellationToken ct)
    {
        // Ensure Tenant Exists
        var tenants = await _admin.GetTenantsAsync(ct);
        if (!tenants.Contains(tenant))
        {
            var clusters = await _admin.GetClustersAsync(ct);
            var tenantInfo = new TenantInfo { AdminRoles = null, AllowedClusters = clusters };
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
            // Add Retention Policy if namespace == Events
            var policies = !nameSpace.Contains(PulsarConstants.Event)
                ? new Policies()
                : new Policies
                {
                    Retention_policies = new RetentionPolicies
                    {
                        RetentionTimeInMinutes = retentionInMb ?? -1,
                        RetentionSizeInMB = retentionInMinutes ?? -1
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
