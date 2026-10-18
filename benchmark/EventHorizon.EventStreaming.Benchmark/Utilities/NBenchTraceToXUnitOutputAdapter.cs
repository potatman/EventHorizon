using System.Globalization;
using System.Text;
using NBench;
using Xunit;

namespace EventHorizon.EventStreaming.Benchmark.Utilities;

public class NBenchTraceToXUnitOutputAdapter: ITestOutputHelper
{
    private readonly IBenchmarkTrace _trace;
    private readonly StringBuilder _output = new();

    public NBenchTraceToXUnitOutputAdapter(IBenchmarkTrace trace)
    {
        _trace = trace;
    }

    public string Output => _output.ToString();

    public void Write(string message)
    {
        _output.Append(message);
        _trace.Info(message);
    }

    public void Write(string format, params object[] args)
    {
        Write(string.Format(CultureInfo.InvariantCulture, format ?? "", args));
    }

    public void WriteLine(string message)
    {
        _output.AppendLine(message);
        _trace.Info(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        WriteLine(string.Format(CultureInfo.InvariantCulture, format ?? "", args));
    }
}
