using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Microsoft.Extensions.Logging;
using SharpPulsar.Admin.v2;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicAdmin : ITopicAdmin
{
    private readonly IPulsarAdminRESTAPIClient _admin;
    private readonly PulsarConfig _pulsarConfig;
    private readonly ILogger<PulsarTopicAdmin> _logger;
    private static readonly SemaphoreSlim SemaphoreSlim = new(1,1);
    private readonly HttpClient _httpClient;
    private static readonly List<string> Tenants = new();
    private static readonly List<string> Namespaces = new();

    public PulsarTopicAdmin(IPulsarAdminRESTAPIClient admin, PulsarConfig pulsarConfig, ILogger<PulsarTopicAdmin> logger)
    {
        _admin = admin;
        _pulsarConfig = pulsarConfig;
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri($"{pulsarConfig.AdminUrl}/admin/v2/") };
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        Console.WriteLine("RequireTopicAsync - 1");
        var topic = PulsarTopicParser.Parse(str);
        await RequireNamespace(topic.Tenant, topic.Namespace, -1, -1, ct).ConfigureAwait(false);
        Console.WriteLine("RequireTopicAsync - 2");

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
        await SemaphoreSlim.WaitAsync(ct);
        try
        {
            Console.WriteLine("DeleteTopicAsync - 1");
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
            Console.WriteLine("DeleteTopicAsync - 2");
        }
        finally
        {
            SemaphoreSlim.Release();
        }
    }

    private async Task RequireNamespace(string tenant, string nameSpace, int? retentionInMb, int? retentionInMinutes, CancellationToken ct)
    {
        // Ensure Tenant Exists
        if (!Tenants.Contains(tenant))
        {
            Console.WriteLine("RequireNamespace - 1");
            var tenants = await GetStringArray("tenants", ct).ConfigureAwait(false);
            Console.WriteLine("RequireNamespace - 2");
            if (!tenants.Contains(tenant))
            {
                var clusters = await GetStringArray("clusters", ct).ConfigureAwait(false);
                Console.WriteLine("RequireNamespace - 3");
                var tenantInfo = new TenantInfo { AdminRoles = null, AllowedClusters = clusters };
                try
                {
                    await _admin.CreateTenantAsync(tenant, tenantInfo, ct).ConfigureAwait(false);
                    Console.WriteLine("RequireNamespace - 4");
                }
                catch (Exception)
                {
                    // Ignore race conditions
                }
            }
            Tenants.Add(tenant);
        }

        // Ensure Namespace Exists
        var namespaceKey = $"{tenant}/{nameSpace}";
        if (!Namespaces.Contains(namespaceKey))
        {
            var namespaces = await GetStringArray($"namespaces/{tenant}", ct).ConfigureAwait(false);
            Console.WriteLine("RequireNamespace - 5");
            if (!namespaces.Contains(namespaceKey))
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
                    await _admin.CreateNamespaceAsync(tenant, nameSpace, policies, ct).ConfigureAwait(false);
                    Console.WriteLine("RequireNamespace - 6");
                }
                catch (Exception)
                {
                    // Ignore race conditions
                }
            }
            Namespaces.Add(namespaceKey);
        }
    }

    private async Task<string[]> GetStringArray(string path, CancellationToken ct)
    {
        Console.WriteLine("GetStringArray - 1");
        var client = new HttpClient { BaseAddress = new Uri($"{_pulsarConfig.AdminUrl}/admin/v2/") };
        var result = await client.GetStringAsync(path, ct).ConfigureAwait(false);
        var res = JsonSerializer.Deserialize<string[]>(result);
        Console.WriteLine($"GetStringArray - {string.Join(",", res)}");
        return res;
    }
}
