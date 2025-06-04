using System;
using EventHorizon.Abstractions.Util;

namespace EventHorizon.EventStreaming.Util;

public static class NameUtil
{
    public static string AssemblyNameWithGuid => $"{AssemblyUtil.AssemblyName}_{Guid.NewGuid().ToString()[..8]}";
}
