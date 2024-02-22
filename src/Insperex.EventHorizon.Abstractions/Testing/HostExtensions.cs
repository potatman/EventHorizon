using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.Abstractions.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.Abstractions.Testing;

public static class HostExtensions
{
    public static IHost AddTestBucketIds(this IHost host, string postfix = null)
    {
        postfix ??= $"_{Guid.NewGuid().ToString()[..8]}";
        var attributeUtil = host.Services.GetRequiredService<AttributeUtil>();
        TestUtil.SetTestBucketIds(attributeUtil, postfix, AssemblyUtil.GetTypes<IState>().ToArray());
        TestUtil.SetTestBucketIds(attributeUtil, postfix, AssemblyUtil.GetTypes<IAction>().ToArray());
        return host;
    }
}
