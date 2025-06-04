﻿using EventHorizon.EventStreaming.Benchmark.Utilities;
using NBench;
using Xunit.Abstractions;

namespace EventHorizon.EventStreaming.Benchmark.Extensions;

public static class IBenchmarkTraceExtensions
{
    public static ITestOutputHelper AsXunitOutput(this IBenchmarkTrace trace)
    {
        return new NBenchTraceToXUnitOutputAdapter(trace);
    }
}
