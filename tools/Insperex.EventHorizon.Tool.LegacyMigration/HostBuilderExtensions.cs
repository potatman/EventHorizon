using System;
using System.Net.Http;
using Consul;
using Microsoft.Extensions.Hosting;
using Winton.Extensions.Configuration.Consul;
using Winton.Extensions.Configuration.Consul.Parsers;

namespace Insperex.EventHorizon.Tool.LegacyMigration
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseConsul(this IHostBuilder hostBuilder, string serviceName = null)
        {
            return hostBuilder
                .ConfigureAppConfiguration((context, builder) =>
                {
                    var env = context.HostingEnvironment;
                    var datacenter = GetDataCenter(env);
                    var address = Environment.GetEnvironmentVariable("CONSUL_ADDRESS");
                    var token = Environment.GetEnvironmentVariable("CONSUL_TOKEN");

                    // exit if variables are not set
                    if (datacenter == null
                        || address == null
                        || token == null) return;

                    builder.AddConsul($"v1/_global/", x => Config(x, address, token, datacenter));
                    builder.AddConsul($"v1/{serviceName ?? env.ApplicationName}/", x => Config(x, address, token, datacenter));
                });
        }

        private static void Config(IConsulConfigurationSource x, string addr, string token, string dc)
        {
            // Setup Host
            x.ConsulConfigurationOptions = c => GetClientConfig(c, addr, token, dc);

            // Setup Client
            x.ConsulHttpClientHandlerOptions = GetHttpClientConfig;

            // if doesnt exist, move on
            x.Optional = true;

            // Parse Key Value Pairs instead of json
            x.Parser = new SimpleConfigurationParser();

            // Reload if consul made change
            x.ReloadOnChange = true;
        }

        private static void GetClientConfig(ConsulClientConfiguration x, string addr, string token, string dc)
        {
            x.Datacenter = dc;
            x.Address = new Uri(addr);
            x.Token = token;
        }

        private static void GetHttpClientConfig(HttpClientHandler handler)
        {
            // TODO: Remove once cert is fixed
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        private static string GetDataCenter(IHostEnvironment env)
        {
            return env.EnvironmentName.ToLowerInvariant() switch
            {
                "dc1" => "dc1",
                "development" => "dev",
                "staging" => "stage",
                "production" => "prod",
                _ => null
            };
        }
    }
}
