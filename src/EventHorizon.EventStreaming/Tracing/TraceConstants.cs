using System.Diagnostics;

namespace EventHorizon.EventStreaming.Tracing;

public static class TraceConstants
{
    public const string ActivitySourceName = "Insperex.EventHorizon";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static class Tags
    {
        public const string Count = "Count";
        public const string Start = "Start";
        public const string End = "End";
    }
}
