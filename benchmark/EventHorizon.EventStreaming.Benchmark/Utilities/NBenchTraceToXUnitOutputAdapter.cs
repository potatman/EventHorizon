using System.Globalization;
using NBench;
using Xunit.Abstractions;

namespace EventHorizon.EventStreaming.Benchmark.Utilities;

public class NBenchTraceToXUnitOutputAdapter: ITestOutputHelper
{
    private readonly IBenchmarkTrace _trace;

    public NBenchTraceToXUnitOutputAdapter(IBenchmarkTrace trace)
    {
        _trace = trace;
    }

    public void WriteLine(string message)
    {
        _trace.Info(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        var formattedMessage = string.Format(CultureInfo.InvariantCulture, format ?? "", args);
        _trace.Info(formattedMessage);
    }
}
