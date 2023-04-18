using System;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.EventStreaming.Util;

public static class NameUtil
{
    public static string AssemblyNameWithGuid => $"{AssemblyUtil.AssemblyName}-{Guid.NewGuid().ToString()[4..]}";
}