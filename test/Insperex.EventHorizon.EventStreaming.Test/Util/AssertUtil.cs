using System;
using System.Linq;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Util;

public static class AssertUtil
{
    public static void AssertEventsValid(Event[] expected, params MessageContext<Event>[][] results)
    {
        var actual = results.SelectMany(x => x).ToArray();
        
        // Assert Count
        Assert.Equal(expected.Length, results.Sum(x => x.Length));
        
        // Ensure each list has results
        for (var i = 0; i < results.Length; i++)
            Assert.True(results[i].Any(), $"list[{i}] is empty, all lists should receive events");

        // Assert StreamId Order is Correct (within topic)
        var lookup1 = actual.ToLookup(x => new { x.TopicData.Topic, x.Data.StreamId });
        var keys = lookup1.Select(x => x.Key).ToArray();
        foreach (var key in keys)
        {
            var ids = lookup1[key].Select(x => x.Data.SequenceId).ToArray();
            var orderedIds = ids.OrderBy(x => x).ToArray();
            Assert.True(ids.SequenceEqual(orderedIds), $"{key}{Environment.NewLine} Expected => [{string.Join(",", orderedIds)}]{Environment.NewLine}Actual => [{string.Join(",", ids)}]");
        }
        
        // Check if subscriptions read dedicated keys
        var lookups = results.Select(s => s.ToLookup(x => x.Data.StreamId, x => x.Data.SequenceId)).ToArray();
        foreach (var lookupA in lookups)
        {
            foreach (var lookupB in lookups)
            {
                // Ignore Same List
                if(lookupA == lookupB) continue;
                
                // Compare Keys
                var keysA = lookupA.Select(x => x.Key).ToArray();
                var keysB = lookupB.Select(x => x.Key).ToArray();
                var intersect = keysA.Intersect(keysB).ToArray();
                Assert.False(intersect.Any(), "Ids found on different listeners");
            }
        }
        
        // Assert Data Hasn't Changed
        var lookupChange1 = expected.OrderBy(x => x.SequenceId).ToLookup(x => x.StreamId);
        var lookupChange2 = actual.Select(x => x.Data)
            .OrderBy(x => x.SequenceId).ToLookup(x => x.StreamId);
        var streamIds = lookupChange1.Select(x => x.Key).ToArray();
        foreach (var streamId in streamIds)
        {
            var events1 = lookupChange1[streamId].ToArray();
            var events2 = lookupChange2[streamId].ToArray();
            
            Assert.Equal(events1.Length, events2.Length);
            for (var i = 0; i < events1.Length; i++)
            {
                Assert.Equal(events1[i].StreamId, events2[i].StreamId);
                var json1 = JsonSerializer.Serialize(events1[i].Payload);
                var json2 = JsonSerializer.Serialize(events2[i].Payload);
                Assert.Equal(json1, json2);
            }
        }
    }
}