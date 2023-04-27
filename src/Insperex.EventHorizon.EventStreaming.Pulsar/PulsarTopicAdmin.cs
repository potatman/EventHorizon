using System;
using System.Net.Http;
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
    private readonly PulsarConfig _config;
    private readonly ILogger<PulsarTopicAdmin> _logger;

    public PulsarTopicAdmin(PulsarConfig config, ILogger<PulsarTopicAdmin> logger)
    {
        _config = config;
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
        Console.WriteLine("DeleteTopicAsync - 1");
        var topic = PulsarTopicParser.Parse(str);
        try
        {
            if (topic.IsPersisted)
                await GetAdmin().DeleteTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct);
            else
                await GetAdmin().UnloadTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, ct);
            _logger.LogInformation("Deleted Topic {Topic}", topic);
        }
        catch (ApiException ex)
        {
            // 404 - Namespace or topic does not exist
            if (ex.StatusCode != 404)
                throw;
        }
        Console.WriteLine("DeleteTopicAsync - 2");
    }

    private async Task RequireNamespace(string tenant, string nameSpace, int? retentionInMb, int? retentionInMinutes, CancellationToken ct)
    {
        // Ensure Tenant Exists
        Console.WriteLine("RequireNamespace - 1");
        var tenants = await GetAdmin().GetTenantsAsync(ct);
        Console.WriteLine("RequireNamespace - 2");
        if (!tenants.Contains(tenant))
        {
            var clusters = await GetAdmin().GetClustersAsync(ct);
            Console.WriteLine("RequireNamespace - 3");
            var tenantInfo = new TenantInfo { AdminRoles = null, AllowedClusters = clusters };
            try
            {
                await GetAdmin().CreateTenantAsync(tenant, tenantInfo, ct);
                Console.WriteLine("RequireNamespace - 4");
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }

        // Ensure Namespace Exists
        var namespaces = await GetAdmin().GetTenantNamespacesAsync(tenant, ct);
        Console.WriteLine("RequireNamespace - 5");
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
                await GetAdmin().CreateNamespaceAsync(tenant, nameSpace, policies, ct);
                Console.WriteLine("RequireNamespace - 6");
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }
    }

    private PulsarAdminRESTAPIClient GetAdmin()
    {
        return new PulsarAdminRESTAPIClient(new HttpClient { BaseAddress = new Uri($"{_config.AdminUrl}/admin/v2/") });
    }
}
