using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Record of the state of one stream (in or out of failure mode.)
/// </summary>
public class StreamState
{
    public string StreamId { get; set; }
    public Dictionary<string, TopicState> Topics { get; set; }

    public override string ToString()
    {
        var strBuilder = new StringBuilder();
        strBuilder.Append(StreamId).Append(": {");

        if (Topics != null)
        {
            var topicStrings = Topics
                .Select(t => $"{t.Key}=>{t.Value.ToString(false)}")
                .ToArray();
            strBuilder.Append(string.Join(", ", topicStrings));
        }

        strBuilder.Append('}');
        return strBuilder.ToString();
    }
}
