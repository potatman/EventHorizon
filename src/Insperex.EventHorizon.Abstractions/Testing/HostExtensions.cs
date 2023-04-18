using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.Abstractions.Testing;

public static class HostExtensions
{
    // public static IHostBuilder UseTestBucketIds(this IHostBuilder builder)
    // {
    //     builder.ConfigureServices((Action<HostBuilderContext, IServiceCollection>)((context, collection) =>
    //     {
    //         collection.AddSingleton(x =>
    //         {
    //             var attributeUtil = new AttributeUtil();
    //             TestUtil.SetTestBucketIds(attributeUtil, AssemblyUtil.StateDict.Values.ToArray());
    //             return attributeUtil;
    //         });
    //     }));
    //
    //     return builder;
    // }
    
    public static IHost AddTestBucketIds(this IHost host)
    {
        var attributeUtil = host.Services.GetRequiredService<AttributeUtil>();
        TestUtil.SetTestBucketIds(attributeUtil, AssemblyUtil.StateDict.Values.ToArray());
        var actions = AssemblyUtil.ActionToStateDict.Keys.ToArray();
        TestUtil.SetTestBucketIds(attributeUtil, actions.Select(x => AssemblyUtil.TypeDictionary[x]).ToArray());
        return host;
    }
}