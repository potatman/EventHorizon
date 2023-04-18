using OpenTelemetry.Trace;

namespace Insperex.EventHorizon.EventStreaming.Tracing;

public static class TraceExtensions
{
    public static TracerProviderBuilder AddEventStreamingInstrumentation(this TracerProviderBuilder builder)
    {
        builder.AddSource(TraceConstants.ActivitySourceName);
        return builder;
    }
}