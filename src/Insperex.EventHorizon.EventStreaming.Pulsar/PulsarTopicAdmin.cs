using System;
using System.Linq;
using System.Net.Http;
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
    private readonly ILogger<PulsarTopicAdmin> _logger;
    private static readonly SemaphoreSlim SemaphoreSlim = new(1,1);
    private readonly HttpClient _httpClient;

    public PulsarTopicAdmin(IPulsarAdminRESTAPIClient admin, PulsarConfig pulsarConfig, ILogger<PulsarTopicAdmin> logger)
    {
        _admin = admin;
        _logger = logger;
        _httpClient = new HttpClient { BaseAddress = new Uri($"{pulsarConfig.AdminUrl}/admin/v2/") };
    }

    public async Task RequireTopicAsync(string str, CancellationToken ct)
    {
        await SemaphoreSlim.WaitAsync(ct);
        try
        {
            var topic = PulsarTopicParser.Parse(str);
            await RequireNamespace(topic.Tenant, topic.Namespace, -1, -1, ct);
        }
        finally
        {
            SemaphoreSlim.Release();
        }

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
        Console.WriteLine("RequireNamespace - 1");
        var tenants = await GetStringArray("tenants");
        Console.WriteLine("RequireNamespace - 2");
        if (!tenants.Contains(tenant))
        {
            var clusters = await GetStringArray("clusters");
            Console.WriteLine("RequireNamespace - 3");
            var tenantInfo = new TenantInfo { AdminRoles = null, AllowedClusters = clusters };
            try
            {
                await _admin.CreateTenantAsync(tenant, tenantInfo, ct);
                Console.WriteLine("RequireNamespace - 4");
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }

        // Ensure Namespace Exists
        var namespaces = await GetStringArray($"namespaces/{tenant}");
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
                await _admin.CreateNamespaceAsync(tenant, nameSpace, policies, ct);
                Console.WriteLine("RequireNamespace - 6");
            }
            catch (Exception)
            {
                // Ignore race conditions
            }
        }
    }

    private async Task<string[]> GetStringArray(string path)
    {
        var result = await _httpClient.GetStringAsync(path);
        return JsonSerializer.Deserialize<string[]>(result);
    }
}
