using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Generated;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicAdmin : ITopicAdmin
{
    private readonly ILogger<PulsarTopicAdmin> _logger;
    private readonly ClustersBaseClient _clustersBaseClient;
    private readonly TenantsBaseClient _tenantsBaseClient;
    private readonly NamespacesClient _namespacesClient;
    private readonly PersistentTopicsClient _persistentTopicsClient;
    private readonly NonPersistentTopicsClient _nonPersistentTopicsClient;

    public PulsarTopicAdmin(IOptions<PulsarConfig> options, ILogger<PulsarTopicAdmin> logger)
    {
        _logger = logger;
        var baseUrl = $"{options.Value.AdminUrl}/admin/v2/";
        var httpClient = new HttpClient();
        _clustersBaseClient = new ClustersBaseClient(baseUrl, httpClient);
        _tenantsBaseClient = new TenantsBaseClient(baseUrl, httpClient);
        _namespacesClient = new NamespacesClient(baseUrl, httpClient);
        _persistentTopicsClient = new PersistentTopicsClient(baseUrl, httpClient);
        _nonPersistentTopicsClient = new NonPersistentTopicsClient(baseUrl, httpClient);
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
                await _persistentTopicsClient.DeleteTopic2Async(topic.Tenant, topic.Namespace, topic.Topic, true, true, ct);
            else
                await _nonPersistentTopicsClient.UnloadTopicAsync(topic.Tenant, topic.Namespace, topic.Topic, true, ct);
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
        var tenants = await _tenantsBaseClient.GetTenantsAsync(ct);
        if (!tenants.Contains(tenant))
        {
            var clusters = await _clustersBaseClient.GetClustersAsync(ct);
            var tenantInfo = new TenantInfo { AdminRoles = null, AllowedClusters = clusters };
            try
            {
                await _tenantsBaseClient.CreateTenantAsync(tenant, tenantInfo, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }

        // Ensure Namespace Exists
        var namespaces = await _namespacesClient.GetTenantNamespacesAsync(tenant, ct);
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
                await _namespacesClient.CreateNamespaceAsync(tenant, nameSpace, policies, ct);
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }
    }
}
