using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.Abstractions.Testing;

public static class HostExtensions
{
    public static IHost AddTestBucketIds(this IHost host)
    {
        var attributeUtil = host.Services.GetRequiredService<AttributeUtil>();
        TestUtil.SetTestBucketIds(attributeUtil, AssemblyUtil.StateDict.Values.ToArray());
        TestUtil.SetTestBucketIds(attributeUtil, AssemblyUtil.ActionDict.Values.ToArray());
        return host;
    }
}
