using OpenTelemetry.Trace;

namespace EventHorizon.EventStreaming.Tracing;

public static class TraceExtensions
{
    public static TracerProviderBuilder AddEventStreamingInstrumentation(this TracerProviderBuilder builder)
    {
        builder.AddSource(TraceConstants.ActivitySourceName);
        return builder;
    }
}
